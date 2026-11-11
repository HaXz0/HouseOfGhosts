using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

[DefaultExecutionOrder(0)]
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
    [SerializeField] private bool instantResetOnLoseBeam = false;

    [Tooltip("How fast capture progress drains per second when you look away (0.9 = gradual fair cooldown).")]
    [SerializeField] private float decaySpeed = 0.9f;

    [Header("Stage 1: Spectral Shader & Color Feedback")]
    [Tooltip("Tints the ghost/dummy color while illuminated or phasing through walls.")]
    [SerializeField] private bool showVisualColorFeedback = true;

    [Tooltip("Optional shared spectral material (Mat_GhostSpectral). Automatically created from spectralShader if left empty.")]
    [SerializeField] private Material spectralMaterial;

    [Tooltip("Automatically links to 'HouseOfGhosts/GhostSpectralPresence' for cloaking & dissolve.")]
    [SerializeField] private Shader spectralShader;

    [Tooltip("Spectral icy cyan color when the ghost phases through a solid wall or materializes.")]
    [SerializeField] private Color wallPhaseColor = new Color(0.2f, 0.85f, 1.0f, 1.0f);

    [Tooltip("Color when the flashlight beam first hits the ghost.")]
    [SerializeField] private Color startIlluminatedColor = new Color(1f, 0.9f, 0.3f, 1f);

    [Tooltip("Color right before the 3-second capture finishes.")]
    [SerializeField] private Color nearCaptureColor = new Color(1f, 0.2f, 0.1f, 1f);

    [Header("Stage 2: Supernatural Capture Vanish Effect")]
    [Tooltip("If true, upon 3s capture the ghost performs a fast upward light-dissolve stretch instead of snapping off in 1 frame.")]
    [SerializeField] private bool enableCaptureVanish = true;

    [Tooltip("Duration (seconds) of the supernatural light-implosion / upward dissolve when captured.")]
    [Range(0.1f, 1.2f)]
    [SerializeField] private float captureVanishDuration = 0.38f;

    [Tooltip("Bright supernatural flash color during the capture dissolve.")]
    [SerializeField] private Color captureFlashColor = new Color(0.96f, 0.97f, 1.0f, 1.0f);

    [Tooltip("Scale multiplier at the end of the capture vanish (thin & stretched upward into light).")]
    [SerializeField] private Vector3 captureStretchMultiplier = new Vector3(0.25f, 1.45f, 0.25f);

    [Tooltip("Upward float distance (meters) during the 0.38s capture dissolve.")]
    [SerializeField] private float captureRiseHeight = 0.35f;

    [Header("Stage 2B: Capture Particle VFX")]
    [Tooltip("If true, plays the multi-layer supernatural particle burst when the 3s capture completes.")]
    [SerializeField] private bool enableCaptureParticles = true;

    [Tooltip("Drag VFX_GhostCaptureBurst from 'Assets/HouseOfGhosts/Khamid/Partical systems' here.")]
    [SerializeField] private GameObject captureVfxPrefab;

    [Tooltip("If true, capturing GhostDummy capsules also plays the particle burst for easy testing.")]
    [SerializeField] private bool playParticlesOnTestDummies = true;

    [Tooltip("Vertical offset (meters) from the ghost's center where the capture VFX spawns.")]
    [SerializeField] private float vfxHeightOffset = 0.0f;

    [Tooltip("Overall size multiplier for the capture particle burst.")]
    [Range(0.4f, 2.5f)]
    [SerializeField] private float vfxScaleMultiplier = 1.0f;

    [Header("Events")]
    [Tooltip("Invoked every frame the ghost is illuminated, passing progress from 0.0 to 1.0.")]
    public UnityEvent<float> onCaptureProgress;

    [Tooltip("Invoked once when capture completes successfully.")]
    public UnityEvent onGhostCaptured;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private struct RendererCache
    {
        public Renderer Renderer;
        public Color OriginalColor;
        public Texture OriginalTexture;
        public float LocalMinY;
        public float LocalMaxY;
    }

    private float m_CurrentCaptureTime = 0f;
    private float m_CurrentGraceTimer = 0f;
    private float m_DummyRespawnTimer = 0f;
    private float m_WallPhaseBlend = 0f;

    // Stage 1 spectral presence state (default 1.0 so test dummies stay visible)
    private float m_SpectralVisibility = 1.0f;
    private float m_WipeMode = 0.0f;
    private Color m_CustomRimColor = new Color(0.3f, 0.85f, 1.0f, 1.0f);
    private float m_CustomRimIntensity = 0f;

    // Stage 2 capture vanish animation state
    private bool m_IsPlayingCaptureVanish = false;
    private float m_CaptureVanishTimer = 0f;
    private Vector3 m_RestingScale = Vector3.one;
    private Vector3 m_CaptureStartPos;
    private Vector3 m_CaptureStartScale = Vector3.one;

    private bool m_IsIlluminatedThisFrame = false;
    private bool m_IsUVIlluminatedThisFrame = false;
    private bool m_IsCaptured = false;

    private Collider m_Collider;
    private readonly List<RendererCache> m_CachedRenderers = new List<RendererCache>();
    private MaterialPropertyBlock m_PropBlock;
    private Material m_CreatedMaterialInstance;

    // Pooled world-space Capture VFX instance (never snapped off when Ghost deactivates)
    private static Transform s_VfxPoolContainer;
    private GameObject m_PooledCaptureVfxInstance;
    private ParticleSystem[] m_PooledVfxSystems;

    private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int s_ColorId = Shader.PropertyToID("_Color");
    private static readonly int s_BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int s_MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int s_EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int s_VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int s_WipeModeId = Shader.PropertyToID("_WipeMode");
    private static readonly int s_LocalBoundsMinYId = Shader.PropertyToID("_LocalBoundsMinY");
    private static readonly int s_LocalBoundsMaxYId = Shader.PropertyToID("_LocalBoundsMaxY");
    private static readonly int s_DissolveEdgeColorId = Shader.PropertyToID("_DissolveEdgeColor");
    private static readonly int s_RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int s_RimIntensityId = Shader.PropertyToID("_RimIntensity");
    private static readonly Color s_UVSpectralAuraColor = new Color(0.54f, 0.16f, 1.0f, 1.0f);
    private static readonly Color s_UVSpectralRimColor = new Color(0.72f, 0.28f, 1.0f, 1.0f);

    public float CurrentProgressNormalized => Mathf.Clamp01(m_CurrentCaptureTime / requiredCaptureDuration);
    public bool IsIlluminated => m_IsIlluminatedThisFrame;
    public bool IsRecentlyIlluminated => m_IsIlluminatedThisFrame || m_CurrentGraceTimer > 0f;
    public bool IsUVIlluminated => m_IsUVIlluminatedThisFrame;
    public bool IsCaptured => m_IsCaptured;
    public bool IsTestDummy => isTestDummy;
    public float CurrentVisibility => m_SpectralVisibility;

    public Vector3 TargetCenter => m_Collider != null ? m_Collider.bounds.center : transform.position;

    private void OnValidate()
    {
        if (spectralShader == null)
        {
            spectralShader = Shader.Find("HouseOfGhosts/GhostSpectralPresence");
        }
    }

    private void Awake()
    {
        if (!isTestDummy)
        {
            Instance = this;
        }

        m_RestingScale = transform.localScale;
        m_Collider = GetComponent<Collider>();
        m_PropBlock = new MaterialPropertyBlock();
        InitializeRenderersAndMaterial();
    }

    private void Start()
    {
        EnsureCaptureVfxPooled();
    }

    private void InitializeRenderersAndMaterial()
    {
        m_CachedRenderers.Clear();

        Material activeSpectralMat = spectralMaterial;
        if (activeSpectralMat == null)
        {
            if (spectralShader == null)
            {
                spectralShader = Shader.Find("HouseOfGhosts/GhostSpectralPresence");
            }

            if (spectralShader != null)
            {
                m_CreatedMaterialInstance = new Material(spectralShader)
                {
                    name = $"{gameObject.name}_SpectralMat",
                    enableInstancing = true
                };
                activeSpectralMat = m_CreatedMaterialInstance;
            }
        }

        Renderer[] foundRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < foundRenderers.Length; i++)
        {
            Renderer rend = foundRenderers[i];
            if (rend == null || rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer)
                continue;

            Color origColor = Color.white;
            Texture origTex = null;
            Material sharedMat = rend.sharedMaterial;

            if (sharedMat != null && sharedMat != activeSpectralMat)
            {
                if (sharedMat.HasProperty(s_BaseColorId))
                    origColor = sharedMat.GetColor(s_BaseColorId);
                else if (sharedMat.HasProperty(s_ColorId))
                    origColor = sharedMat.GetColor(s_ColorId);

                if (sharedMat.HasProperty(s_BaseMapId))
                    origTex = sharedMat.GetTexture(s_BaseMapId);
                else if (sharedMat.HasProperty(s_MainTexId))
                    origTex = sharedMat.GetTexture(s_MainTexId);
            }

            float minY = -1.0f;
            float maxY = 1.0f;
            MeshFilter mf = rend.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds localB = mf.sharedMesh.bounds;
                minY = localB.min.y;
                maxY = localB.max.y;
            }
            else if (rend is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                Bounds localB = smr.sharedMesh.bounds;
                minY = localB.min.y;
                maxY = localB.max.y;
            }

            if (activeSpectralMat != null)
            {
                rend.sharedMaterial = activeSpectralMat;
            }

            m_CachedRenderers.Add(new RendererCache
            {
                Renderer = rend,
                OriginalColor = origColor,
                OriginalTexture = origTex,
                LocalMinY = minY,
                LocalMaxY = maxY
            });
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

        if (m_CreatedMaterialInstance != null)
        {
            Destroy(m_CreatedMaterialInstance);
        }

        if (m_PooledCaptureVfxInstance != null)
        {
            Destroy(m_PooledCaptureVfxInstance);
        }
    }

    private void Update()
    {
        if (m_IsCaptured)
        {
            if (m_IsPlayingCaptureVanish)
            {
                UpdateCaptureVanishAnimation();
                return;
            }

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
        m_IsUVIlluminatedThisFrame = false;
    }

    private void UpdateCaptureVanishAnimation()
    {
        m_CaptureVanishTimer += Time.deltaTime;
        float duration = Mathf.Max(0.05f, captureVanishDuration);
        float t = Mathf.Clamp01(m_CaptureVanishTimer / duration);
        float smoothT = t * t * (3f - 2f * t);

        // Stretch upward and narrow into a beam of light while dissolving from feet to head
        Vector3 targetScale = Vector3.Scale(m_CaptureStartScale, captureStretchMultiplier);
        transform.localScale = Vector3.Lerp(m_CaptureStartScale, targetScale, smoothT);
        transform.position = m_CaptureStartPos + Vector3.up * (captureRiseHeight * smoothT);

        float vanishVisibility = Mathf.Clamp01(1.0f - smoothT);

        for (int i = 0; i < m_CachedRenderers.Count; i++)
        {
            RendererCache entry = m_CachedRenderers[i];
            if (entry.Renderer == null)
                continue;

            entry.Renderer.enabled = vanishVisibility > 0.005f;
            entry.Renderer.shadowCastingMode = ShadowCastingMode.Off;

            if (!entry.Renderer.enabled)
                continue;

            entry.Renderer.GetPropertyBlock(m_PropBlock);
            if (entry.OriginalTexture != null)
            {
                m_PropBlock.SetTexture(s_BaseMapId, entry.OriginalTexture);
            }

            m_PropBlock.SetColor(s_BaseColorId, captureFlashColor);
            m_PropBlock.SetColor(s_ColorId, captureFlashColor);
            m_PropBlock.SetFloat(s_VisibilityId, vanishVisibility);
            m_PropBlock.SetFloat(s_WipeModeId, 2.0f); // Mode 2 = Ascend Capture Dissolve
            m_PropBlock.SetFloat(s_LocalBoundsMinYId, entry.LocalMinY);
            m_PropBlock.SetFloat(s_LocalBoundsMaxYId, entry.LocalMaxY);
            m_PropBlock.SetColor(s_DissolveEdgeColorId, captureFlashColor * 1.8f);
            m_PropBlock.SetColor(s_RimColorId, captureFlashColor);
            m_PropBlock.SetFloat(s_RimIntensityId, 2.2f);
            m_PropBlock.SetColor(s_EmissionColorId, captureFlashColor * (1.2f + smoothT * 1.4f));

            entry.Renderer.SetPropertyBlock(m_PropBlock);
        }

        if (t >= 1.0f)
        {
            m_IsPlayingCaptureVanish = false;
            transform.localScale = m_RestingScale;
            transform.position = m_CaptureStartPos;
            FinishCaptureHide();
        }
    }

    private void FinishCaptureHide()
    {
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
    /// Called by FlashlightCaptureDetector when Leo's UV beam hits the ghost (triggers UV Cloak Shimmer without capturing).
    /// </summary>
    public void ReportUVIlluminated()
    {
        if (!m_IsCaptured)
        {
            m_IsUVIlluminatedThisFrame = true;
        }
    }

    /// <summary>
    /// Called by GhostAI to blend the spectral wall-phasing glow (0.0 = normal, 1.0 = inside wall).
    /// </summary>
    public void SetWallPhaseBlend(float blend01)
    {
        m_WallPhaseBlend = Mathf.Clamp01(blend01);
    }

    /// <summary>
    /// Called by GhostAI to control cloaking, bottom-up materialization, UV cloak shimmer, and stalking phantom visibility.
    /// </summary>
    public void SetSpectralState(float visibility01, bool useHeightWipe, Color rimColor, float rimIntensity)
    {
        if (m_IsPlayingCaptureVanish)
            return;

        m_SpectralVisibility = Mathf.Clamp01(visibility01);
        m_WipeMode = useHeightWipe ? 1.0f : 0.0f;
        m_CustomRimColor = rimColor;
        m_CustomRimIntensity = Mathf.Max(0f, rimIntensity);

        UpdateRendererVisibilityToggle();
    }

    private void UpdateRendererVisibilityToggle()
    {
        if (m_IsCaptured && !m_IsPlayingCaptureVanish && isTestDummy)
            return;

        bool shouldEnableRenderers = m_SpectralVisibility > 0.005f;
        ShadowCastingMode shadowMode = m_SpectralVisibility > 0.78f ? ShadowCastingMode.On : ShadowCastingMode.Off;

        for (int i = 0; i < m_CachedRenderers.Count; i++)
        {
            Renderer rend = m_CachedRenderers[i].Renderer;
            if (rend == null)
                continue;

            if (rend.enabled != shouldEnableRenderers)
            {
                rend.enabled = shouldEnableRenderers;
            }
            if (rend.shadowCastingMode != shadowMode)
            {
                rend.shadowCastingMode = shadowMode;
            }
        }
    }

    private void CaptureGhost()
    {
        m_IsCaptured = true;
        m_CurrentCaptureTime = requiredCaptureDuration;
        m_WallPhaseBlend = 0f;

        if (m_Collider != null)
        {
            m_Collider.enabled = false;
        }

        onCaptureProgress?.Invoke(1f);
        Debug.Log($"[{gameObject.name}] *** CAPTURED! *** (3 continuous seconds completed)");
        PlayCaptureVFX();
        onGhostCaptured?.Invoke();

        if (enableCaptureVanish && captureVanishDuration > 0.05f)
        {
            m_IsPlayingCaptureVanish = true;
            m_CaptureVanishTimer = 0f;
            m_CaptureStartPos = transform.position;
            m_CaptureStartScale = m_RestingScale;

            for (int i = 0; i < m_CachedRenderers.Count; i++)
            {
                if (m_CachedRenderers[i].Renderer != null)
                    m_CachedRenderers[i].Renderer.enabled = true;
            }
        }
        else
        {
            UpdateVisualFeedback(false);
            FinishCaptureHide();
        }
    }

    private void EnsureCaptureVfxPooled()
    {
        if (!enableCaptureParticles || captureVfxPrefab == null || m_PooledCaptureVfxInstance != null)
            return;

        if (isTestDummy && !playParticlesOnTestDummies)
            return;

        if (s_VfxPoolContainer == null)
        {
            GameObject containerObj = new GameObject("GhostCaptureVFX_Pool");
            s_VfxPoolContainer = containerObj.transform;
        }

        m_PooledCaptureVfxInstance = Instantiate(captureVfxPrefab, s_VfxPoolContainer);
        m_PooledCaptureVfxInstance.name = $"{gameObject.name}_CaptureVFX";
        m_PooledVfxSystems = m_PooledCaptureVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
        m_PooledCaptureVfxInstance.SetActive(false);
    }

    /// <summary>
    /// Right-click GhostTarget in Play Mode and choose "Preview Capture VFX Now" to test the particle burst anytime!
    /// </summary>
    [ContextMenu("Preview Capture VFX Now")]
    public void PlayCaptureVFX()
    {
        if (!Application.isPlaying || !enableCaptureParticles || captureVfxPrefab == null)
            return;

        if (isTestDummy && !playParticlesOnTestDummies)
            return;

        EnsureCaptureVfxPooled();
        if (m_PooledCaptureVfxInstance == null)
            return;

        Vector3 spawnPos = TargetCenter + Vector3.up * vfxHeightOffset;
        m_PooledCaptureVfxInstance.transform.position = spawnPos;
        m_PooledCaptureVfxInstance.transform.rotation = Quaternion.identity;
        m_PooledCaptureVfxInstance.transform.localScale = Vector3.one * Mathf.Max(0.1f, vfxScaleMultiplier);

        if (!m_PooledCaptureVfxInstance.activeSelf)
        {
            m_PooledCaptureVfxInstance.SetActive(true);
        }

        if (m_PooledVfxSystems != null)
        {
            for (int i = 0; i < m_PooledVfxSystems.Length; i++)
            {
                ParticleSystem ps = m_PooledVfxSystems[i];
                if (ps == null)
                    continue;

                ps.Clear(withChildren: false);
                ps.Play(withChildren: false);
            }
        }
    }

    [ContextMenu("Reset Ghost Target")]
    public void ResetTarget()
    {
        m_IsCaptured = false;
        m_IsPlayingCaptureVanish = false;
        m_CaptureVanishTimer = 0f;
        m_CurrentCaptureTime = 0f;
        m_CurrentGraceTimer = 0f;
        m_WallPhaseBlend = 0f;
        m_IsIlluminatedThisFrame = false;
        m_IsUVIlluminatedThisFrame = false;

        transform.localScale = m_RestingScale;

        if (m_Collider != null)
        {
            m_Collider.enabled = true;
        }

        if (isTestDummy)
        {
            m_SpectralVisibility = 1.0f;
            m_WipeMode = 0.0f;
            SetDummyVisible(true);
        }

        gameObject.SetActive(true);
        UpdateRendererVisibilityToggle();
        UpdateVisualFeedback(false);

        if (showDebugLogs)
            Debug.Log($"[{gameObject.name}] Target has been reset.");
    }

    private void SetDummyVisible(bool visible)
    {
        for (int i = 0; i < m_CachedRenderers.Count; i++)
        {
            if (m_CachedRenderers[i].Renderer != null)
                m_CachedRenderers[i].Renderer.enabled = visible;
        }

        if (m_Collider != null)
            m_Collider.enabled = visible;
    }

    private void UpdateVisualFeedback(bool illuminated)
    {
        if (m_CachedRenderers.Count == 0 || m_PropBlock == null || m_IsPlayingCaptureVanish)
            return;

        if (m_SpectralVisibility <= 0.005f)
            return;

        float progress = CurrentProgressNormalized;
        Color progressColor = Color.Lerp(startIlluminatedColor, nearCaptureColor, progress);
        bool uvHit = m_IsUVIlluminatedThisFrame;

        Color activeRimColor = Color.Lerp(m_CustomRimColor, wallPhaseColor, m_WallPhaseBlend);
        float activeRimIntensity = Mathf.Max(m_CustomRimIntensity, m_WallPhaseBlend * 1.4f);

        if (uvHit)
        {
            activeRimColor = s_UVSpectralRimColor;
            activeRimIntensity = Mathf.Max(activeRimIntensity, 2.25f);
        }

        if (progress > 0.02f)
        {
            float rimWeight = illuminated ? progress : (progress * 0.75f);
            activeRimColor = Color.Lerp(activeRimColor, progressColor, rimWeight);
            activeRimIntensity = Mathf.Max(activeRimIntensity, rimWeight * 1.6f);
        }

        for (int i = 0; i < m_CachedRenderers.Count; i++)
        {
            RendererCache entry = m_CachedRenderers[i];
            if (entry.Renderer == null || !entry.Renderer.enabled)
                continue;

            Color baseStateColor = Color.Lerp(entry.OriginalColor, wallPhaseColor, m_WallPhaseBlend);
            if (uvHit)
            {
                baseStateColor = Color.Lerp(baseStateColor, s_UVSpectralAuraColor, 0.75f);
            }

            Color finalColor = baseStateColor;

            if (showVisualColorFeedback && (illuminated || m_CurrentCaptureTime > 0f))
            {
                // Smoothly blend back toward baseStateColor as m_CurrentCaptureTime gradually decays to 0!
                float colorBlend = illuminated ? 1.0f : Mathf.Clamp01(progress * 1.25f);
                finalColor = Color.Lerp(baseStateColor, progressColor, colorBlend);
            }

            entry.Renderer.GetPropertyBlock(m_PropBlock);

            if (entry.OriginalTexture != null)
            {
                m_PropBlock.SetTexture(s_BaseMapId, entry.OriginalTexture);
            }

            m_PropBlock.SetColor(s_BaseColorId, finalColor);
            m_PropBlock.SetColor(s_ColorId, finalColor);
            m_PropBlock.SetFloat(s_VisibilityId, m_SpectralVisibility);
            m_PropBlock.SetFloat(s_WipeModeId, m_WipeMode);
            m_PropBlock.SetFloat(s_LocalBoundsMinYId, entry.LocalMinY);
            m_PropBlock.SetFloat(s_LocalBoundsMaxYId, entry.LocalMaxY);
            m_PropBlock.SetColor(s_DissolveEdgeColorId, wallPhaseColor);
            m_PropBlock.SetColor(s_RimColorId, activeRimColor);
            m_PropBlock.SetFloat(s_RimIntensityId, activeRimIntensity);

            if (uvHit)
            {
                m_PropBlock.SetColor(s_EmissionColorId, s_UVSpectralAuraColor * 0.85f);
            }
            else if (m_WallPhaseBlend > 0.01f)
            {
                m_PropBlock.SetColor(s_EmissionColorId, wallPhaseColor * (m_WallPhaseBlend * 0.55f));
            }
            else if (illuminated && progress > 0.1f)
            {
                m_PropBlock.SetColor(s_EmissionColorId, progressColor * (progress * 0.5f));
            }
            else
            {
                m_PropBlock.SetColor(s_EmissionColorId, Color.black);
            }

            entry.Renderer.SetPropertyBlock(m_PropBlock);
        }
    }
}
