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

    [Tooltip("Spectral icy blue color when the ghost phases through a solid wall or materializes.")]
    [SerializeField] private Color wallPhaseColor = new Color(0.16f, 0.72f, 1.0f, 1.0f);

    [Tooltip("Color when the flashlight beam first hits the ghost (Option A: Spectral Blue).")]
    [SerializeField] private Color startIlluminatedColor = new Color(0.18f, 0.68f, 1.0f, 1.0f);

    [Tooltip("Color right before the 3-second capture finishes (Option A: Incandescent Icy Silver-Blue).")]
    [SerializeField] private Color nearCaptureColor = new Color(0.56f, 0.84f, 1.0f, 1.0f);

    public enum CaptureVanishStyle
    {
        WindSweptDustDissolve = 0,
        UpwardLightStretch = 1
    }

    [Header("Stage 2: Supernatural Capture Vanish Effect")]
    [Tooltip("If true, upon 3s capture the ghost performs a supernatural dissolve instead of snapping off in 1 frame.")]
    [SerializeField] private bool enableCaptureVanish = true;

    [Tooltip("WindSweptDustDissolve crumbles the ghost into the directional dust cloud; UpwardLightStretch uses the legacy vertical stretch.")]
    [SerializeField] private CaptureVanishStyle captureVanishStyle = CaptureVanishStyle.WindSweptDustDissolve;

    [Tooltip("Duration (seconds) of the supernatural dissolve when captured.")]
    [Range(0.1f, 1.2f)]
    [SerializeField] private float captureVanishDuration = 0.34f;

    [Tooltip("Bright pale silver-white supernatural flash color during the capture dissolve.")]
    [SerializeField] private Color captureFlashColor = new Color(0.96f, 0.97f, 1.0f, 1.0f);

    [Tooltip("Downwind drift distance (meters) as the ghost body crumbles into the wind-swept dust cloud.")]
    [Range(0f, 0.8f)]
    [SerializeField] private float dustDriftDistance = 0.22f;

    [Tooltip("Legacy scale multiplier at the end of UpwardLightStretch vanish.")]
    [SerializeField] private Vector3 captureStretchMultiplier = new Vector3(0.25f, 1.45f, 0.25f);

    [Tooltip("Legacy upward float distance (meters) during UpwardLightStretch vanish.")]
    [SerializeField] private float captureRiseHeight = 0.35f;

    [Header("Stage 2B: Capture Particle VFX")]
    [Tooltip("If true, plays the supernatural particle dispersion when the 3s capture completes.")]
    [SerializeField] private bool enableCaptureParticles = true;

    [Tooltip("Drag VFX_GhostSoulDustDispersion (or VFX_GhostCaptureBurst) from 'Assets/HouseOfGhosts/Khamid/Partical systems' here.")]
    [SerializeField] private GameObject captureVfxPrefab;

    [Tooltip("If true, capturing GhostDummy capsules also plays the particle dispersion for easy testing.")]
    [SerializeField] private bool playParticlesOnTestDummies = true;

    [Tooltip("If true, blasts the dust dispersion away from the player's flashlight beam with a natural sweeping cross-breeze.")]
    [SerializeField] private bool orientVfxAwayFromPlayer = true;

    [Tooltip("Horizontal cross-breeze angle (degrees) so the undulating wavy dust ridges sweep visibly across your VR view.")]
    [Range(0f, 75f)]
    [SerializeField] private float vfxCrosswindAngleDegrees = 38f;

    [Tooltip("Vertical offset (meters) from the ghost's center where the capture VFX spawns.")]
    [SerializeField] private float vfxHeightOffset = 0.0f;

    [Tooltip("Overall master size multiplier applied on top of round scaling.")]
    [Range(0.4f, 2.5f)]
    [SerializeField] private float vfxScaleMultiplier = 1.0f;

    [Header("Stage 2C: Round 1-6 VFX Escalation & Round 6 Finale")]
    [Tooltip("If true, the soul-dust VFX starts very small & brief in Round 1, grows each round, and unleashes a massive long-hanging finale in Round 6.")]
    [SerializeField] private bool enableRoundVfxScaling = true;

    [Tooltip("Cloud size multiplier in Round 1 (very small initial wisp).")]
    [Range(0.2f, 1.2f)]
    [SerializeField] private float round1ScaleMultiplier = 0.42f;

    [Tooltip("Cloud size multiplier by Round 5 (right before the finale).")]
    [Range(0.6f, 2.0f)]
    [SerializeField] private float round5ScaleMultiplier = 1.26f;

    [Tooltip("GRAND FINALE cloud size multiplier on Round 6 (final banishment).")]
    [Range(1.0f, 3.0f)]
    [SerializeField] private float round6FinaleScaleMultiplier = 1.75f;

    [Tooltip("Particle lifetime/duration multiplier in Round 1 (~0.45 = ~0.8s quick puff).")]
    [Range(0.2f, 1.5f)]
    [SerializeField] private float round1DurationMultiplier = 0.45f;

    [Tooltip("Particle lifetime/duration multiplier by Round 5 (~1.45 = ~2.65s).")]
    [Range(0.5f, 3.0f)]
    [SerializeField] private float round5DurationMultiplier = 1.45f;

    [Tooltip("GRAND FINALE particle lifetime multiplier on Round 6 (~3.4 = ~6.2s weightless mid-air hang!).")]
    [Range(1.5f, 6.0f)]
    [SerializeField] private float round6FinaleDurationMultiplier = 3.40f;

    [Tooltip("Dust burst count multiplier in Round 1 (light wisp).")]
    [Range(0.15f, 1.0f)]
    [SerializeField] private float round1DensityMultiplier = 0.35f;

    [Tooltip("Dust burst count multiplier by Round 5.")]
    [Range(0.5f, 1.8f)]
    [SerializeField] private float round5DensityMultiplier = 1.20f;

    [Tooltip("GRAND FINALE dust burst count multiplier on Round 6.")]
    [Range(1.0f, 2.5f)]
    [SerializeField] private float round6FinaleDensityMultiplier = 1.60f;

    [Tooltip("Slower, dramatic ghost body crumble duration (seconds) on the final Round 6 capture.")]
    [Range(0.2f, 1.5f)]
    [SerializeField] private float round6FinaleVanishDuration = 0.62f;

    [Tooltip("0 = Automatic (uses RoundManager on Ghost, or 1->6 cycle on Test Dummies). Set 1-6 to force a specific round for testing.")]
    [Range(0, 6)]
    [SerializeField] private int vfxRoundTestOverride = 0;

    [Tooltip("If true on GhostDummy capsules, each dummy capture steps Round 1 -> 2 -> 3 -> 4 -> 5 -> 6 so you can compare all 6 rounds in a row!")]
    [SerializeField] private bool cycleRoundsOnTestDummies = true;

    [Header("Stage 2D: 0s-3s Peeling Soul-Ash Build-Up")]
    [Tooltip("If true, holding the flashlight beam from 0s to 3s fractures her surface with silver-white veins and peels a live wake of soul-ash into the air!")]
    [SerializeField] private bool enablePeelingBuildUp = true;

    [Tooltip("Drag VFX_GhostPeelingAshLoop from 'Assets/HouseOfGhosts/Khamid/Partical systems' here.")]
    [SerializeField] private GameObject peelingAshVfxPrefab;

    [Tooltip("Overall emission intensity multiplier for the 0s-3s peeling soul-ash stream.")]
    [Range(0.25f, 2.5f)]
    [SerializeField] private float peelingIntensityMultiplier = 1.0f;

    [Tooltip("If true, the 0s-3s peeling stream starts subtle in Round 1 and grows fiercer up to Round 6!")]
    [SerializeField] private bool scalePeelingWithRound = true;

    [Tooltip("Radiant silver-white glow color of the 3D soul-fracture veins and flaking silhouette tears.")]
    [SerializeField] private Color strainCrackColor = new Color(0.96f, 0.99f, 1.0f, 1.0f);

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
    private Color m_CustomRimColor = new Color(0.16f, 0.72f, 1.0f, 1.0f);
    private float m_CustomRimIntensity = 0f;

    // Stage 1B stealth escape capture immunity (Normal Flashlight does nothing until <= reveal distance)
    private bool m_IsCloakCaptureImmune = false;

    private struct VfxLayerBaseline
    {
        public ParticleSystem System;
        public ParticleSystemRenderer Renderer;
        public float Duration;
        public float LifeMin;
        public float LifeMax;
        public float SpeedMin;
        public float SpeedMax;
        public float NoiseScroll;
        public bool HasNoise;
        public int MaxParticles;
        public int BurstCount;
        public ParticleSystem.Burst Burst0;
        public ParticleSystem.Burst Burst1;
    }

    // Stage 2 capture vanish animation state
    private bool m_IsPlayingCaptureVanish = false;
    private float m_CaptureVanishTimer = 0f;
    private float m_ActiveCaptureVanishDuration = 0.34f;
    private float m_ActiveDustDriftDistance = 0.22f;
    private Vector3 m_RestingScale = Vector3.one;
    private Vector3 m_CaptureStartPos;
    private Vector3 m_CaptureStartScale = Vector3.one;
    private Vector3 m_LastVfxWindDirection = Vector3.forward;

    private bool m_IsIlluminatedThisFrame = false;
    private bool m_IsUVIlluminatedThisFrame = false;
    private bool m_IsCaptured = false;

    private Collider m_Collider;
    private readonly List<RendererCache> m_CachedRenderers = new List<RendererCache>();
    private MaterialPropertyBlock m_PropBlock;
    private MaterialPropertyBlock m_VfxPropBlock;
    private Material m_CreatedMaterialInstance;

    private struct PeelingLayerBaseline
    {
        public ParticleSystem System;
        public ParticleSystemRenderer Renderer;
        public float BaseRateOverTime;
        public bool IsSilkWispLayer;
    }

    // Pooled world-space Capture VFX instance (never snapped off when Ghost deactivates)
    private static Transform s_VfxPoolContainer;
    private static int s_NextDummyPreviewRound = 1;
    private static AnimationCurve s_StandardDecelCurve;
    private static AnimationCurve s_FinaleSuspensionCurve;
    private GameObject m_PooledSourcePrefab;
    private GameObject m_PooledCaptureVfxInstance;
    private ParticleSystem[] m_PooledVfxSystems;
    private VfxLayerBaseline[] m_PooledVfxBaselines;

    // Pooled world-space 0s-3s Peeling Soul-Ash Loop instance
    private GameObject m_PooledPeelingSourcePrefab;
    private GameObject m_PooledPeelingVfxInstance;
    private ParticleSystem[] m_PooledPeelingSystems;
    private PeelingLayerBaseline[] m_PooledPeelingBaselines;
    private bool m_IsPeelingVfxEmitting = false;
    private float m_PeelingCrosswindSign = 1f;
    private float m_PeelingWindCheckTimer = 0f;
    private Vector3 m_CurrentPeelingWindDir = Vector3.forward;
    private bool m_HasActivePeelingWind = false;
    private float m_DebugPreviewPeelTimer = 0f;

    private static readonly int s_CloudScaleFactorId = Shader.PropertyToID("_CloudScaleFactor");
    private static readonly int s_DustModeId = Shader.PropertyToID("_DustMode");

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
    private static readonly int s_CaptureStrainId = Shader.PropertyToID("_CaptureStrain");
    private static readonly int s_StrainCrackColorId = Shader.PropertyToID("_StrainCrackColor");

    public float CurrentProgressNormalized => Mathf.Clamp01(m_CurrentCaptureTime / requiredCaptureDuration);
    public bool IsIlluminated => m_IsIlluminatedThisFrame;
    public bool IsCloakCaptureImmune => m_IsCloakCaptureImmune;
    public bool IsRecentlyIlluminated => (!m_IsCloakCaptureImmune && m_IsIlluminatedThisFrame) || m_CurrentGraceTimer > 0f;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticDummyCycle()
    {
        s_NextDummyPreviewRound = 1;
        s_VfxPoolContainer = null;
    }

    private void Awake()
    {
        if (!isTestDummy)
        {
            Instance = this;
            s_NextDummyPreviewRound = 1;
        }

        m_RestingScale = transform.localScale;
        m_Collider = GetComponent<Collider>();
        m_PropBlock = new MaterialPropertyBlock();
        m_VfxPropBlock = new MaterialPropertyBlock();
        InitializeRenderersAndMaterial();
    }

    private void Start()
    {
        EnsureCaptureVfxPooled();
        EnsurePeelingVfxPooled();
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
        StopPeelingVfx(clearImmediately: false);
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

        if (m_PooledPeelingVfxInstance != null)
        {
            Destroy(m_PooledPeelingVfxInstance);
        }
    }

    private void Update()
    {
        if (m_IsCaptured)
        {
            TickStoppedPeelingCleanup();

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

        bool isPreviewingPeel = false;
        if (m_DebugPreviewPeelTimer > 0f)
        {
            m_DebugPreviewPeelTimer = Mathf.Max(0f, m_DebugPreviewPeelTimer - Time.deltaTime);
            m_CurrentCaptureTime = requiredCaptureDuration * 0.85f;
            isPreviewingPeel = true;
        }

        bool effectivelyIlluminated = (m_IsIlluminatedThisFrame || isPreviewingPeel) && !m_IsCloakCaptureImmune;

        if (effectivelyIlluminated)
        {
            if (m_CurrentCaptureTime <= 0.01f)
            {
                m_PeelingWindCheckTimer = 0f;
            }

            m_CurrentGraceTimer = beamLossGracePeriod;
            if (!isPreviewingPeel)
            {
                m_CurrentCaptureTime += Time.deltaTime;
            }

            if (showDebugLogs && !isPreviewingPeel && Time.frameCount % 20 == 0)
            {
                Debug.Log($"[{gameObject.name}] Capture Progress: {m_CurrentCaptureTime:F1}s / {requiredCaptureDuration:F1}s ({CurrentProgressNormalized * 100f:F0}%)");
            }

            UpdateVisualFeedback(true);
            UpdatePeelingBuildUp(true);
            onCaptureProgress?.Invoke(CurrentProgressNormalized);

            if (!isPreviewingPeel && m_CurrentCaptureTime >= requiredCaptureDuration)
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
            UpdatePeelingBuildUp(false);
        }

        m_IsIlluminatedThisFrame = false;
        m_IsUVIlluminatedThisFrame = false;
    }

    private void UpdateCaptureVanishAnimation()
    {
        m_CaptureVanishTimer += Time.deltaTime;
        float duration = Mathf.Max(0.05f, m_ActiveCaptureVanishDuration);
        float t = Mathf.Clamp01(m_CaptureVanishTimer / duration);
        float smoothT = t * t * (3f - 2f * t);

        float wipeModeForVanish = 0.0f;
        if (captureVanishStyle == CaptureVanishStyle.WindSweptDustDissolve)
        {
            // Crumble in place and drift gently downwind with the billowing silver-ash dust cloud
            Vector3 dustExpandScale = Vector3.Scale(m_CaptureStartScale, new Vector3(1.06f, 1.02f, 1.06f));
            transform.localScale = Vector3.Lerp(m_CaptureStartScale, dustExpandScale, smoothT);
            transform.position = m_CaptureStartPos
                + m_LastVfxWindDirection * (m_ActiveDustDriftDistance * smoothT)
                + Vector3.up * (0.04f * smoothT);
            wipeModeForVanish = 0.0f; // Mode 0 = Full-body noisy 3D crumble dissolve
        }
        else
        {
            // Legacy UpwardLightStretch: narrow into a vertical beam of light from feet to head
            Vector3 targetScale = Vector3.Scale(m_CaptureStartScale, captureStretchMultiplier);
            transform.localScale = Vector3.Lerp(m_CaptureStartScale, targetScale, smoothT);
            transform.position = m_CaptureStartPos + Vector3.up * (captureRiseHeight * smoothT);
            wipeModeForVanish = 2.0f; // Mode 2 = Ascend Capture Dissolve
        }

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
            m_PropBlock.SetFloat(s_WipeModeId, wipeModeForVanish);
            m_PropBlock.SetFloat(s_LocalBoundsMinYId, entry.LocalMinY);
            m_PropBlock.SetFloat(s_LocalBoundsMaxYId, entry.LocalMaxY);
            m_PropBlock.SetColor(s_DissolveEdgeColorId, captureFlashColor * 1.6f);
            m_PropBlock.SetColor(s_RimColorId, captureFlashColor);
            m_PropBlock.SetFloat(s_RimIntensityId, 2.0f);
            m_PropBlock.SetColor(s_EmissionColorId, captureFlashColor * (0.85f + smoothT * 1.15f));
            m_PropBlock.SetFloat(s_CaptureStrainId, (enablePeelingBuildUp && captureVanishStyle == CaptureVanishStyle.WindSweptDustDissolve) ? 1.0f : 0.0f);
            m_PropBlock.SetColor(s_StrainCrackColorId, strainCrackColor);

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

    /// <summary>
    /// Returns true if a world-space point (such as a close-range VR flashlight tip) is inside the ghost's collider.
    /// </summary>
    public bool IsPointInsideCollider(Vector3 worldPoint)
    {
        if (m_Collider == null || !m_Collider.enabled)
            return false;

        Vector3 closest = m_Collider.ClosestPoint(worldPoint);
        return (closest - worldPoint).sqrMagnitude < 0.0004f;
    }

    public void ReportIlluminated()
    {
        if (!m_IsCaptured)
        {
            m_IsIlluminatedThisFrame = true;
        }
    }

    /// <summary>
    /// Called by GhostAI during Invisible Flee so the Normal Flashlight does nothing until the player closes within reveal distance.
    /// </summary>
    public void SetCloakCaptureImmunity(bool immune)
    {
        m_IsCloakCaptureImmune = immune;
        if (immune)
        {
            m_CurrentGraceTimer = 0f;
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
        m_DebugPreviewPeelTimer = 0f;

        // Stop emitting new peeling motes so already-shed wake specks finish twinkling out seamlessly into the final burst!
        StopPeelingVfx(clearImmediately: false);

        if (m_Collider != null)
        {
            m_Collider.enabled = false;
        }

        onCaptureProgress?.Invoke(1f);
        int activeRound = ResolveActiveVfxRound();
        Debug.Log($"[{gameObject.name}] *** CAPTURED! *** (Round {activeRound} Soul-Dust VFX triggered)");
        PlayCaptureVFXForRound(activeRound);
        m_HasActivePeelingWind = false;
        onGhostCaptured?.Invoke();

        m_ActiveCaptureVanishDuration = GetVanishDurationForRound(activeRound);
        m_ActiveDustDriftDistance = GetDustDriftForRound(activeRound);

        if (enableCaptureVanish && m_ActiveCaptureVanishDuration > 0.05f)
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

    private void EnsureDecelCurvesInitialized()
    {
        if (s_StandardDecelCurve == null)
        {
            s_StandardDecelCurve = new AnimationCurve(
                new Keyframe(0.00f, 1.00f, 0f, -1.2f),
                new Keyframe(0.45f, 0.48f, -0.7f, -0.6f),
                new Keyframe(1.00f, 0.16f, -0.3f, 0f)
            );
        }

        if (s_FinaleSuspensionCurve == null)
        {
            // Rapid initial room-filling burst (first ~0.8s), then cushions to a weightless mid-air shimmer for ~5.4s!
            s_FinaleSuspensionCurve = new AnimationCurve(
                new Keyframe(0.00f, 1.00f, 0f, -5.8f),
                new Keyframe(0.13f, 0.22f, -1.8f, -0.9f),
                new Keyframe(0.28f, 0.035f, -0.25f, -0.05f),
                new Keyframe(1.00f, 0.015f, -0.01f, 0f)
            );
        }
    }

    private void EnsureCaptureVfxPooled()
    {
        if (!enableCaptureParticles || captureVfxPrefab == null)
            return;

        if (isTestDummy && !playParticlesOnTestDummies)
            return;

        if (m_PooledCaptureVfxInstance != null && m_PooledSourcePrefab == captureVfxPrefab)
            return;

        if (m_PooledCaptureVfxInstance != null)
        {
            Destroy(m_PooledCaptureVfxInstance);
            m_PooledCaptureVfxInstance = null;
        }

        if (s_VfxPoolContainer == null)
        {
            GameObject containerObj = new GameObject("GhostCaptureVFX_Pool");
            s_VfxPoolContainer = containerObj.transform;
        }

        EnsureDecelCurvesInitialized();
        if (m_VfxPropBlock == null)
        {
            m_VfxPropBlock = new MaterialPropertyBlock();
        }

        m_PooledSourcePrefab = captureVfxPrefab;
        m_PooledCaptureVfxInstance = Instantiate(captureVfxPrefab, s_VfxPoolContainer);
        m_PooledCaptureVfxInstance.name = $"{gameObject.name}_CaptureVFX";
        m_PooledVfxSystems = m_PooledCaptureVfxInstance.GetComponentsInChildren<ParticleSystem>(true);

        // Cache baseline particle curves & burst counts once for zero-GC runtime Round 1-6 scaling
        m_PooledVfxBaselines = new VfxLayerBaseline[m_PooledVfxSystems.Length];
        ParticleSystem.Burst[] tempBursts = new ParticleSystem.Burst[4];

        for (int i = 0; i < m_PooledVfxSystems.Length; i++)
        {
            ParticleSystem ps = m_PooledVfxSystems[i];
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            int bCount = emission.GetBursts(tempBursts);

            var noise = ps.noise;
            m_PooledVfxBaselines[i] = new VfxLayerBaseline
            {
                System = ps,
                Renderer = ps.GetComponent<ParticleSystemRenderer>(),
                Duration = main.duration,
                LifeMin = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants ? main.startLifetime.constantMin : main.startLifetime.constant,
                LifeMax = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants ? main.startLifetime.constantMax : main.startLifetime.constant,
                SpeedMin = main.startSpeed.mode == ParticleSystemCurveMode.TwoConstants ? main.startSpeed.constantMin : main.startSpeed.constant,
                SpeedMax = main.startSpeed.mode == ParticleSystemCurveMode.TwoConstants ? main.startSpeed.constantMax : main.startSpeed.constant,
                NoiseScroll = noise.enabled ? noise.scrollSpeed.constant : 0f,
                HasNoise = noise.enabled,
                MaxParticles = main.maxParticles,
                BurstCount = bCount,
                Burst0 = bCount > 0 ? tempBursts[0] : default,
                Burst1 = bCount > 1 ? tempBursts[1] : default
            };
        }

        m_PooledCaptureVfxInstance.SetActive(false);
    }

    private void EnsurePeelingVfxPooled()
    {
        if (!enablePeelingBuildUp || peelingAshVfxPrefab == null)
            return;

        if (isTestDummy && !playParticlesOnTestDummies)
            return;

        if (m_PooledPeelingVfxInstance != null && m_PooledPeelingSourcePrefab == peelingAshVfxPrefab)
            return;

        if (m_PooledPeelingVfxInstance != null)
        {
            Destroy(m_PooledPeelingVfxInstance);
            m_PooledPeelingVfxInstance = null;
        }

        if (s_VfxPoolContainer == null)
        {
            GameObject containerObj = new GameObject("GhostCaptureVFX_Pool");
            s_VfxPoolContainer = containerObj.transform;
        }

        if (m_VfxPropBlock == null)
        {
            m_VfxPropBlock = new MaterialPropertyBlock();
        }

        m_PooledPeelingSourcePrefab = peelingAshVfxPrefab;
        m_PooledPeelingVfxInstance = Instantiate(peelingAshVfxPrefab, s_VfxPoolContainer);
        m_PooledPeelingVfxInstance.name = $"{gameObject.name}_PeelingAshVFX";
        m_PooledPeelingSystems = m_PooledPeelingVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
        m_PooledPeelingBaselines = new PeelingLayerBaseline[m_PooledPeelingSystems.Length];

        for (int i = 0; i < m_PooledPeelingSystems.Length; i++)
        {
            ParticleSystem ps = m_PooledPeelingSystems[i];
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = false;

            var emission = ps.emission;
            float baseRate = emission.rateOverTime.mode == ParticleSystemCurveMode.TwoConstants
                ? emission.rateOverTime.constantMax
                : emission.rateOverTime.constant;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(1.0f);
            emission.rateOverTimeMultiplier = 0f;

            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            bool isSilk = false;
            if (psr != null && psr.sharedMaterial != null && psr.sharedMaterial.HasProperty(s_DustModeId))
            {
                isSilk = psr.sharedMaterial.GetFloat(s_DustModeId) < 0.5f;
            }

            m_PooledPeelingBaselines[i] = new PeelingLayerBaseline
            {
                System = ps,
                Renderer = psr,
                BaseRateOverTime = Mathf.Max(4f, baseRate),
                IsSilkWispLayer = isSilk
            };

            ps.Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        m_IsPeelingVfxEmitting = false;
        m_PooledPeelingVfxInstance.SetActive(false);
    }

    private void UpdatePeelingBuildUp(bool effectivelyIlluminated)
    {
        if (!Application.isPlaying)
            return;

        if (!enablePeelingBuildUp || peelingAshVfxPrefab == null || (isTestDummy && !playParticlesOnTestDummies))
        {
            StopPeelingVfx(clearImmediately: false);
            TickStoppedPeelingCleanup();
            return;
        }

        if (m_PooledPeelingVfxInstance == null || m_PooledPeelingSourcePrefab != peelingAshVfxPrefab)
        {
            EnsurePeelingVfxPooled();
        }

        if (m_PooledPeelingVfxInstance == null || m_IsCloakCaptureImmune || m_SpectralVisibility <= 0.08f)
        {
            StopPeelingVfx(clearImmediately: false);
            TickStoppedPeelingCleanup();
            return;
        }

        float progress = CurrentProgressNormalized;
        float peelIntensity = 0f;
        if (effectivelyIlluminated)
        {
            // Immediate Frame-1 tactile sparks (18%) escalating into a roaring pre-shatter torrent (100%)
            peelIntensity = Mathf.Lerp(0.18f, 1.0f, progress * (0.35f + 0.65f * progress));
        }
        else if (m_CurrentCaptureTime > 0.02f)
        {
            peelIntensity = m_CurrentGraceTimer > 0f
                ? (progress * 0.45f)
                : (progress * progress * 0.28f);
        }

        if (peelIntensity <= 0.008f)
        {
            m_HasActivePeelingWind = false;
            StopPeelingVfx(clearImmediately: false);
            TickStoppedPeelingCleanup();
            return;
        }

        Vector3 spawnPos = TargetCenter + Vector3.up * vfxHeightOffset;
        Vector3 desiredWind = orientVfxAwayFromPlayer ? ComputeSmoothPeelingWindDirection(spawnPos) : Vector3.forward;
        if (!m_HasActivePeelingWind)
        {
            m_CurrentPeelingWindDir = desiredWind;
            m_HasActivePeelingWind = true;
        }
        else
        {
            m_CurrentPeelingWindDir = Vector3.Slerp(m_CurrentPeelingWindDir, desiredWind, Mathf.Clamp01(Time.deltaTime * 9.0f));
            if (m_CurrentPeelingWindDir.sqrMagnitude > 0.0001f)
                m_CurrentPeelingWindDir.Normalize();
        }

        m_PooledPeelingVfxInstance.transform.position = spawnPos;
        m_PooledPeelingVfxInstance.transform.rotation = orientVfxAwayFromPlayer
            ? Quaternion.LookRotation(m_CurrentPeelingWindDir, Vector3.up)
            : Quaternion.identity;

        int upcomingRound = PeekActiveVfxRound();
        int finaleRound = RoundManager.Instance != null ? Mathf.Max(2, RoundManager.Instance.TotalRounds) : 6;
        float roundRateMult = 1.0f;
        float roundScaleMult = 1.0f;

        if (scalePeelingWithRound && enableRoundVfxScaling)
        {
            if (upcomingRound >= finaleRound)
            {
                roundRateMult = 1.48f;
                roundScaleMult = 1.25f;
            }
            else
            {
                float rt = Mathf.Clamp01((upcomingRound - 1) / (float)Mathf.Max(1, finaleRound - 2));
                roundRateMult = Mathf.Lerp(0.65f, 1.22f, rt);
                roundScaleMult = Mathf.Lerp(0.76f, 1.12f, rt);
            }
        }

        float activeScale = Mathf.Max(0.25f, vfxScaleMultiplier * roundScaleMult);
        m_PooledPeelingVfxInstance.transform.localScale = Vector3.one * activeScale;

        if (!m_PooledPeelingVfxInstance.activeSelf)
        {
            m_PooledPeelingVfxInstance.SetActive(true);
        }

        float silkPhaseWeight = Mathf.SmoothStep(0.12f, 1.0f, progress);

        for (int i = 0; i < m_PooledPeelingBaselines.Length; i++)
        {
            PeelingLayerBaseline pb = m_PooledPeelingBaselines[i];
            if (pb.System == null)
                continue;

            float layerIntensity = pb.IsSilkWispLayer ? (peelIntensity * silkPhaseWeight) : peelIntensity;
            var emission = pb.System.emission;
            emission.rateOverTimeMultiplier = pb.BaseRateOverTime * layerIntensity * roundRateMult * peelingIntensityMultiplier;

            if (pb.Renderer != null)
            {
                pb.Renderer.GetPropertyBlock(m_VfxPropBlock);
                m_VfxPropBlock.SetFloat(s_CloudScaleFactorId, activeScale);
                pb.Renderer.SetPropertyBlock(m_VfxPropBlock);
            }

            if (!pb.System.isPlaying)
            {
                pb.System.Play(withChildren: false);
            }
        }

        m_IsPeelingVfxEmitting = true;
    }

    private Vector3 ComputeSmoothPeelingWindDirection(Vector3 spawnPos)
    {
        Vector3 awayDir = -transform.forward;
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 fromPlayer = spawnPos - mainCam.transform.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude > 0.001f)
            {
                awayDir = fromPlayer.normalized;
            }
        }

        awayDir.y = 0f;
        awayDir = awayDir.sqrMagnitude > 0.001f ? awayDir.normalized : Vector3.forward;

        if (vfxCrosswindAngleDegrees <= 0.5f)
            return awayDir;

        m_PeelingWindCheckTimer -= Time.deltaTime;
        if (m_PeelingWindCheckTimer <= 0f)
        {
            m_PeelingWindCheckTimer = 0.12f;
            Vector3 rightSweep = Quaternion.Euler(0f, vfxCrosswindAngleDegrees, 0f) * awayDir;
            Vector3 leftSweep = Quaternion.Euler(0f, -vfxCrosswindAngleDegrees, 0f) * awayDir;

            float checkDist = 1.5f;
            float rightClearance = Physics.Raycast(spawnPos, rightSweep, out RaycastHit hitRight, checkDist, ~0, QueryTriggerInteraction.Ignore)
                ? hitRight.distance
                : checkDist;
            float leftClearance = Physics.Raycast(spawnPos, leftSweep, out RaycastHit hitLeft, checkDist, ~0, QueryTriggerInteraction.Ignore)
                ? hitLeft.distance
                : checkDist;

            if (Mathf.Abs(rightClearance - leftClearance) > 0.18f)
            {
                m_PeelingCrosswindSign = rightClearance >= leftClearance ? 1f : -1f;
            }
            else if (!m_HasActivePeelingWind)
            {
                m_PeelingCrosswindSign = ((GetInstanceID() + Time.frameCount) & 1) == 0 ? 1f : -1f;
            }
        }

        return Quaternion.Euler(0f, m_PeelingCrosswindSign * vfxCrosswindAngleDegrees, 0f) * awayDir;
    }

    private void StopPeelingVfx(bool clearImmediately)
    {
        if (m_PooledPeelingSystems == null)
            return;

        if (!m_IsPeelingVfxEmitting && !clearImmediately)
            return;

        ParticleSystemStopBehavior behavior = clearImmediately
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        for (int i = 0; i < m_PooledPeelingSystems.Length; i++)
        {
            ParticleSystem ps = m_PooledPeelingSystems[i];
            if (ps == null)
                continue;

            var emission = ps.emission;
            emission.rateOverTimeMultiplier = 0f;
            if (ps.isPlaying || clearImmediately)
            {
                ps.Stop(withChildren: false, stopBehavior: behavior);
            }
        }

        m_IsPeelingVfxEmitting = false;
        if (clearImmediately && m_PooledPeelingVfxInstance != null && m_PooledPeelingVfxInstance.activeSelf)
        {
            m_PooledPeelingVfxInstance.SetActive(false);
        }
    }

    private void TickStoppedPeelingCleanup()
    {
        if (m_IsPeelingVfxEmitting || m_PooledPeelingVfxInstance == null || !m_PooledPeelingVfxInstance.activeSelf)
            return;

        if (m_PooledPeelingSystems != null)
        {
            for (int i = 0; i < m_PooledPeelingSystems.Length; i++)
            {
                ParticleSystem ps = m_PooledPeelingSystems[i];
                if (ps != null && ps.particleCount > 0)
                    return;
            }
        }

        m_PooledPeelingVfxInstance.SetActive(false);
    }

    private int PeekActiveVfxRound()
    {
        if (!enableRoundVfxScaling)
            return 4;

        if (vfxRoundTestOverride >= 1 && vfxRoundTestOverride <= 6)
            return vfxRoundTestOverride;

        if (isTestDummy && cycleRoundsOnTestDummies)
            return s_NextDummyPreviewRound;

        if (RoundManager.Instance != null)
        {
            int total = Mathf.Max(1, RoundManager.Instance.TotalRounds);
            return Mathf.Clamp(RoundManager.Instance.CurrentRound, 1, total);
        }

        return 4;
    }

    private int ResolveActiveVfxRound()
    {
        if (!enableRoundVfxScaling)
            return 4;

        if (vfxRoundTestOverride >= 1 && vfxRoundTestOverride <= 6)
            return vfxRoundTestOverride;

        if (isTestDummy && cycleRoundsOnTestDummies)
        {
            int roundToPlay = s_NextDummyPreviewRound;
            s_NextDummyPreviewRound = (s_NextDummyPreviewRound % 6) + 1;
            return roundToPlay;
        }

        if (RoundManager.Instance != null)
        {
            int total = Mathf.Max(1, RoundManager.Instance.TotalRounds);
            return Mathf.Clamp(RoundManager.Instance.CurrentRound, 1, total);
        }

        return 4;
    }

    private float GetVanishDurationForRound(int roundNumber)
    {
        if (!enableRoundVfxScaling)
            return captureVanishDuration;

        int finaleRound = RoundManager.Instance != null ? Mathf.Max(2, RoundManager.Instance.TotalRounds) : 6;
        if (roundNumber >= finaleRound)
            return round6FinaleVanishDuration;

        float t = Mathf.Clamp01((roundNumber - 1) / (float)Mathf.Max(1, finaleRound - 2));
        return Mathf.Lerp(0.22f, Mathf.Max(0.24f, captureVanishDuration * 1.18f), t);
    }

    private float GetDustDriftForRound(int roundNumber)
    {
        if (!enableRoundVfxScaling)
            return dustDriftDistance;

        int finaleRound = RoundManager.Instance != null ? Mathf.Max(2, RoundManager.Instance.TotalRounds) : 6;
        if (roundNumber >= finaleRound)
            return dustDriftDistance * 1.55f;

        float t = Mathf.Clamp01((roundNumber - 1) / (float)Mathf.Max(1, finaleRound - 2));
        return dustDriftDistance * Mathf.Lerp(0.55f, 1.15f, t);
    }

    private void ComputeRoundVfxMultipliers(int roundNumber, out float scaleMult, out float durationMult, out float densityMult, out float windReachMult, out bool isFinale)
    {
        if (!enableRoundVfxScaling)
        {
            scaleMult = 1.0f;
            durationMult = 1.0f;
            densityMult = 1.0f;
            windReachMult = 1.0f;
            isFinale = false;
            return;
        }

        int finaleRound = RoundManager.Instance != null ? Mathf.Max(2, RoundManager.Instance.TotalRounds) : 6;
        if (roundNumber >= finaleRound)
        {
            scaleMult = round6FinaleScaleMultiplier;
            durationMult = round6FinaleDurationMultiplier;
            densityMult = round6FinaleDensityMultiplier;
            windReachMult = 1.48f;
            isFinale = true;
            return;
        }

        // Smooth interpolation across Rounds 1 -> 5
        float t = Mathf.Clamp01((roundNumber - 1) / (float)Mathf.Max(1, finaleRound - 2));
        scaleMult = Mathf.Lerp(round1ScaleMultiplier, round5ScaleMultiplier, t);
        durationMult = Mathf.Lerp(round1DurationMultiplier, round5DurationMultiplier, t);
        densityMult = Mathf.Lerp(round1DensityMultiplier, round5DensityMultiplier, t);
        windReachMult = Mathf.Lerp(0.58f, 1.18f, t);
        isFinale = false;
    }

    private void ApplyRoundScalingToPooledVfx(int roundNumber)
    {
        if (m_PooledVfxBaselines == null || m_PooledCaptureVfxInstance == null)
            return;

        EnsureDecelCurvesInitialized();
        ComputeRoundVfxMultipliers(roundNumber, out float roundScale, out float durationMult, out float densityMult, out float windReachMult, out bool isFinale);

        float finalScale = Mathf.Max(0.15f, vfxScaleMultiplier * roundScale);
        m_PooledCaptureVfxInstance.transform.localScale = Vector3.one * finalScale;

        // Hierarchy scaling already multiplies startSpeed by finalScale, so compensate cleanly for exact wind reach!
        float localSpeedRatio = windReachMult / Mathf.Max(0.2f, roundScale);
        AnimationCurve activeSpeedCurve = isFinale ? s_FinaleSuspensionCurve : s_StandardDecelCurve;

        if (m_VfxPropBlock == null)
        {
            m_VfxPropBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < m_PooledVfxBaselines.Length; i++)
        {
            VfxLayerBaseline b = m_PooledVfxBaselines[i];
            if (b.System == null)
                continue;

            var main = b.System.main;
            main.duration = Mathf.Max(0.4f, b.Duration * durationMult);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.18f, b.LifeMin * durationMult),
                Mathf.Max(0.28f, b.LifeMax * durationMult)
            );
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                b.SpeedMin * localSpeedRatio,
                b.SpeedMax * localSpeedRatio
            );
            main.maxParticles = Mathf.Max(16, Mathf.CeilToInt(b.MaxParticles * Mathf.Max(1f, densityMult * 1.15f)));

            var vel = b.System.velocityOverLifetime;
            if (vel.enabled)
            {
                vel.speedModifier = new ParticleSystem.MinMaxCurve(1.0f, activeSpeedCurve);
            }

            if (b.HasNoise)
            {
                var noise = b.System.noise;
                noise.scrollSpeed = isFinale ? (b.NoiseScroll * 0.58f) : b.NoiseScroll;
            }

            if (b.BurstCount > 0)
            {
                var emission = b.System.emission;
                ParticleSystem.Burst b0 = b.Burst0;
                b0.count = Mathf.Max(2, Mathf.RoundToInt(b.Burst0.count.constant * densityMult));
                emission.SetBurst(0, b0);

                if (b.BurstCount > 1)
                {
                    ParticleSystem.Burst b1 = b.Burst1;
                    b1.count = Mathf.Max(1, Mathf.RoundToInt(b.Burst1.count.constant * densityMult));
                    emission.SetBurst(1, b1);
                }
            }

            if (b.Renderer != null)
            {
                b.Renderer.GetPropertyBlock(m_VfxPropBlock);
                m_VfxPropBlock.SetFloat(s_CloudScaleFactorId, finalScale);
                b.Renderer.SetPropertyBlock(m_VfxPropBlock);
            }
        }
    }

    private Vector3 ComputeVfxWindDirection(Vector3 spawnPos)
    {
        Vector3 awayDir = -transform.forward;
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 fromPlayer = spawnPos - mainCam.transform.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude > 0.001f)
            {
                awayDir = fromPlayer.normalized;
            }
        }

        awayDir.y = 0f;
        if (awayDir.sqrMagnitude < 0.001f)
        {
            awayDir = Vector3.forward;
        }
        else
        {
            awayDir.Normalize();
        }

        if (vfxCrosswindAngleDegrees <= 0.5f)
        {
            return awayDir;
        }

        // Sweep slightly sideways into open room space so the undulating wavy ridges spread clearly across your VR view
        Vector3 rightSweep = Quaternion.Euler(0f, vfxCrosswindAngleDegrees, 0f) * awayDir;
        Vector3 leftSweep = Quaternion.Euler(0f, -vfxCrosswindAngleDegrees, 0f) * awayDir;

        float checkDist = 1.5f;
        float rightClearance = Physics.Raycast(spawnPos, rightSweep, out RaycastHit hitRight, checkDist, ~0, QueryTriggerInteraction.Ignore)
            ? hitRight.distance
            : checkDist;
        float leftClearance = Physics.Raycast(spawnPos, leftSweep, out RaycastHit hitLeft, checkDist, ~0, QueryTriggerInteraction.Ignore)
            ? hitLeft.distance
            : checkDist;

        if (Mathf.Abs(rightClearance - leftClearance) > 0.15f)
        {
            return rightClearance >= leftClearance ? rightSweep : leftSweep;
        }

        return (Time.frameCount & 1) == 0 ? rightSweep : leftSweep;
    }

    /// <summary>
    /// Right-click GhostTarget in Play Mode and choose "Preview Capture VFX Now" to test the particle dispersion anytime!
    /// </summary>
    [ContextMenu("Preview Capture VFX Now")]
    public void PlayCaptureVFX()
    {
        PlayCaptureVFXForRound(ResolveActiveVfxRound());
    }

    [ContextMenu("Preview Round 1 VFX (Tiny Wisp)")]
    public void PreviewRound1VFX()
    {
        PlayCaptureVFXForRound(1);
    }

    [ContextMenu("Preview Round 6 VFX (Grand Finale)")]
    public void PreviewRound6FinaleVFX()
    {
        PlayCaptureVFXForRound(6);
    }

    [ContextMenu("Preview 85% Peeling Overload (2.5s Test)")]
    public void PreviewPeelingOverload()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] Enter Play Mode to preview the live 85% Peeling Soul-Ash & Surface Fracture overload!");
            return;
        }

        if (m_IsCaptured)
        {
            ResetTarget();
        }

        m_IsCloakCaptureImmune = false;
        m_SpectralVisibility = 1.0f;
        m_DebugPreviewPeelTimer = 2.5f;
        m_CurrentCaptureTime = requiredCaptureDuration * 0.85f;
        UpdateRendererVisibilityToggle();
        UpdateVisualFeedback(true);
        UpdatePeelingBuildUp(true);

        if (showDebugLogs)
        {
            Debug.Log($"[{gameObject.name}] Previewing 85% Peeling Soul-Ash & Surface Fracture Overload for 2.5s!");
        }
    }

    public void PlayCaptureVFXForRound(int roundNumber)
    {
        if (!Application.isPlaying || !enableCaptureParticles || captureVfxPrefab == null)
            return;

        if (isTestDummy && !playParticlesOnTestDummies)
            return;

        EnsureCaptureVfxPooled();
        if (m_PooledCaptureVfxInstance == null)
            return;

        Vector3 spawnPos = TargetCenter + Vector3.up * vfxHeightOffset;
        if (orientVfxAwayFromPlayer)
        {
            // Seamlessly inherit the live 0s-3s peeling wind direction so the 3.0s burst continues along the exact same vector!
            m_LastVfxWindDirection = (m_HasActivePeelingWind && m_CurrentPeelingWindDir.sqrMagnitude > 0.001f)
                ? m_CurrentPeelingWindDir
                : ComputeVfxWindDirection(spawnPos);
        }
        else
        {
            m_LastVfxWindDirection = Vector3.forward;
        }

        m_PooledCaptureVfxInstance.transform.position = spawnPos;
        m_PooledCaptureVfxInstance.transform.rotation = orientVfxAwayFromPlayer
            ? Quaternion.LookRotation(m_LastVfxWindDirection, Vector3.up)
            : Quaternion.identity;

        ApplyRoundScalingToPooledVfx(roundNumber);

        if (showDebugLogs && isTestDummy && enableRoundVfxScaling)
        {
            string finaleTag = roundNumber >= 6 ? " [GRAND FINALE!]" : "";
            Debug.Log($"[{gameObject.name}] Previewing Round {roundNumber}/6 Soul-Dust Dispersion{finaleTag}");
        }

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
        m_IsCloakCaptureImmune = false;
        m_IsIlluminatedThisFrame = false;
        m_IsUVIlluminatedThisFrame = false;
        m_DebugPreviewPeelTimer = 0f;
        m_HasActivePeelingWind = false;

        StopPeelingVfx(clearImmediately: false);

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

        Color activeRimColor = Color.Lerp(m_CustomRimColor, wallPhaseColor, m_WallPhaseBlend);
        float activeRimIntensity = Mathf.Max(m_CustomRimIntensity, m_WallPhaseBlend * 1.4f);

        if (!m_IsCloakCaptureImmune && progress > 0.02f)
        {
            float rimWeight = illuminated ? progress : (progress * 0.75f);
            Color overchargeRim = Color.Lerp(progressColor, strainCrackColor, progress * 0.65f);
            activeRimColor = Color.Lerp(activeRimColor, overchargeRim, rimWeight);
            activeRimIntensity = Mathf.Max(activeRimIntensity, rimWeight * 1.85f);
        }

        float strain = (!m_IsCloakCaptureImmune && enablePeelingBuildUp && progress > 0.01f)
            ? (illuminated ? progress : progress * 0.85f)
            : 0f;

        // Spectral color language: Blue when exposed & visible without UV, Purple when cloaked & revealed by UV (0 on GhostDummies)
        float spectralTintStrength = Mathf.Clamp01(m_CustomRimIntensity * 0.38f);
        Color dissolveEdgeColor = m_CustomRimIntensity > 0.05f ? activeRimColor : wallPhaseColor;

        for (int i = 0; i < m_CachedRenderers.Count; i++)
        {
            RendererCache entry = m_CachedRenderers[i];
            if (entry.Renderer == null || !entry.Renderer.enabled)
                continue;

            Color spectralBase = Color.Lerp(entry.OriginalColor, m_CustomRimColor, spectralTintStrength);
            Color baseStateColor = Color.Lerp(spectralBase, wallPhaseColor, m_WallPhaseBlend);
            Color finalColor = baseStateColor;

            if (showVisualColorFeedback && !m_IsCloakCaptureImmune && (illuminated || m_CurrentCaptureTime > 0f))
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
            m_PropBlock.SetColor(s_DissolveEdgeColorId, dissolveEdgeColor);
            m_PropBlock.SetColor(s_RimColorId, activeRimColor);
            m_PropBlock.SetFloat(s_RimIntensityId, activeRimIntensity);
            m_PropBlock.SetFloat(s_CaptureStrainId, strain);
            m_PropBlock.SetColor(s_StrainCrackColorId, strainCrackColor);

            if (m_WallPhaseBlend > 0.01f)
            {
                m_PropBlock.SetColor(s_EmissionColorId, wallPhaseColor * (m_WallPhaseBlend * 0.55f));
            }
            else if (!m_IsCloakCaptureImmune && illuminated && progress > 0.08f)
            {
                // Restrained body glow so the incandescent silver-white 3D fracture veins pop with high contrast!
                m_PropBlock.SetColor(s_EmissionColorId, progressColor * (progress * 0.15f));
            }
            else if (m_CustomRimIntensity > 0.05f)
            {
                m_PropBlock.SetColor(s_EmissionColorId, m_CustomRimColor * 0.14f);
            }
            else
            {
                m_PropBlock.SetColor(s_EmissionColorId, Color.black);
            }

            entry.Renderer.SetPropertyBlock(m_PropBlock);
        }
    }
}
