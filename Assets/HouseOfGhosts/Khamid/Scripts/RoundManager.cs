using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Round Progression Settings")]
    [Tooltip("Total number of captures required to win the game.")]
    [SerializeField] private int totalRounds = 6;

    [Tooltip("After which capture the Attic unlock event should trigger.")]
    [SerializeField] private int atticUnlockCaptureCount = 3;

    [Tooltip("Seconds to wait after a capture before the ghost respawns for the next round.")]
    [SerializeField] private float respawnDelaySeconds = 3.0f;

    [Header("Difficulty Scaling (Rounds 1 to 6)")]
    [Tooltip("Ghost flee speed in Round 1 on Normal difficulty (meters/second).")]
    [SerializeField] private float round1FleeSpeed = 2.8f;

    [Tooltip("How much flee speed increases each round (+0.35 m/s per round).")]
    [SerializeField] private float speedIncreasePerRound = 0.35f;

    [Tooltip("Evasive weave width (meters) in Round 1.")]
    [SerializeField] private float round1WeaveAmplitude = 0.55f;

    [Tooltip("How much weave width increases each round (+0.08m per round, up to 0.95m in Round 6).")]
    [SerializeField] private float weaveAmplitudeIncreasePerRound = 0.08f;

    [Tooltip("Evasive weave speed (Hz) in Round 1.")]
    [SerializeField] private float round1WeaveFrequency = 3.0f;

    [Tooltip("How much weave speed increases each round (+0.3 Hz per round, up to 4.5 Hz in Round 6).")]
    [SerializeField] private float weaveFrequencyIncreasePerRound = 0.3f;

    [Tooltip("Behaviour A stare duration in Round 1 (seconds).")]
    [SerializeField] private float round1WatchDuration = 2.0f;

    [Tooltip("How much stare duration decreases each round (-0.2s per round).")]
    [SerializeField] private float watchDurationDecreasePerRound = 0.2f;

    [Tooltip("Minimum stare duration so it never reaches zero.")]
    [SerializeField] private float minimumWatchDuration = 0.5f;

    [Tooltip("Behaviour A stare probability in Round 1 (0.5 = 50%).")]
    [Range(0f, 1f)]
    [SerializeField] private float round1WatchChance = 0.5f;

    [Tooltip("How much Behaviour A probability decreases each round (-0.05 = -5% per round).")]
    [SerializeField] private float watchChanceDecreasePerRound = 0.05f;

    [Tooltip("Minimum probability for Behaviour A.")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumWatchChance = 0.15f;

    [Header("Current Game State (Read Only)")]
    [SerializeField] private int currentRound = 1;
    [SerializeField] private int capturesCompleted = 0;
    [SerializeField] private bool isAtticUnlocked = false;
    [SerializeField] private bool isGameWon = false;
    [SerializeField] private bool isGameLost = false;

    [Header("References")]
    [Tooltip("Reference to the GhostAI component. Automatically found if left empty.")]
    [SerializeField] private GhostAI ghostAI;

    [Tooltip("Reference to the GhostTarget component. Automatically found if left empty.")]
    [SerializeField] private GhostTarget ghostTarget;

    [Header("Events")]
    [Tooltip("Invoked when capture count changes. Passes (capturesCompleted, totalRounds).")]
    public UnityEvent<int, int> onCaptureCountChanged;

    [Tooltip("Invoked when a new round begins. Passes (currentRound).")]
    public UnityEvent<int> onRoundStarted;

    [Tooltip("Invoked after the 3rd capture (hook for future Attic unlock).")]
    public UnityEvent onAtticUnlocked;

    [Tooltip("Invoked after the 6th capture when the player wins.")]
    public UnityEvent onAllRoundsCompleted;

    private Coroutine m_RespawnCoroutine;

    public int CurrentRound => currentRound;
    public int CapturesCompleted => capturesCompleted;
    public int TotalRounds => totalRounds;
    public bool IsAtticUnlocked => isAtticUnlocked;
    public bool IsGameWon => isGameWon;
    public bool IsGameLost => isGameLost;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RoundManager] Duplicate RoundManager found in scene.", this);
        }
        Instance = this;
    }

    private void Start()
    {
        if (ghostAI == null)
        {
            ghostAI = FindFirstObjectByType<GhostAI>();
        }

        if (ghostTarget == null && ghostAI != null)
        {
            ghostTarget = ghostAI.GetComponent<GhostTarget>();
        }

        if (ghostTarget != null)
        {
            ghostTarget.onGhostCaptured.AddListener(HandleGhostCaptured);
        }
        else
        {
            Debug.LogError("[RoundManager] Could not find GhostTarget in scene!", this);
        }

        currentRound = 1;
        capturesCompleted = 0;
        isAtticUnlocked = false;
        isGameWon = false;
        isGameLost = false;

        ApplyRoundSettingsAndSpawn(currentRound);
        onCaptureCountChanged?.Invoke(capturesCompleted, totalRounds);
    }

    private void OnDestroy()
    {
        if (ghostTarget != null)
        {
            ghostTarget.onGhostCaptured.RemoveListener(HandleGhostCaptured);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandleGhostCaptured()
    {
        if (isGameWon || isGameLost)
            return;

        capturesCompleted++;
        Debug.Log($"[RoundManager] === CAPTURE {capturesCompleted} / {totalRounds} COMPLETED! ===");
        onCaptureCountChanged?.Invoke(capturesCompleted, totalRounds);

        if (!isAtticUnlocked && capturesCompleted >= atticUnlockCaptureCount)
        {
            isAtticUnlocked = true;
            Debug.Log("[RoundManager] *** 3RD CAPTURE REACHED: ATTIC UNLOCK HOOK TRIGGERED! *** (Waiting for Attic 3D model integration)");
            onAtticUnlocked?.Invoke();
        }

        if (capturesCompleted >= totalRounds)
        {
            isGameWon = true;
            Debug.Log("[RoundManager] *** VICTORY! All 6 ghost captures completed! You win! ***");
            onAllRoundsCompleted?.Invoke();
            return;
        }

        if (m_RespawnCoroutine != null)
        {
            StopCoroutine(m_RespawnCoroutine);
        }
        m_RespawnCoroutine = StartCoroutine(WaitAndStartNextRound());
    }

    private IEnumerator WaitAndStartNextRound()
    {
        Debug.Log($"[RoundManager] Next round starting in {respawnDelaySeconds:F1} seconds...");
        yield return new WaitForSeconds(respawnDelaySeconds);

        if (isGameLost)
            yield break;

        currentRound++;
        ApplyRoundSettingsAndSpawn(currentRound);
    }

    private void ApplyRoundSettingsAndSpawn(int roundNumber)
    {
        int roundOffset = Mathf.Max(0, roundNumber - 1);

        float difficultySpeedMultiplier = DangerMeter.Instance != null
            ? DangerMeter.Instance.GetGhostSpeedMultiplier()
            : 1.0f;

        float speedForRound = (round1FleeSpeed + (roundOffset * speedIncreasePerRound)) * difficultySpeedMultiplier;
        float weaveAmpForRound = round1WeaveAmplitude + (roundOffset * weaveAmplitudeIncreasePerRound);
        float weaveFreqForRound = round1WeaveFrequency + (roundOffset * weaveFrequencyIncreasePerRound);
        float watchDurationForRound = Mathf.Max(minimumWatchDuration, round1WatchDuration - (roundOffset * watchDurationDecreasePerRound));
        float watchChanceForRound = Mathf.Clamp(round1WatchChance - (roundOffset * watchChanceDecreasePerRound), minimumWatchChance, 1f);

        Debug.Log($"[RoundManager] ---> STARTING ROUND {roundNumber} / {totalRounds} <---");

        if (ghostAI != null)
        {
            ghostAI.StartNewRound(speedForRound, watchDurationForRound, watchChanceForRound, weaveAmpForRound, weaveFreqForRound);
        }

        onRoundStarted?.Invoke(roundNumber);
    }

    public void StopGameOnLoss()
    {
        if (isGameWon || isGameLost)
            return;

        isGameLost = true;

        if (m_RespawnCoroutine != null)
        {
            StopCoroutine(m_RespawnCoroutine);
            m_RespawnCoroutine = null;
        }

        if (ghostAI != null)
        {
            ghostAI.gameObject.SetActive(false);
        }

        Debug.Log("[RoundManager] Match ended in defeat.");
    }
}