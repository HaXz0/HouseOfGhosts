using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DangerMeter : MonoBehaviour
{
    public enum GameDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    public static DangerMeter Instance { get; private set; }

    // Static property so the future Difficulty Selection Menu can set difficulty before loading the room
    public static GameDifficulty PersistentDifficulty { get; set; } = GameDifficulty.Normal;

    [Header("Difficulty Settings")]
    [Tooltip("Current difficulty level. Affects Danger Meter drain speed and Ghost movement speed.")]
    [SerializeField] private GameDifficulty difficulty = GameDifficulty.Normal;

    [Tooltip("Danger drain multiplier on Easy (0.75 = 25% slower drain).")]
    [SerializeField] private float easyDrainMultiplier = 0.75f;

    [Tooltip("Danger drain multiplier on Normal (1.0 = standard drain).")]
    [SerializeField] private float normalDrainMultiplier = 1.0f;

    [Tooltip("Danger drain multiplier on Hard (1.35 = 35% faster drain).")]
    [SerializeField] private float hardDrainMultiplier = 1.35f;

    [Tooltip("Ghost speed multiplier on Easy (0.85 = 15% slower ghost).")]
    [SerializeField] private float easyGhostSpeedMultiplier = 0.85f;

    [Tooltip("Ghost speed multiplier on Normal (1.0 = standard speed).")]
    [SerializeField] private float normalGhostSpeedMultiplier = 1.0f;

    [Tooltip("Ghost speed multiplier on Hard (1.2 = 20% faster ghost).")]
    [SerializeField] private float hardGhostSpeedMultiplier = 1.2f;

    [Header("Danger Meter Settings")]
    [Tooltip("Maximum meter value when completely safe.")]
    [SerializeField] private float maxMeter = 100f;

    [Tooltip("Distance in meters within which the ghost drains the Danger Meter.")]
    [SerializeField] private float dangerDistance = 4.0f;

    [Tooltip("Points drained per second on Normal difficulty when the ghost is within danger distance.")]
    [SerializeField] private float baseDrainRatePerSecond = 15.0f;

    [Tooltip("Points recovered per second when the player is at a safe distance.")]
    [SerializeField] private float recoveryRatePerSecond = 8.0f;

    [Header("Current State (Read Only)")]
    [SerializeField] private float currentMeter = 100f;
    [SerializeField] private bool isPlayerInDanger = false;
    [SerializeField] private bool isMeterEmpty = false;

    [Header("References")]
    [Tooltip("Player VR Headset / Camera. Automatically finds Camera.main if left empty.")]
    [SerializeField] private Transform playerHead;

    [Tooltip("Reference to the GhostAI. Automatically found if left empty.")]
    [SerializeField] private GhostAI ghostAI;

    [Header("Events")]
    [Tooltip("Invoked when the meter changes, passing normalized value (1.0 = full, 0.0 = empty).")]
    public UnityEvent<float> onMeterChanged;

    [Tooltip("Invoked immediately when the Danger Meter hits 0 (Lose condition).")]
    public UnityEvent onMeterEmpty;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public GameDifficulty CurrentDifficulty => difficulty;
    public float CurrentMeter => currentMeter;
    public float MaxMeter => maxMeter;
    public float CurrentMeterNormalized => Mathf.Clamp01(currentMeter / maxMeter);
    public bool IsPlayerInDanger => isPlayerInDanger;
    public bool IsMeterEmpty => isMeterEmpty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DangerMeter] Duplicate DangerMeter detected in scene.", this);
        }
        Instance = this;

        currentMeter = maxMeter;
    }

    private void Start()
    {
        if (playerHead == null && Camera.main != null)
        {
            playerHead = Camera.main.transform;
        }

        if (ghostAI == null)
        {
            ghostAI = FindFirstObjectByType<GhostAI>();
        }

        onMeterChanged?.Invoke(CurrentMeterNormalized);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // Stop updating if the player already lost or already won all 6 rounds
        if (isMeterEmpty)
            return;

        if (RoundManager.Instance != null && RoundManager.Instance.IsGameWon)
            return;

        isPlayerInDanger = CheckIfGhostIsNearby(out float distanceToGhost);

        if (isPlayerInDanger)
        {
            float drainAmount = baseDrainRatePerSecond * GetDangerDrainMultiplier() * Time.deltaTime;
            currentMeter = Mathf.Max(0f, currentMeter - drainAmount);

            onMeterChanged?.Invoke(CurrentMeterNormalized);

            if (showDebugLogs && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[DangerMeter] DANGER! Ghost is {distanceToGhost:F1}m away | Meter: {currentMeter:F0}/{maxMeter:F0} ({CurrentMeterNormalized * 100f:F0}%) | Difficulty: {difficulty}");
            }

            if (currentMeter <= 0f)
            {
                TriggerGameOver();
            }
        }
        else if (currentMeter < maxMeter)
        {
            currentMeter = Mathf.Min(maxMeter, currentMeter + recoveryRatePerSecond * Time.deltaTime);
            onMeterChanged?.Invoke(CurrentMeterNormalized);

            if (showDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[DangerMeter] Safe — Recovering meter: {currentMeter:F0}/{maxMeter:F0} ({CurrentMeterNormalized * 100f:F0}%)");
            }
        }
    }

    private bool CheckIfGhostIsNearby(out float distanceToGhost)
    {
        distanceToGhost = float.MaxValue;

        if (playerHead == null || ghostAI == null)
            return false;

        // Ghost does not drain meter while disabled/respawning between rounds or already captured
        if (!ghostAI.gameObject.activeInHierarchy || ghostAI.CurrentState == GhostAI.GhostState.Captured)
            return false;

        distanceToGhost = Vector3.Distance(playerHead.position, ghostAI.transform.position);
        return distanceToGhost <= dangerDistance;
    }

    private void TriggerGameOver()
    {
        isMeterEmpty = true;
        currentMeter = 0f;
        onMeterChanged?.Invoke(0f);

        Debug.Log("[DangerMeter] *** GAME OVER! Danger Meter is completely empty (0%)! YOU LOSE! ***");
        onMeterEmpty?.Invoke();

        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.StopGameOnLoss();
        }
    }

    /// <summary>
    /// Returns the Danger Meter drain multiplier for the selected difficulty.
    /// </summary>
    public float GetDangerDrainMultiplier()
    {
        switch (difficulty)
        {
            case GameDifficulty.Easy:
                return easyDrainMultiplier;
            case GameDifficulty.Hard:
                return hardDrainMultiplier;
            case GameDifficulty.Normal:
            default:
                return normalDrainMultiplier;
        }
    }

    /// <summary>
    /// Returns the Ghost movement speed multiplier for the selected difficulty.
    /// </summary>
    public float GetGhostSpeedMultiplier()
    {
        switch (difficulty)
        {
            case GameDifficulty.Easy:
                return easyGhostSpeedMultiplier;
            case GameDifficulty.Hard:
                return hardGhostSpeedMultiplier;
            case GameDifficulty.Normal:
            default:
                return normalGhostSpeedMultiplier;
        }
    }

    /// <summary>
    /// Allows UI menus or Inspector tests to change the difficulty.
    /// </summary>
    public void SetDifficulty(GameDifficulty newDifficulty)
    {
        difficulty = newDifficulty;
        PersistentDifficulty = newDifficulty;
        Debug.Log($"[DangerMeter] Difficulty set to: {difficulty}");
    }

    private void OnDrawGizmosSelected()
    {
        if (ghostAI != null)
        {
            Gizmos.color = isPlayerInDanger ? Color.red : new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(ghostAI.transform.position, dangerDistance);
        }
    }
}