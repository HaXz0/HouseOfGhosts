using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(10)]
[DisallowMultipleComponent]
public class GhostTrace : MonoBehaviour
{
    public enum TraceVisualType
    {
        Footprint = 0,
        WallSplash = 1,
        ResidualRing = 2,
        GhostOutline = 3,
        Handprint = 4,
        WallPhaseRift = WallSplash
    }

    public static readonly List<GhostTrace> ActiveTraces = new List<GhostTrace>();

    [Header("UV Reveal Settings")]
    [Tooltip("Shape rendered on this UV trace (Footprint, WallSplash, ResidualRing, GhostOutline, or Handprint).")]
    [SerializeField] private TraceVisualType traceType = TraceVisualType.Footprint;

    [Tooltip("Fluorescent color when revealed by the UV flashlight.")]
    [SerializeField] private Color uvFluorescentColor = new Color(0.15f, 1.0f, 0.78f, 1.0f);

    [Tooltip("Thickness of the hollow contour when rendering GhostOutline (Shape 3).")]
    [Range(0.015f, 0.12f)]
    [SerializeField] private float outlineThickness = 0.042f;

    [Tooltip("How fast the trace lights up when hit by the UV beam.")]
    [SerializeField] private float revealSpeed = 6.5f;

    [Tooltip("How long (seconds) the fluorescent afterglow lingers after the UV beam moves away.")]
    [SerializeField] private float afterglowHoldSeconds = 0.55f;

    [Tooltip("How fast the trace fades back to invisible after the UV beam moves away.")]
    [SerializeField] private float fadeOutSpeed = 1.5f;

    [Header("Lifetime Settings")]
    [Tooltip("If true, this trace never expires (useful for handprints/clues placed manually in rooms).")]
    [SerializeField] private bool isPermanentTrace = false;

    [Tooltip("Optional variation seed for permanent scene-placed traces (for Handprint: >= 0 = Right Hand, < 0 = Left Hand).")]
    [SerializeField] private float permanentTraceSeed = 1.0f;

    [Tooltip("Seconds before a dropped trace expires and hides itself.")]
    [SerializeField] private float traceLifetimeSeconds = 25.0f;

    [Header("Shader & Material Reference")]
    [SerializeField] private Material customTraceMaterial;
    [SerializeField] private Shader uvTraceShader;

    [Header("Current State (Read Only)")]
    [SerializeField] private float currentRevealAmount = 0f;

    private Renderer m_Renderer;
    private MaterialPropertyBlock m_PropBlock;
    private Vector3 m_SurfaceNormal = Vector3.up;
    private Collider m_SurfaceCollider = null;
    private float m_TraceSeed = 0f;
    private float m_QuadAspect = 1.0f;
    private Texture m_CustomSilhouetteTex = null;
    private bool m_IsHitByUVThisFrame = false;
    private float m_TargetStrengthThisFrame = 1f;
    private float m_AfterglowTimer = 0f;
    private float m_AgeTimer = 0f;

    private static Material s_SharedUVTraceMaterial;
    private static readonly int s_UVGlowColorId = Shader.PropertyToID("_UVGlowColor");
    private static readonly int s_RevealAmountId = Shader.PropertyToID("_RevealAmount");
    private static readonly int s_TraceShapeId = Shader.PropertyToID("_TraceShape");
    private static readonly int s_TraceSeedId = Shader.PropertyToID("_TraceSeed");
    private static readonly int s_QuadAspectId = Shader.PropertyToID("_QuadAspect");
    private static readonly int s_OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
    private static readonly int s_UseSilhouetteTexId = Shader.PropertyToID("_UseSilhouetteTex");
    private static readonly int s_SilhouetteTexId = Shader.PropertyToID("_SilhouetteTex");

    public Vector3 Position => transform.position;
    public Vector3 SurfaceNormal => m_SurfaceNormal;
    public Collider SurfaceCollider => m_SurfaceCollider;
    public TraceVisualType CurrentTraceType => traceType;
    public float CurrentRevealAmount => currentRevealAmount;
    public bool IsWallMark =>
        traceType == TraceVisualType.WallSplash ||
        traceType == TraceVisualType.GhostOutline ||
        traceType == TraceVisualType.Handprint;

    public void SetTraceMaterial(Material mat)
    {
        if (mat == null)
            return;

        customTraceMaterial = mat;
        if (m_Renderer != null)
        {
            m_Renderer.sharedMaterial = customTraceMaterial;
        }
    }

    private void OnValidate()
    {
        if (uvTraceShader == null)
        {
            uvTraceShader = Shader.Find("HouseOfGhosts/UVGhostTrace");
        }
    }

    private void Awake()
    {
        m_PropBlock = new MaterialPropertyBlock();
        m_Renderer = GetComponentInChildren<Renderer>(true);
        m_QuadAspect = transform.localScale.x > 0.01f ? (transform.localScale.y / transform.localScale.x) : 1.0f;
        if (isPermanentTrace)
        {
            m_TraceSeed = permanentTraceSeed;
            if (IsWallMark)
            {
                Vector3 quadFrontNormal = -transform.forward;
                m_SurfaceNormal = quadFrontNormal.sqrMagnitude > 0.001f ? quadFrontNormal.normalized : Vector3.back;
            }
        }
        EnsureUVMaterialAssigned();
        ApplyVisuals(0f);
    }

    private void OnEnable()
    {
        if (!ActiveTraces.Contains(this))
        {
            ActiveTraces.Add(this);
        }

        m_AgeTimer = 0f;
        currentRevealAmount = 0f;
        m_IsHitByUVThisFrame = false;
        ApplyVisuals(0f);
    }

    private void OnDisable()
    {
        ActiveTraces.Remove(this);
    }

    private void Update()
    {
        if (!isPermanentTrace)
        {
            m_AgeTimer += Time.deltaTime;
            if (m_AgeTimer >= traceLifetimeSeconds)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        if (m_IsHitByUVThisFrame)
        {
            m_AfterglowTimer = afterglowHoldSeconds;
            float ageFade = isPermanentTrace ? 1.0f : Mathf.Clamp01((traceLifetimeSeconds - m_AgeTimer) / 3.0f);
            float target = m_TargetStrengthThisFrame * ageFade;
            currentRevealAmount = Mathf.MoveTowards(currentRevealAmount, target, revealSpeed * Time.deltaTime);
        }
        else
        {
            if (m_AfterglowTimer > 0f)
            {
                m_AfterglowTimer -= Time.deltaTime;
            }
            else if (currentRevealAmount > 0f)
            {
                currentRevealAmount = Mathf.MoveTowards(currentRevealAmount, 0f, fadeOutSpeed * Time.deltaTime);
            }
        }

        ApplyVisuals(currentRevealAmount);
        m_IsHitByUVThisFrame = false;
    }

    /// <summary>
    /// Fills up to 3 world-space sample points slightly offset along SurfaceNormal so tall wall silhouettes
    /// (like the 2m Ghost Outline) light up smoothly whether the player aims at the feet, chest, or head.
    /// </summary>
    public int GetUVSamplePoints(Vector3[] buffer)
    {
        if (buffer == null || buffer.Length == 0)
            return 0;

        Vector3 normalOffset = m_SurfaceNormal * 0.04f;
        Vector3 center = transform.position + normalOffset;
        buffer[0] = center;

        float height = transform.localScale.y;
        if (height >= 0.95f && buffer.Length >= 3)
        {
            Vector3 upOffset = transform.up * (height * 0.34f);
            buffer[1] = center + upOffset;
            buffer[2] = center - upOffset;
            return 3;
        }

        return 1;
    }

    /// <summary>
    /// Called by FlashlightCaptureDetector each frame the UV beam illuminates this trace.
    /// </summary>
    public void ReportUVIlluminated(float intensityFactor = 1.0f)
    {
        m_IsHitByUVThisFrame = true;
        m_TargetStrengthThisFrame = Mathf.Clamp01(intensityFactor);
    }

    /// <summary>
    /// Spawns or repositions a pooled UV trace (Footprint, WallSplash, ResidualRing, or GhostOutline).
    /// </summary>
    public void ActivateTrace(
        Vector3 worldPos,
        Quaternion worldRot,
        Vector3 surfaceNormal,
        Vector3 scale,
        float lifetime,
        TraceVisualType visualType,
        Color glowColor,
        float randomSeed = 0f,
        Texture customSilhouette = null,
        Collider surfaceCollider = null)
    {
        transform.SetPositionAndRotation(worldPos, worldRot);
        transform.localScale = scale;
        m_SurfaceNormal = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
        m_SurfaceCollider = surfaceCollider;
        m_QuadAspect = scale.x > 0.01f ? (scale.y / scale.x) : 1.0f;
        m_TraceSeed = randomSeed;
        m_CustomSilhouetteTex = customSilhouette;
        traceLifetimeSeconds = lifetime;
        traceType = visualType;
        uvFluorescentColor = glowColor;
        isPermanentTrace = false;
        m_AgeTimer = 0f;
        currentRevealAmount = 0f;
        gameObject.SetActive(true);
        ApplyVisuals(0f);
    }

    private void EnsureUVMaterialAssigned()
    {
        if (m_Renderer == null)
            return;

        if (customTraceMaterial != null)
        {
            m_Renderer.sharedMaterial = customTraceMaterial;
            m_Renderer.shadowCastingMode = ShadowCastingMode.Off;
            m_Renderer.receiveShadows = false;
            return;
        }

        if (s_SharedUVTraceMaterial == null)
        {
            if (uvTraceShader == null)
            {
                uvTraceShader = Shader.Find("HouseOfGhosts/UVGhostTrace");
            }

            if (uvTraceShader != null)
            {
                s_SharedUVTraceMaterial = new Material(uvTraceShader)
                {
                    name = "Shared_UVGhostTrace_Mat",
                    enableInstancing = true
                };
            }
        }

        if (s_SharedUVTraceMaterial != null)
        {
            m_Renderer.sharedMaterial = s_SharedUVTraceMaterial;
            m_Renderer.shadowCastingMode = ShadowCastingMode.Off;
            m_Renderer.receiveShadows = false;
        }
    }

    private void ApplyVisuals(float reveal01)
    {
        if (m_Renderer == null)
            return;

        bool shouldRender = reveal01 > 0.002f;
        if (m_Renderer.enabled != shouldRender)
        {
            m_Renderer.enabled = shouldRender;
        }

        if (!shouldRender)
            return;

        m_PropBlock.Clear();
        m_PropBlock.SetColor(s_UVGlowColorId, uvFluorescentColor);
        m_PropBlock.SetFloat(s_RevealAmountId, reveal01);
        m_PropBlock.SetFloat(s_TraceShapeId, (float)traceType);
        m_PropBlock.SetFloat(s_TraceSeedId, m_TraceSeed);
        m_PropBlock.SetFloat(s_QuadAspectId, m_QuadAspect);
        m_PropBlock.SetFloat(s_OutlineThicknessId, outlineThickness);

        bool hasCustomSilhouette = (traceType == TraceVisualType.GhostOutline && m_CustomSilhouetteTex != null);
        m_PropBlock.SetFloat(s_UseSilhouetteTexId, hasCustomSilhouette ? 1.0f : 0.0f);
        if (hasCustomSilhouette)
        {
            m_PropBlock.SetTexture(s_SilhouetteTexId, m_CustomSilhouetteTex);
        }

        m_Renderer.SetPropertyBlock(m_PropBlock);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.1f, 1.0f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, 0.12f);
        Gizmos.DrawRay(transform.position, m_SurfaceNormal * 0.2f);
    }
}
