using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class DangerMeterTensionEffect : MonoBehaviour
{
    [Header("Live Test Slider (Play Mode)")]
    [Tooltip("Drag from 0 to 1 in Play Mode to immediately preview and tune the Danger Vignette & Heartbeat!")]
    [Range(0f, 1f)]
    [SerializeField] private float previewDangerOverride = 0f;

    [Tooltip("Read-only current danger visual strength (0 = Safe, 1 = Maximum Danger).")]
    [SerializeField] private float currentDangerStrength = 0f;

    [Header("Stage 4: Danger Vignette Color & Intensity")]
    [Tooltip("Master toggle for the VR-comfortable peripheral Danger Vignette.")]
    [SerializeField] private bool enableTensionEffect = true;

    [Tooltip("Main peripheral glow/shadow color when in danger (click to pick any color!).")]
    [SerializeField] private Color dangerVignetteColor = new Color(0.55f, 0.03f, 0.06f, 1.0f);

    [Tooltip("Deep shadow color at the extreme outer corners of vision.")]
    [SerializeField] private Color outerCornerShadowColor = new Color(0.05f, 0.0f, 0.01f, 1.0f);

    [Tooltip("Maximum Vignette strength when Danger Meter nears 0% (center vision always stays clear for VR comfort).")]
    [Range(0.15f, 0.95f)]
    [SerializeField] private float maxVignetteIntensity = 0.72f;

    [Tooltip("Softness of the peripheral vignette edge transition.")]
    [Range(0.15f, 1.0f)]
    [SerializeField] private float vignetteSmoothness = 0.55f;

    [Tooltip("Normalized Danger Meter level (0.95 = 95%) below which the peripheral vignette begins appearing.")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float dangerStartThreshold = 0.95f;

    [Tooltip("How fast the visual tension smoothly fades in and out.")]
    [SerializeField] private float transitionSmoothSpeed = 4.0f;

    [Header("Heartbeat Peripheral Pulse (No Camera Shake)")]
    [Tooltip("If true, pulses the vignette border like a racing heartbeat as danger rises.")]
    [SerializeField] private bool enableHeartbeatPulse = true;

    [Tooltip("Heartbeat speed (Hz) when danger first starts.")]
    [SerializeField] private float minPulseFrequency = 1.4f;

    [Tooltip("Heartbeat speed (Hz) when Danger Meter is nearly empty (0%).")]
    [SerializeField] private float maxPulseFrequency = 3.8f;

    [Tooltip("Extra Vignette strength added on each heartbeat throb.")]
    [Range(0f, 0.30f)]
    [SerializeField] private float pulseIntensityBoost = 0.14f;

    [Header("Cold Horror Peripheral Tint")]
    [Tooltip("If true, adds a cold, eerie blue-grey dread wash around your outer vision as danger rises.")]
    [SerializeField] private bool enableColdDesaturation = true;

    [Tooltip("Cold peripheral tint color.")]
    [SerializeField] private Color coldVeilColor = new Color(0.08f, 0.14f, 0.22f, 1.0f);

    [Tooltip("Maximum strength of the cold peripheral horror wash.")]
    [Range(0f, 0.45f)]
    [SerializeField] private float maxColdVeilStrength = 0.20f;

    [Header("References & Shader")]
    [Tooltip("Material using HouseOfGhosts/DangerVignetteOverlay (Mat_DangerVignette).")]
    [SerializeField] private Material vignetteMaterial;

    [Tooltip("Shader fallback if vignetteMaterial is left empty.")]
    [SerializeField] private Shader vignetteShader;

    [Tooltip("Reference to DangerMeter. Automatically found if left empty.")]
    [SerializeField] private DangerMeter dangerMeter;

    [Tooltip("Main VR Camera. Automatically finds Camera.main if left empty.")]
    [SerializeField] private Camera targetCamera;

    private GameObject m_OverlayObject;
    private MeshRenderer m_OverlayRenderer;
    private Mesh m_QuadMesh;
    private Material m_CreatedMaterialInstance;
    private MaterialPropertyBlock m_PropBlock;
    private float m_PulsePhase = 0f;

    private static readonly int s_VignetteColorId = Shader.PropertyToID("_VignetteColor");
    private static readonly int s_OuterShadowColorId = Shader.PropertyToID("_OuterShadowColor");
    private static readonly int s_ColdVeilColorId = Shader.PropertyToID("_ColdVeilColor");
    private static readonly int s_IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int s_SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int s_PulseBoostId = Shader.PropertyToID("_PulseBoost");
    private static readonly int s_ColdVeilStrengthId = Shader.PropertyToID("_ColdVeilStrength");

    private void OnValidate()
    {
        if (vignetteShader == null)
        {
            vignetteShader = Shader.Find("HouseOfGhosts/DangerVignetteOverlay");
        }
    }

    private void Awake()
    {
        m_PropBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (dangerMeter == null)
        {
            dangerMeter = DangerMeter.Instance != null ? DangerMeter.Instance : FindFirstObjectByType<DangerMeter>();
        }

        EnsureOverlayAttachedToCamera();
        ApplyEffectValues(0f);
    }

    private void OnDestroy()
    {
        if (m_OverlayObject != null)
        {
            Destroy(m_OverlayObject);
        }

        if (m_QuadMesh != null)
        {
            Destroy(m_QuadMesh);
        }

        if (m_CreatedMaterialInstance != null)
        {
            Destroy(m_CreatedMaterialInstance);
        }
    }

    private void LateUpdate()
    {
        if (!enableTensionEffect)
        {
            if (currentDangerStrength > 0f || (m_OverlayRenderer != null && m_OverlayRenderer.enabled))
            {
                currentDangerStrength = 0f;
                ApplyEffectValues(0f);
            }
            return;
        }

        EnsureOverlayAttachedToCamera();

        if (dangerMeter == null && DangerMeter.Instance != null)
        {
            dangerMeter = DangerMeter.Instance;
        }

        float targetDanger01 = 0f;
        if (dangerMeter != null)
        {
            float meterNorm = dangerMeter.CurrentMeterNormalized;
            if (meterNorm < dangerStartThreshold)
            {
                targetDanger01 = 1.0f - Mathf.Clamp01(meterNorm / Mathf.Max(0.05f, dangerStartThreshold));
            }
        }

        if (previewDangerOverride > 0.001f)
        {
            targetDanger01 = Mathf.Max(targetDanger01, previewDangerOverride);
        }

        currentDangerStrength = Mathf.MoveTowards(currentDangerStrength, targetDanger01, transitionSmoothSpeed * Time.deltaTime);
        ApplyEffectValues(currentDangerStrength);
    }

    private void EnsureOverlayAttachedToCamera()
    {
        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
            return;

        if (m_OverlayObject != null)
        {
            if (m_OverlayObject.transform.parent != targetCamera.transform)
            {
                m_OverlayObject.transform.SetParent(targetCamera.transform, false);
                m_OverlayObject.transform.localPosition = new Vector3(0f, 0f, 0.15f);
                m_OverlayObject.transform.localRotation = Quaternion.identity;
                m_OverlayObject.transform.localScale = new Vector3(1.6f, 1.6f, 1.0f);
            }
            return;
        }

        Material activeMat = vignetteMaterial;
        if (activeMat == null)
        {
            if (vignetteShader == null)
            {
                vignetteShader = Shader.Find("HouseOfGhosts/DangerVignetteOverlay");
            }

            if (vignetteShader != null)
            {
                m_CreatedMaterialInstance = new Material(vignetteShader)
                {
                    name = "RuntimeDangerVignetteMat",
                    enableInstancing = true
                };
                activeMat = m_CreatedMaterialInstance;
            }
        }

        if (activeMat == null)
            return;

        // Build a lightweight 4-vertex quad with ZERO Collider so raycasts & VR hands are never blocked!
        m_QuadMesh = new Mesh
        {
            name = "DangerVignetteQuad"
        };
        m_QuadMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f)
        };
        m_QuadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        m_QuadMesh.bounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 4f));

        m_OverlayObject = new GameObject("DangerVignette_CameraOverlay");
        m_OverlayObject.layer = targetCamera.gameObject.layer;
        m_OverlayObject.transform.SetParent(targetCamera.transform, false);
        m_OverlayObject.transform.localPosition = new Vector3(0f, 0f, 0.15f);
        m_OverlayObject.transform.localRotation = Quaternion.identity;
        m_OverlayObject.transform.localScale = new Vector3(1.6f, 1.6f, 1.0f);

        MeshFilter mf = m_OverlayObject.AddComponent<MeshFilter>();
        mf.sharedMesh = m_QuadMesh;

        m_OverlayRenderer = m_OverlayObject.AddComponent<MeshRenderer>();
        m_OverlayRenderer.sharedMaterial = activeMat;
        m_OverlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        m_OverlayRenderer.receiveShadows = false;
        m_OverlayRenderer.lightProbeUsage = LightProbeUsage.Off;
        m_OverlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        m_OverlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        m_OverlayRenderer.allowOcclusionWhenDynamic = false;
        m_OverlayRenderer.enabled = false;
    }

    private void ApplyEffectValues(float danger01)
    {
        if (m_OverlayRenderer == null)
            return;

        bool effectActive = danger01 > 0.002f;
        if (m_OverlayRenderer.enabled != effectActive)
        {
            m_OverlayRenderer.enabled = effectActive;
        }

        if (!effectActive)
            return;

        float pulseBoost = 0f;
        if (enableHeartbeatPulse)
        {
            float pulseFreq = Mathf.Lerp(minPulseFrequency, maxPulseFrequency, danger01);
            m_PulsePhase += Time.deltaTime * pulseFreq * Mathf.PI * 2f;
            // Double-beat systolic curve so it feels like an organic racing heartbeat
            float beatWave = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(m_PulsePhase)), 4f);
            pulseBoost = beatWave * pulseIntensityBoost * danger01;
        }

        float coldStrength = enableColdDesaturation ? (maxColdVeilStrength * danger01) : 0f;

        m_OverlayRenderer.GetPropertyBlock(m_PropBlock);
        m_PropBlock.SetColor(s_VignetteColorId, dangerVignetteColor);
        m_PropBlock.SetColor(s_OuterShadowColorId, outerCornerShadowColor);
        m_PropBlock.SetColor(s_ColdVeilColorId, coldVeilColor);
        m_PropBlock.SetFloat(s_IntensityId, maxVignetteIntensity * danger01);
        m_PropBlock.SetFloat(s_SmoothnessId, vignetteSmoothness);
        m_PropBlock.SetFloat(s_PulseBoostId, pulseBoost);
        m_PropBlock.SetFloat(s_ColdVeilStrengthId, coldStrength);
        m_OverlayRenderer.SetPropertyBlock(m_PropBlock);
    }
}
