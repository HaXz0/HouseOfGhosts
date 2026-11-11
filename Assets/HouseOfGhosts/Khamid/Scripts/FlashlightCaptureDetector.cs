using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

    private readonly Vector3[] m_AimPoints = new Vector3[3];
    private readonly Vector3[] m_TraceSamplePoints = new Vector3[3];
    private readonly RaycastHit[] m_HitBuffer = new RaycastHit[12];
    private Collider[] m_SelfColliders;

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
    }

    private void Update()
    {
        if (beamLight == null)
        {
            FindBeamLightIfNeeded();
            if (beamLight == null)
                return;
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
            return;

        Vector3 beamOrigin = beamLight.transform.position;
        Vector3 beamForward = beamLight.transform.forward;
        float maxRange = beamLight.range;
        float halfSpotAngle = beamLight.spotAngle * 0.5f;

        bool isUvMode = IsUVMode;

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
