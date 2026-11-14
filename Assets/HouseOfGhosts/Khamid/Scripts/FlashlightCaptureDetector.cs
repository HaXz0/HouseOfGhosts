using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class FlashlightCaptureDetector : MonoBehaviour
{
    [Header("Flashlight References")]
    [Tooltip("Reference to Leo's FlashlightController on this GameObject. Automatically found if left empty.")]
    [SerializeField] private FlashlightController flashlightController;

    [Tooltip("The spotlight created/managed by FlashlightController. Automatically found if left empty.")]
    [SerializeField] private Light beamLight;

    [Header("UV & Capture Rules")]
    [Tooltip("If false (recommended), only Normal Mode captures the ghost; UV Mode reveals Ghost Traces and UV Cloak Shimmer.")]
    [SerializeField] private bool allowGhostCaptureInUVMode = false;

    [Tooltip("Allows pressing the 'U' key on the PC keyboard in the Editor to toggle Leo's UV Mode for testing.")]
    [SerializeField] private bool enableKeyboardUToggle = true;

    [Tooltip("Fallback UV state used ONLY if no FlashlightController is attached to this object.")]
    [SerializeField] private bool fallbackUvMode = false;

    [Header("Detection Settings")]
    [Tooltip("Layers that can block the flashlight beam (e.g. Walls, Furniture, Default).")]
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Header("Atmosphere: Soft Volumetric Beam & Floating Dust Motes")]
    [Tooltip("If true, renders a Quest-3-optimized soft volumetric light shaft from the flashlight lens that shortens smoothly against walls.")]
    [SerializeField] private bool enableVolumetricBeam = true;

    [Tooltip("Material using HouseOfGhosts/VolumetricFlashlightBeam (automatically created from shader if left empty).")]
    [SerializeField] private Material volumetricConeMaterial;

    [Tooltip("Warm misty volumetric cone color & alpha in Normal Flashlight Mode.")]
    [SerializeField] private Color normalBeamVolumetricColor = new Color(1.0f, 0.95f, 0.84f, 0.088f);

    [Tooltip("Deep ultraviolet blacklight volumetric cone color & alpha in UV Mode.")]
    [SerializeField] private Color uvBeamVolumetricColor = new Color(0.54f, 0.18f, 1.0f, 0.130f);

    [Tooltip("Overall brightness/opacity multiplier for the volumetric light cone.")]
    [Range(0f, 3f)]
    [SerializeField] private float volumetricIntensityMultiplier = 1.0f;

    [Tooltip("Radius (meters) of the flashlight glass lens where the volumetric beam originates.")]
    [Range(0.01f, 0.08f)]
    [SerializeField] private float lensStartRadius = 0.024f;

    [Tooltip("If true, spawns subtle world-space haunted house dust motes that glimmer only while inside the active flashlight cone.")]
    [SerializeField] private bool enableFloatingDustMotes = true;

    [Tooltip("Optional VFX_FlashlightDustMotes prefab (automatically generated if left empty).")]
    [SerializeField] private ParticleSystem dustMotesPrefab;

    [Tooltip("Material using HouseOfGhosts/FlashlightDustMote for cone-masked dust specks.")]
    [SerializeField] private Material dustMotesMaterial;

    [Tooltip("Warm sunlit champagne dust mote tint in Normal Flashlight Mode.")]
    [SerializeField] private Color normalDustMoteColor = new Color(1.0f, 0.94f, 0.80f, 0.82f);

    [Tooltip("Fluorescent blacklight violet dust/spore mote tint in UV Mode.")]
    [SerializeField] private Color uvDustMoteColor = new Color(0.74f, 0.38f, 1.0f, 0.95f);

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

    private readonly Vector3[] m_AimPoints = new Vector3[3];
    private readonly Vector3[] m_TraceSamplePoints = new Vector3[3];
    private readonly RaycastHit[] m_HitBuffer = new RaycastHit[12];
    private Collider[] m_SelfColliders;

    // Volumetric Cone & Dust Motes runtime state
    private MeshRenderer m_VolumetricConeRenderer;
    private Transform m_VolumetricConeTransform;
    private MaterialPropertyBlock m_ConePropBlock;
    private ParticleSystem m_DustMotesInstance;
    private ParticleSystemRenderer m_DustMotesRenderer;
    private MaterialPropertyBlock m_DustPropBlock;
    private float m_SmoothedBeamReach = 8.0f;
    private float m_UvVisualBlend = 0f;
    private bool m_AtmosphereInitialized = false;

    private static Mesh s_SharedUnitConeMesh;
    private static Material s_FallbackConeMaterial;
    private static Material s_FallbackDustMaterial;

    private static readonly int s_BeamColorId = Shader.PropertyToID("_BeamColor");
    private static readonly int s_StartRadiusId = Shader.PropertyToID("_StartRadius");
    private static readonly int s_EndRadiusId = Shader.PropertyToID("_EndRadius");
    private static readonly int s_MeshLengthId = Shader.PropertyToID("_MeshLength");
    private static readonly int s_MaxBeamRangeId = Shader.PropertyToID("_MaxBeamRange");
    private static readonly int s_WallStopDistId = Shader.PropertyToID("_WallStopDist");

    private static readonly int s_DustColorId = Shader.PropertyToID("_DustColor");
    private static readonly int s_BeamOriginWSId = Shader.PropertyToID("_BeamOriginWS");
    private static readonly int s_BeamDirWSId = Shader.PropertyToID("_BeamDirWS");
    private static readonly int s_BeamParamsId = Shader.PropertyToID("_BeamParams");

    /// <summary>
    /// Reads UV Mode directly from Leo's FlashlightController (or fallbackUvMode if testing without it).
    /// </summary>
    public bool IsUVMode => flashlightController != null ? flashlightController.IsUvMode : fallbackUvMode;

    private void Awake()
    {
        m_SelfColliders = GetComponentsInChildren<Collider>(true);
        if (flashlightController == null)
        {
            flashlightController = GetComponent<FlashlightController>();
        }
    }

    private void Start()
    {
        // Run in Start as well because Leo's FlashlightController creates "Flashlight Beam" in Awake()
        m_SelfColliders = GetComponentsInChildren<Collider>(true);
        if (flashlightController == null)
        {
            flashlightController = GetComponent<FlashlightController>();
        }
        FindBeamLightIfNeeded();
        if (beamLight != null)
        {
            m_SmoothedBeamReach = beamLight.range;
            m_UvVisualBlend = IsUVMode ? 1f : 0f;
            EnsureAtmosphereVisualsInitialized();
        }
    }

    private void OnDisable()
    {
        SetAtmosphereVisualsActive(false);
    }

    private void Update()
    {
        if (beamLight == null)
        {
            FindBeamLightIfNeeded();
            if (beamLight == null)
                return;

            m_SmoothedBeamReach = beamLight.range;
        }

        if (!m_AtmosphereInitialized)
        {
            EnsureAtmosphereVisualsInitialized();
        }

        // Optional PC keyboard 'U' shortcut for testing in the Editor
        if (enableKeyboardUToggle && Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
        {
            ToggleUVMode();
        }

        // Check if the flashlight beam is actually turned ON
        bool isBeamOn = flashlightController != null
            ? flashlightController.IsOn
            : (beamLight.enabled && beamLight.gameObject.activeInHierarchy);

        if (!isBeamOn)
        {
            SetAtmosphereVisualsActive(false);
            return;
        }

        Vector3 beamOrigin = beamLight.transform.position;
        Vector3 beamForward = beamLight.transform.forward;
        float maxRange = beamLight.range;
        float halfSpotAngle = beamLight.spotAngle * 0.5f;

        bool isUvMode = IsUVMode;
        UpdateAtmosphereVisuals(beamOrigin, beamForward, maxRange, halfSpotAngle, isUvMode);

        // 1. Check GhostTargets (Normal Mode captures; UV Mode triggers eerie UV Cloak Shimmer!)
        for (int i = GhostTarget.ActiveTargets.Count - 1; i >= 0; i--)
        {
            GhostTarget target = GhostTarget.ActiveTargets[i];
            if (target == null || target.IsCaptured)
                continue;

            if (IsTargetIlluminated(target, beamOrigin, beamForward, maxRange, halfSpotAngle))
            {
                if (!isUvMode || allowGhostCaptureInUVMode)
                {
                    target.ReportIlluminated();
                }
                else
                {
                    target.ReportUVIlluminated();
                }
            }
        }

        // 2. In UV Mode: detect and reveal hidden GhostTraces (Footprints, Wall-Phase Rifts, Hint Rings)
        if (isUvMode)
        {
            UpdateUVTraceDetection(beamOrigin, beamForward, maxRange, halfSpotAngle);
        }
    }

    /// <summary>
    /// Toggles UV Mode through Leo's FlashlightController so his beam color and physical switch stay in sync.
    /// </summary>
    [ContextMenu("Toggle Normal / UV Mode")]
    public void ToggleUVMode()
    {
        if (flashlightController != null)
        {
            flashlightController.SetUvMode(!flashlightController.IsUvMode);
        }
        else
        {
            fallbackUvMode = !fallbackUvMode;
        }
    }

    private void UpdateUVTraceDetection(Vector3 beamOrigin, Vector3 beamForward, float maxRange, float halfSpotAngle)
    {
        for (int i = GhostTrace.ActiveTraces.Count - 1; i >= 0; i--)
        {
            GhostTrace trace = GhostTrace.ActiveTraces[i];
            if (trace == null || !trace.gameObject.activeInHierarchy)
                continue;

            // For wall splashes/outlines, ensure the flashlight is on the front side of that wall surface
            if (trace.IsWallMark && Vector3.Dot(beamOrigin - trace.Position, trace.SurfaceNormal) <= 0.005f)
                continue;

            int pointCount = trace.GetUVSamplePoints(m_TraceSamplePoints);
            float bestIntensity = 0f;

            for (int p = 0; p < pointCount; p++)
            {
                Vector3 tracePoint = m_TraceSamplePoints[p];
                Vector3 toTrace = tracePoint - beamOrigin;
                float distance = toTrace.magnitude;

                if (distance > maxRange || distance < 0.01f)
                    continue;

                Vector3 direction = toTrace / distance;
                float angle = Vector3.Angle(beamForward, direction);
                if (angle > halfSpotAngle)
                    continue;

                // Ensure no OTHER wall blocks the UV beam from reaching this point of the trace
                if (!IsBlockedByWall(beamOrigin, direction, distance - 0.04f, trace.SurfaceCollider))
                {
                    float distanceFalloff = 1.0f - Mathf.Clamp01(distance / maxRange) * 0.4f;
                    float angleFalloff = 1.0f - Mathf.Clamp01(angle / halfSpotAngle) * 0.35f;
                    float intensity = distanceFalloff * angleFalloff;
                    if (intensity > bestIntensity)
                    {
                        bestIntensity = intensity;
                    }
                }
            }

            if (bestIntensity > 0f)
            {
                trace.ReportUVIlluminated(bestIntensity);
            }
        }
    }

    private bool IsBlockedByWall(Vector3 origin, Vector3 direction, float checkDistance, Collider hostSurfaceCollider = null)
    {
        if (checkDistance <= 0.05f)
            return false;

        int hitCount = Physics.RaycastNonAlloc(origin, direction, m_HitBuffer, checkDistance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCol = m_HitBuffer[i].collider;
            if (hitCol == null || IsSelfCollider(hitCol))
                continue;

            if (hitCol.GetComponentInParent<GhostTarget>() != null)
                continue;

            // Ignore grazing contact near the end of the ray against the exact wall surface hosting this UV decal
            if (hostSurfaceCollider != null && hitCol == hostSurfaceCollider && m_HitBuffer[i].distance >= checkDistance - 0.18f)
                continue;

            return true;
        }

        return false;
    }

    private bool IsTargetIlluminated(GhostTarget target, Vector3 beamOrigin, Vector3 beamForward, float maxRange, float halfSpotAngle)
    {
        // Point-blank VR check: if the player extends their flashlight directly inside the ghost's capsule (<= 0.5m)
        if (target.IsPointInsideCollider(beamOrigin))
        {
            return true;
        }

        // Direct center-beam check so close-range (<= 1.5m) aiming anywhere on the body has zero vertical dead zones
        if (HasClearLineOfSight(beamOrigin, beamForward, maxRange, target))
        {
            return true;
        }

        target.GetAimPoints(m_AimPoints);

        for (int p = 0; p < m_AimPoints.Length; p++)
        {
            Vector3 targetPoint = m_AimPoints[p];
            Vector3 toPoint = targetPoint - beamOrigin;
            float distance = toPoint.magnitude;

            if (distance > maxRange || distance < 0.001f)
                continue;

            Vector3 direction = toPoint / distance;

            float angle = Vector3.Angle(beamForward, direction);
            if (angle > halfSpotAngle)
                continue;

            if (HasClearLineOfSight(beamOrigin, direction, distance, target))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasClearLineOfSight(Vector3 origin, Vector3 direction, float maxDistance, GhostTarget expectedTarget)
    {
        int hitCount = Physics.RaycastNonAlloc(origin, direction, m_HitBuffer, maxDistance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        if (hitCount == 0)
            return false;

        float closestDistance = float.MaxValue;
        Collider closestCollider = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCol = m_HitBuffer[i].collider;
            if (hitCol == null || IsSelfCollider(hitCol))
                continue;

            if (m_HitBuffer[i].distance < closestDistance)
            {
                closestDistance = m_HitBuffer[i].distance;
                closestCollider = hitCol;
            }
        }

        if (closestCollider == null)
            return false;

        GhostTarget hitTarget = closestCollider.GetComponentInParent<GhostTarget>();
        return hitTarget == expectedTarget;
    }

    private bool IsSelfCollider(Collider col)
    {
        if (m_SelfColliders == null)
            return false;

        for (int i = 0; i < m_SelfColliders.Length; i++)
        {
            if (m_SelfColliders[i] == col)
                return true;
        }
        return false;
    }

    private void FindBeamLightIfNeeded()
    {
        if (beamLight != null)
            return;

        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Spot)
            {
                beamLight = lights[i];
                return;
            }
        }

        if (lights.Length > 0)
        {
            beamLight = lights[0];
        }
    }

    // =========================================================================
    // ATMOSPHERE: QUEST-3 VOLUMETRIC CONE + CONE-MASKED FLOATING DUST MOTES
    // =========================================================================
    private void EnsureAtmosphereVisualsInitialized()
    {
        if (m_AtmosphereInitialized || beamLight == null)
            return;

        m_AtmosphereInitialized = true;
        m_ConePropBlock = new MaterialPropertyBlock();
        m_DustPropBlock = new MaterialPropertyBlock();

        // 1. Volumetric Cone Child on beamLight.transform
        GameObject coneObj = new GameObject("VolumetricBeamCone_Runtime");
        m_VolumetricConeTransform = coneObj.transform;
        m_VolumetricConeTransform.SetParent(beamLight.transform, false);
        m_VolumetricConeTransform.localPosition = Vector3.zero;
        m_VolumetricConeTransform.localRotation = Quaternion.identity;
        m_VolumetricConeTransform.localScale = Vector3.one;

        MeshFilter mf = coneObj.AddComponent<MeshFilter>();
        mf.sharedMesh = GetOrCreateSharedUnitConeMesh();

        m_VolumetricConeRenderer = coneObj.AddComponent<MeshRenderer>();
        m_VolumetricConeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        m_VolumetricConeRenderer.receiveShadows = false;
        m_VolumetricConeRenderer.lightProbeUsage = LightProbeUsage.Off;
        m_VolumetricConeRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        Material activeConeMat = volumetricConeMaterial;
        if (activeConeMat == null)
        {
            if (s_FallbackConeMaterial == null)
            {
                Shader coneShader = Shader.Find("HouseOfGhosts/VolumetricFlashlightBeam");
                if (coneShader != null)
                {
                    s_FallbackConeMaterial = new Material(coneShader)
                    {
                        name = "Shared_VolumetricFlashlightBeam_Mat",
                        enableInstancing = true
                    };
                }
            }
            activeConeMat = s_FallbackConeMaterial;
        }

        if (activeConeMat != null)
        {
            m_VolumetricConeRenderer.sharedMaterial = activeConeMat;
        }
        m_VolumetricConeRenderer.enabled = false;

        // 2. Floating Haunted Room Dust Motes (World-Space Particle Volume)
        if (dustMotesPrefab != null)
        {
            m_DustMotesInstance = Instantiate(dustMotesPrefab, beamLight.transform, false);
            m_DustMotesInstance.name = "VFX_FlashlightDustMotes_Runtime";
        }
        else
        {
            m_DustMotesInstance = CreateFallbackDustMotesSystem(beamLight.transform);
        }

        if (m_DustMotesInstance != null)
        {
            m_DustMotesRenderer = m_DustMotesInstance.GetComponent<ParticleSystemRenderer>();
            if (m_DustMotesRenderer != null)
            {
                Material activeDustMat = dustMotesMaterial != null ? dustMotesMaterial : m_DustMotesRenderer.sharedMaterial;
                if (activeDustMat == null)
                {
                    if (s_FallbackDustMaterial == null)
                    {
                        Shader dustShader = Shader.Find("HouseOfGhosts/FlashlightDustMote");
                        if (dustShader != null)
                        {
                            s_FallbackDustMaterial = new Material(dustShader)
                            {
                                name = "Shared_FlashlightDustMotes_Mat",
                                enableInstancing = true
                            };
                        }
                    }
                    activeDustMat = s_FallbackDustMaterial;
                }

                if (activeDustMat != null)
                {
                    m_DustMotesRenderer.sharedMaterial = activeDustMat;
                }

                ConfigureDustVertexStreams(m_DustMotesRenderer);
                m_DustMotesRenderer.shadowCastingMode = ShadowCastingMode.Off;
                m_DustMotesRenderer.receiveShadows = false;
                m_DustMotesRenderer.enabled = false;
            }

            m_DustMotesInstance.Play(true);
            m_DustMotesInstance.Pause(true);
        }
    }

    private void SetAtmosphereVisualsActive(bool active)
    {
        if (m_VolumetricConeRenderer != null && m_VolumetricConeRenderer.enabled != (active && enableVolumetricBeam))
        {
            m_VolumetricConeRenderer.enabled = active && enableVolumetricBeam;
        }

        bool shouldShowDust = active && enableFloatingDustMotes;
        if (m_DustMotesRenderer != null && m_DustMotesRenderer.enabled != shouldShowDust)
        {
            m_DustMotesRenderer.enabled = shouldShowDust;
            if (m_DustMotesInstance != null)
            {
                if (shouldShowDust)
                    m_DustMotesInstance.Play(true);
                else
                    m_DustMotesInstance.Pause(true);
            }
        }
    }

    private void UpdateAtmosphereVisuals(Vector3 beamOrigin, Vector3 beamForward, float maxRange, float halfSpotAngle, bool isUvMode)
    {
        if (!enableVolumetricBeam && !enableFloatingDustMotes)
        {
            SetAtmosphereVisualsActive(false);
            return;
        }

        SetAtmosphereVisualsActive(true);

        // 1. Dynamic Wall-Stop Distance (shortens fast against walls so beam never pokes through walls!)
        float targetReach = ComputeBeamObstacleDistance(beamOrigin, beamForward, maxRange);
        float reachSpeed = targetReach < m_SmoothedBeamReach ? 30.0f : 16.0f;
        m_SmoothedBeamReach = Mathf.Lerp(m_SmoothedBeamReach, targetReach, Mathf.Clamp01(reachSpeed * Time.deltaTime));

        // 2. Smooth Normal <-> UV Mode color blend
        float targetUvBlend = isUvMode ? 1.0f : 0.0f;
        m_UvVisualBlend = Mathf.MoveTowards(m_UvVisualBlend, targetUvBlend, 12.0f * Time.deltaTime);

        // 3. Push Volumetric Cone PropertyBlock
        if (enableVolumetricBeam && m_VolumetricConeRenderer != null)
        {
            float activeMeshLen = Mathf.Clamp(m_SmoothedBeamReach + 0.16f, 0.15f, maxRange);
            float tanHalfAngle = Mathf.Tan(halfSpotAngle * Mathf.Deg2Rad);
            float endRadius = lensStartRadius + activeMeshLen * tanHalfAngle;

            Color blendedCone = Color.Lerp(normalBeamVolumetricColor, uvBeamVolumetricColor, m_UvVisualBlend);
            Color finalConeColor = new Color(
                blendedCone.r * Mathf.Max(0.5f, volumetricIntensityMultiplier),
                blendedCone.g * Mathf.Max(0.5f, volumetricIntensityMultiplier),
                blendedCone.b * Mathf.Max(0.5f, volumetricIntensityMultiplier),
                Mathf.Clamp01(blendedCone.a * volumetricIntensityMultiplier)
            );

            m_ConePropBlock.Clear();
            m_ConePropBlock.SetColor(s_BeamColorId, finalConeColor);
            m_ConePropBlock.SetFloat(s_StartRadiusId, lensStartRadius);
            m_ConePropBlock.SetFloat(s_EndRadiusId, endRadius);
            m_ConePropBlock.SetFloat(s_MeshLengthId, activeMeshLen);
            m_ConePropBlock.SetFloat(s_MaxBeamRangeId, maxRange);
            m_ConePropBlock.SetFloat(s_WallStopDistId, m_SmoothedBeamReach);
            m_VolumetricConeRenderer.SetPropertyBlock(m_ConePropBlock);
        }

        // 4. Push Cone-Masked Dust Motes PropertyBlock & Keep Spawn Volume Centered Ahead of Beam
        if (enableFloatingDustMotes && m_DustMotesRenderer != null && m_DustMotesInstance != null)
        {
            float boxCenterZ = Mathf.Clamp(m_SmoothedBeamReach * 0.48f, 0.55f, 2.15f);
            m_DustMotesInstance.transform.localPosition = new Vector3(0f, 0f, boxCenterZ);
            m_DustMotesInstance.transform.localRotation = Quaternion.identity;

            float outerDeg = Mathf.Min(85f, halfSpotAngle * 1.04f);
            float innerDeg = Mathf.Max(2f, halfSpotAngle * 0.68f);
            float cosOuter = Mathf.Cos(outerDeg * Mathf.Deg2Rad);
            float cosInner = Mathf.Cos(innerDeg * Mathf.Deg2Rad);

            Color blendedDust = Color.Lerp(normalDustMoteColor, uvDustMoteColor, m_UvVisualBlend);

            m_DustPropBlock.Clear();
            m_DustPropBlock.SetColor(s_DustColorId, blendedDust);
            m_DustPropBlock.SetVector(s_BeamOriginWSId, new Vector4(beamOrigin.x, beamOrigin.y, beamOrigin.z, 1.0f));
            m_DustPropBlock.SetVector(s_BeamDirWSId, new Vector4(beamForward.x, beamForward.y, beamForward.z, 0.0f));
            m_DustPropBlock.SetVector(s_BeamParamsId, new Vector4(cosOuter, cosInner, m_SmoothedBeamReach, maxRange));
            m_DustMotesRenderer.SetPropertyBlock(m_DustPropBlock);
        }
    }

    private float ComputeBeamObstacleDistance(Vector3 origin, Vector3 direction, float maxDistance)
    {
        int hitCount = Physics.SphereCastNonAlloc(origin, 0.045f, direction, m_HitBuffer, maxDistance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        if (hitCount == 0)
            return maxDistance;

        float closestDist = maxDistance;
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = m_HitBuffer[i].collider;
            if (col == null || IsSelfCollider(col))
                continue;

            if (m_HitBuffer[i].distance > 0.02f && m_HitBuffer[i].distance < closestDist)
            {
                closestDist = m_HitBuffer[i].distance;
            }
        }

        return closestDist;
    }

    private static void ConfigureDustVertexStreams(ParticleSystemRenderer psr)
    {
        if (psr == null)
            return;

        List<ParticleSystemVertexStream> streams = new List<ParticleSystemVertexStream>(5)
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.StableRandomXY,
            ParticleSystemVertexStream.Center
        };
        psr.SetActiveVertexStreams(streams);
    }

    private static ParticleSystem CreateFallbackDustMotesSystem(Transform parent)
    {
        GameObject dustObj = new GameObject("VFX_FlashlightDustMotes_Runtime");
        dustObj.transform.SetParent(parent, false);
        dustObj.transform.localPosition = new Vector3(0f, 0f, 1.85f);

        ParticleSystem ps = dustObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 5.0f;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 5.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.055f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.009f, 0.024f);
        main.gravityModifier = -0.004f;

        var emission = ps.emission;
        emission.rateOverTime = 26.0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.25f, 1.25f, 3.2f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.055f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.12f;
        noise.damping = true;

        var colLifetime = ps.colorOverLifetime;
        colLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.20f),
                new GradientAlphaKey(0.95f, 0.80f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        return ps;
    }

    private static Mesh GetOrCreateSharedUnitConeMesh()
    {
        if (s_SharedUnitConeMesh != null)
            return s_SharedUnitConeMesh;

        const int radialSegments = 32;
        const int axialRings = 9; // 8 subdivisions along beam length for smooth perspective Fresnel

        int vertCount = (radialSegments + 1) * axialRings;
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[radialSegments * (axialRings - 1) * 6];

        int vIdx = 0;
        for (int r = 0; r < axialRings; r++)
        {
            float t = (float)r / (axialRings - 1);
            for (int s = 0; s <= radialSegments; s++)
            {
                float u = (float)s / radialSegments;
                float angle = u * Mathf.PI * 2.0f;
                // Store unit circle direction in XY; vertex shader expands radius & Z analytically!
                vertices[vIdx] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), t);
                uvs[vIdx] = new Vector2(u, t);
                vIdx++;
            }
        }

        int tIdx = 0;
        int stride = radialSegments + 1;
        for (int r = 0; r < axialRings - 1; r++)
        {
            for (int s = 0; s < radialSegments; s++)
            {
                int i00 = r * stride + s;
                int i10 = i00 + 1;
                int i01 = (r + 1) * stride + s;
                int i11 = i01 + 1;

                // Clockwise outward-facing winding in Unity's left-handed coordinate system (+Z = beam forward)
                triangles[tIdx++] = i00;
                triangles[tIdx++] = i10;
                triangles[tIdx++] = i01;

                triangles[tIdx++] = i10;
                triangles[tIdx++] = i11;
                triangles[tIdx++] = i01;
            }
        }

        s_SharedUnitConeMesh = new Mesh
        {
            name = "Procedural_FlashlightUnitConeMesh"
        };
        s_SharedUnitConeMesh.vertices = vertices;
        s_SharedUnitConeMesh.uv = uvs;
        s_SharedUnitConeMesh.triangles = triangles;
        // Generous bounds so frustum culling never clips the 8m analytical cone
        s_SharedUnitConeMesh.bounds = new Bounds(new Vector3(0f, 0f, 4.5f), new Vector3(9.0f, 9.0f, 11.0f));
        return s_SharedUnitConeMesh;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos || beamLight == null || !beamLight.enabled)
            return;

        for (int i = 0; i < GhostTarget.ActiveTargets.Count; i++)
        {
            GhostTarget target = GhostTarget.ActiveTargets[i];
            if (target == null || target.IsCaptured)
                continue;

            Gizmos.color = (target.IsIlluminated || target.IsUVIlluminated)
                ? Color.green
                : (IsUVMode ? new Color(0.55f, 0.2f, 1f) : Color.red);
            Gizmos.DrawLine(beamLight.transform.position, target.TargetCenter);
        }
    }
}
