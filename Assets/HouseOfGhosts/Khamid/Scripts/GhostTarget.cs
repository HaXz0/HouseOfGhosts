using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class GhostTarget : MonoBehaviour
{
    public static GhostTarget Instance { get; private set; }

    public static readonly List<GhostTarget> ActiveTargets = new List<GhostTarget>();

    [Header("Dummy / Testing Settings")]
    [Tooltip("Check this for test dummies! Dummies won't affect RoundManager and will auto-respawn in place.")]
    [SerializeField] private bool isTestDummy = false;

    [Tooltip("Seconds before a captured test dummy pops back up for re-testing.")]
    [SerializeField] private float dummyRespawnDelay = 3.0f;

    [Header("Capture Settings")]
    [Tooltip("Total continuous seconds the ghost must be illuminated to be captured.")]
    [SerializeField] private float requiredCaptureDuration = 3.0f;

    [Tooltip("Short grace buffer (seconds) so tiny VR hand twitches don't immediately wipe out progress.")]
    [SerializeField] private float beamLossGracePeriod = 0.2f;

    [Tooltip("If true, progress resets to 0 after the grace period. If false, it drains gradually.")]
    [SerializeField] private bool instantResetOnLoseBeam = true;

    [Tooltip("Decay speed per second if instant reset is disabled.")]
    [SerializeField] private float decaySpeed = 2.0f;

    [Header("Visual Feedback (Capture & Wall Phasing)")]
    [Tooltip("Tints the ghost/dummy color while illuminated or phasing through walls.")]
    [SerializeField] private bool showVisualColorFeedback = true;

    [Tooltip("Spectral icy cyan color when the ghost phases through a solid wall.")]
    [SerializeField] private Color wallPhaseColor = new Color(0.2f, 0.85f, 1.0f, 1.0f);

    [SerializeField] private Color startIlluminatedColor = new Color(1f, 0.9f, 0.3f, 1f); // Warm Yellow
    [SerializeField] private Color nearCaptureColor = new Color(1f, 0.2f, 0.1f, 1f);      // Bright Red-Orange

    [Header("Events")]
    [Tooltip("Invoked every frame the ghost is illuminated, passing progress from 0.0 to 1.0.")]
    public UnityEvent<float> onCaptureProgress;

    [Tooltip("Invoked once when capture completes successfully.")]
    public UnityEvent onGhostCaptured;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private float m_CurrentCaptureTime = 0f;
    private float m_CurrentGraceTimer = 0f;
    private float m_DummyRespawnTimer = 0f;
    private float m_WallPhaseBlend = 0f;
    private bool m_IsIlluminatedThisFrame = false;
    private bool m_IsCaptured = false;

    private Collider m_Collider;
    private Renderer m_Renderer;
    private MaterialPropertyBlock m_PropBlock;
    private Color m_OriginalColor = Color.white;

    private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int s_ColorId = Shader.PropertyToID("_Color");
    private static readonly int s_EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public float CurrentProgressNormalized => Mathf.Clamp01(m_CurrentCaptureTime / requiredCaptureDuration);
    public bool IsIlluminated => m_IsIlluminatedThisFrame;
    public bool IsCaptured => m_IsCaptured;
    public bool IsTestDummy => isTestDummy;

    public Vector3 TargetCenter => m_Collider != null ? m_Collider.bounds.center : transform.position;

    private void Awake()
    {
        if (!isTestDummy)
        {
            Instance = this;
        }

        m_Collider = GetComponent<Collider>();
        m_Renderer = GetComponentInChildren<Renderer>();

        if (m_Renderer != null && m_Renderer.sharedMaterial != null)
        {
            m_PropBlock = new MaterialPropertyBlock();
            if (m_Renderer.sharedMaterial.HasProperty(s_BaseColorId))
                m_OriginalColor = m_Renderer.sharedMaterial.GetColor(s_BaseColorId);
            else if (m_Renderer.sharedMaterial.HasProperty(s_ColorId))
                m_OriginalColor = m_Renderer.sharedMaterial.GetColor(s_ColorId);
        }
    }

    private void OnEnable()
    {
        if (!ActiveTargets.Contains(this))
        {
            ActiveTargets.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveTargets.Remove(this);
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
        {
            if (isTestDummy)
            {
                m_DummyRespawnTimer -= Time.deltaTime;
                if (m_DummyRespawnTimer <= 0f)
                {
                    ResetTarget();
                }
            }
            return;
        }

        if (m_IsIlluminatedThisFrame)
        {
            m_CurrentGraceTimer = beamLossGracePeriod;
            m_CurrentCaptureTime += Time.deltaTime;

            if (showDebugLogs && Time.frameCount % 20 == 0)
            {
                Debug.Log($"[{gameObject.name}] Capture Progress: {m_CurrentCaptureTime:F1}s / {requiredCaptureDuration:F1}s ({CurrentProgressNormalized * 100f:F0}%)");
            }

            UpdateVisualFeedback(true);
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
                if (m_CurrentGraceTimer > 0f)
                {
                    m_CurrentGraceTimer -= Time.deltaTime;
                }
                else
                {
                    if (instantResetOnLoseBeam)
                    {
                        m_CurrentCaptureTime = 0f;
                        if (showDebugLogs)
                            Debug.Log($"[{gameObject.name}] Beam lost! Capture progress reset to 0s.");
                    }
                    else
                    {
                        m_CurrentCaptureTime = Mathf.Max(0f, m_CurrentCaptureTime - Time.deltaTime * decaySpeed);
                    }

                    onCaptureProgress?.Invoke(CurrentProgressNormalized);
                }
            }

            UpdateVisualFeedback(false);
        }

        m_IsIlluminatedThisFrame = false;
    }

    /// <summary>
    /// Fills a 3-point array (Center, Head/Top, Lower/Legs) so partial peeking around corners can be detected.
    /// </summary>
    public void GetAimPoints(Vector3[] pointsBuffer)
    {
        if (pointsBuffer == null || pointsBuffer.Length < 3)
            return;

        if (m_Collider != null)
        {
            Bounds b = m_Collider.bounds;
            float verticalOffset = b.extents.y * 0.65f;
            pointsBuffer[0] = b.center;
            pointsBuffer[1] = b.center + Vector3.up * verticalOffset;
            pointsBuffer[2] = b.center - Vector3.up * verticalOffset;
        }
        else
        {
            pointsBuffer[0] = transform.position;
            pointsBuffer[1] = transform.position + Vector3.up * 0.5f;
            pointsBuffer[2] = transform.position - Vector3.up * 0.5f;
        }
    }

    public void ReportIlluminated()
    {
        if (!m_IsCaptured)
        {
            m_IsIlluminatedThisFrame = true;
        }
    }

    /// <summary>
    /// Called by GhostAI to blend the spectral wall-phasing glow (0.0 = normal, 1.0 = inside wall).
    /// </summary>
    public void SetWallPhaseBlend(float blend01)
    {
        m_WallPhaseBlend = Mathf.Clamp01(blend01);
    }

    private void CaptureGhost()
    {
        m_IsCaptured = true;
        m_CurrentCaptureTime = requiredCaptureDuration;
        m_WallPhaseBlend = 0f;
        UpdateVisualFeedback(false);
        onCaptureProgress?.Invoke(1f);

        Debug.Log($"[{gameObject.name}] *** CAPTURED! *** (3 continuous seconds completed)");
        onGhostCaptured?.Invoke();

        if (isTestDummy)
        {
            m_DummyRespawnTimer = dummyRespawnDelay;
            SetDummyVisible(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    [ContextMenu("Reset Ghost Target")]
    public void ResetTarget()
    {
        m_IsCaptured = false;
        m_CurrentCaptureTime = 0f;
        m_CurrentGraceTimer = 0f;
        m_WallPhaseBlend = 0f;
        m_IsIlluminatedThisFrame = false;

        SetDummyVisible(true);
        gameObject.SetActive(true);
        UpdateVisualFeedback(false);

        if (showDebugLogs)
            Debug.Log($"[{gameObject.name}] Target has been reset.");
    }

    private void SetDummyVisible(bool visible)
    {
        if (m_Renderer != null)
            m_Renderer.enabled = visible;
        if (m_Collider != null)
            m_Collider.enabled = visible;
    }

    private void UpdateVisualFeedback(bool illuminated)
    {
        if (!showVisualColorFeedback || m_Renderer == null || m_PropBlock == null)
            return;

        // 1. Base color blends between normal material color and icy spectral cyan when phasing through a wall
        Color baseStateColor = Color.Lerp(m_OriginalColor, wallPhaseColor, m_WallPhaseBlend);
        Color finalColor = baseStateColor;

        // 2. If illuminated by flashlight, blend toward warm yellow -> bright red capture glow
        if (illuminated || m_CurrentCaptureTime > 0f)
        {
            Color progressColor = Color.Lerp(startIlluminatedColor, nearCaptureColor, CurrentProgressNormalized);
            finalColor = illuminated ? progressColor : Color.Lerp(baseStateColor, progressColor, 0.5f);
        }

        m_Renderer.GetPropertyBlock(m_PropBlock);
        m_PropBlock.SetColor(s_BaseColorId, finalColor);
        m_PropBlock.SetColor(s_ColorId, finalColor);

        if (m_WallPhaseBlend > 0.01f)
        {
            m_PropBlock.SetColor(s_EmissionColorId, wallPhaseColor * (m_WallPhaseBlend * 0.6f));
        }
        else
        {
            m_PropBlock.SetColor(s_EmissionColorId, Color.black);
        }

        m_Renderer.SetPropertyBlock(m_PropBlock);
    }
}