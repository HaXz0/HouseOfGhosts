using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class GhostTarget : MonoBehaviour
{
    // Global static reference so the flashlight detector can easily find the active ghost
    public static GhostTarget Instance { get; private set; }

    [Header("Capture Settings")]
    [Tooltip("Total continuous seconds the ghost must be illuminated to be captured.")]
    [SerializeField] private float requiredCaptureDuration = 3.0f;

    [Tooltip("If true, losing beam contact instantly resets progress to 0. If false, it drains gradually.")]
    [SerializeField] private bool instantResetOnLoseBeam = true;

    [Tooltip("Decay speed per second if instant reset is disabled.")]
    [SerializeField] private float decaySpeed = 2.0f;

    [Header("Events")]
    [Tooltip("Invoked every frame the ghost is illuminated, passing progress from 0.0 to 1.0.")]
    public UnityEvent<float> onCaptureProgress;

    [Tooltip("Invoked once when capture completes successfully.")]
    public UnityEvent onGhostCaptured;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private float m_CurrentCaptureTime = 0f;
    private bool m_IsIlluminatedThisFrame = false;
    private bool m_IsCaptured = false;
    private Collider m_Collider;

    public float CurrentProgressNormalized => Mathf.Clamp01(m_CurrentCaptureTime / requiredCaptureDuration);
    public bool IsIlluminated => m_IsIlluminatedThisFrame;
    public bool IsCaptured => m_IsCaptured;

    // Returns the center of the ghost's collider (or transform if no collider) for accurate aiming
    public Vector3 TargetCenter => m_Collider != null ? m_Collider.bounds.center : transform.position;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GhostTarget] Duplicate GhostTarget detected. Replacing active instance.", this);
        }
        Instance = this;

        m_Collider = GetComponent<Collider>();
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
        if (m_IsCaptured)
            return;

        if (m_IsIlluminatedThisFrame)
        {
            m_CurrentCaptureTime += Time.deltaTime;

            if (showDebugLogs && Time.frameCount % 20 == 0)
            {
                Debug.Log($"[GhostTarget] Capture Progress: {m_CurrentCaptureTime:F1}s / {requiredCaptureDuration:F1}s ({CurrentProgressNormalized * 100f:F0}%)");
            }

            onCaptureProgress?.Invoke(CurrentProgressNormalized);

            if (m_CurrentCaptureTime >= requiredCaptureDuration)
            {
                CaptureGhost();
            }
        }
        else
        {
            if (m_CurrentCaptureTime > 0f)
            {
                if (instantResetOnLoseBeam)
                {
                    m_CurrentCaptureTime = 0f;
                    if (showDebugLogs)
                        Debug.Log("[GhostTarget] Beam lost! Capture progress reset to 0s.");
                }
                else
                {
                    m_CurrentCaptureTime = Mathf.Max(0f, m_CurrentCaptureTime - Time.deltaTime * decaySpeed);
                }

                onCaptureProgress?.Invoke(CurrentProgressNormalized);
            }
        }

        // Reset the flag for the next frame; detector must re-assert it each frame
        m_IsIlluminatedThisFrame = false;
    }

    /// <summary>
    /// Called by FlashlightCaptureDetector each frame the ghost is inside the light cone without obstructions.
    /// </summary>
    public void ReportIlluminated()
    {
        if (!m_IsCaptured)
        {
            m_IsIlluminatedThisFrame = true;
        }
    }

    private void CaptureGhost()
    {
        m_IsCaptured = true;
        m_CurrentCaptureTime = requiredCaptureDuration;
        onCaptureProgress?.Invoke(1f);

        Debug.Log("[GhostTarget] *** GHOST CAPTURED! *** (3 continuous seconds completed)");
        onGhostCaptured?.Invoke();

        // Disables the ghost object to represent successful capture
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Call this to reset the ghost for testing without restarting Play Mode.
    /// </summary>
    [ContextMenu("Reset Ghost Target")]
    public void ResetTarget()
    {
        m_IsCaptured = false;
        m_CurrentCaptureTime = 0f;
        m_IsIlluminatedThisFrame = false;
        gameObject.SetActive(true);
        Debug.Log("[GhostTarget] Ghost target has been reset.");
    }
}