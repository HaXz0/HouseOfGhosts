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

    [Header("Stage 5: 60-Second Search Hint System (Should)")]
    [Tooltip("If true, triggers a ghostly shimmer + UV breadcrumb trail if the player goes 60 seconds without spotting the ghost.")]
    [SerializeField] private bool enableSearchHints = true;

    [Tooltip("Seconds without spotting/illuminating the ghost before the first Search Hint triggers (GDD = ~60s).")]
    [SerializeField] private float searchHintDelaySeconds = 60.0f;

    [Tooltip("Seconds before repeating another Search Hint if the player still hasn't found her.")]
    [SerializeField] private float repeatHintIntervalSeconds = 30.0f;

    [Header("Difficulty Scaling (Rounds 1 to 6)")]
    [Tooltip("Ghost flee speed in Round 1 on Normal difficulty (meters/second).")]
    [SerializeField] private float round1FleeSpeed = 2.8f;

    [Tooltip("How much flee speed increases each round (+0.35 m/s per round).")]
    [SerializeField] private float speedIncreasePerRound = 0.35f;

    [Tooltip("Evasive weave width (meters) in Round 1 (0.30m = gentle ghostly S-curve).")]
    [SerializeField] private float round1WeaveAmplitude = 0.30f;

    [Tooltip("How much weave width increases each round (+0.04m per round, up to 0.50m in Round 6).")]
    [SerializeField] private float weaveAmplitudeIncreasePerRound = 0.04f;

    [Tooltip("Evasive weave cycle speed (Hz) in Round 1 (0.55 Hz = ~1.8s smooth floating sway).")]
    [SerializeField] private float round1WeaveFrequency = 0.55f;

    [Tooltip("How much weave cycle speed increases each round (+0.06 Hz per round, up to 0.85 Hz in Round 6).")]
    [SerializeField] private float weaveFrequencyIncreasePerRound = 0.06f;

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
    [SerializeField] private float secondsWithoutSpottingGhost = 0f;
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

    [Tooltip("Invoked after 60s without finding the ghost. Passes the ghost's current world position (ready for 3D Spatial Audio!).")]
    public UnityEvent<Vector3> onSearchHintTriggered;

    [Tooltip("Invoked after the 3rd capture (hook for future Attic unlock).")]
    public UnityEvent onAtticUnlocked;

    [Tooltip("Invoked after the 6th capture when the player wins.")]
    public UnityEvent onAllRoundsCompleted;

    private Coroutine m_RespawnCoroutine;
    private bool m_HasTriggeredFirstHintThisSearch = false;
    private bool m_IsWaitingForNextRound = false;

    public int CurrentRound => currentRound;
    public int CapturesCompleted => capturesCompleted;
    public int TotalRounds => totalRounds;
    public float SecondsWithoutSpottingGhost => secondsWithoutSpottingGhost;
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
        secondsWithoutSpottingGhost = 0f;
        m_HasTriggeredFirstHintThisSearch = false;
        m_IsWaitingForNextRound = false;
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

    private void Update()
    {
        if (!enableSearchHints || isGameWon || isGameLost || m_IsWaitingForNextRound || ghostAI == null)
            return;

        // Reset search timer whenever the player actively spots or illuminates the ghost!
        bool playerSpottedGhost =
            ghostAI.CurrentState == GhostAI.GhostState.WatchingPlayer ||
            ghostAI.CurrentState == GhostAI.GhostState.Fleeing ||
            (ghostTarget != null && (ghostTarget.IsIlluminated || ghostTarget.IsUVIlluminated));

        if (playerSpottedGhost)
        {
            secondsWithoutSpottingGhost = 0f;
            m_HasTriggeredFirstHintThisSearch = false;
            return;
        }

        secondsWithoutSpottingGhost += Time.deltaTime;
        float requiredDelay = m_HasTriggeredFirstHintThisSearch
            ? Mathf.Max(5f, repeatHintIntervalSeconds)
            : Mathf.Max(5f, searchHintDelaySeconds);

        if (secondsWithoutSpottingGhost >= requiredDelay)
        {
            secondsWithoutSpottingGhost = 0f;
            m_HasTriggeredFirstHintThisSearch = true;
            TriggerSearchHintNow();
        }
    }

    /// <summary>
    /// Right-click the RoundManager component header in the Inspector and choose "Trigger Search Hint Now" to test anytime!
    /// </summary>
    [ContextMenu("Trigger Search Hint Now")]
    public void TriggerSearchHintNow()
    {
        if (ghostAI == null)
            return;

        Debug.Log($"[RoundManager] *** 60s SEARCH HINT TRIGGERED (Round {currentRound})! Helping player locate the ghost... ***");
        ghostAI.EmitSearchHint();
        onSearchHintTriggered?.Invoke(ghostAI.transform.position);
    }

    private void HandleGhostCaptured()
    {
        if (isGameWon || isGameLost)
            return;

        secondsWithoutSpottingGhost = 0f;
        m_HasTriggeredFirstHintThisSearch = false;

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
        m_IsWaitingForNextRound = true;
        Debug.Log($"[RoundManager] Next round starting in {respawnDelaySeconds:F1} seconds...");
        yield return new WaitForSeconds(respawnDelaySeconds);
        m_IsWaitingForNextRound = false;

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

        secondsWithoutSpottingGhost = 0f;
        m_HasTriggeredFirstHintThisSearch = false;

        Debug.Log($"[RoundManager] ---> STARTING ROUND {roundNumber} / {totalRounds} <---");

        if (ghostAI != null)
        {
            ghostAI.StartNewRound(speedForRound, watchDurationForRound, watchChanceForRound, weaveAmpForRound, weaveFreqForRound, roundNumber);
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
