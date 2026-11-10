using UnityEngine;

[DisallowMultipleComponent]
public class FlashlightCaptureDetector : MonoBehaviour
{
    [Header("Light Source")]
    [Tooltip("The spotlight used to illuminate the ghost. If left empty, it will automatically find the child beam.")]
    [SerializeField] private Light beamLight;

    [Header("Detection Settings")]
    [Tooltip("Layers that block the flashlight beam (e.g. Default, Furniture, Walls). Make sure Ghost layer is included or hit.")]
    [SerializeField] private LayerMask lineOfSightMask = ~0; // Default to Everything

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

    private bool m_IsIlluminatingGhost = false;

    private void Awake()
    {
        FindBeamLightIfNeeded();
    }

    private void Start()
    {
        // Second check in case child beam was generated at runtime by FlashlightController
        FindBeamLightIfNeeded();
    }

    private void Update()
    {
        m_IsIlluminatingGhost = false;

        if (beamLight == null)
        {
            FindBeamLightIfNeeded();
            if (beamLight == null)
                return;
        }

        // Only detect if the light is actively turned on
        if (!beamLight.enabled || !beamLight.gameObject.activeInHierarchy)
            return;

        var ghost = GhostTarget.Instance;
        if (ghost == null || ghost.IsCaptured)
            return;

        Vector3 beamOrigin = beamLight.transform.position;
        Vector3 beamForward = beamLight.transform.forward;
        Vector3 targetPoint = ghost.TargetCenter;

        Vector3 toTarget = targetPoint - beamOrigin;
        float distance = toTarget.magnitude;

        // Check 1: Distance limit
        if (distance > beamLight.range)
            return;

        Vector3 directionToTarget = toTarget / distance;

        // Check 2: Spotlight cone angle
        float angle = Vector3.Angle(beamForward, directionToTarget);
        float halfSpotAngle = beamLight.spotAngle * 0.5f;

        if (angle <= halfSpotAngle)
        {
            // Check 3: Line of Sight Raycast (ensure no walls/furniture block the beam)
            if (Physics.Raycast(beamOrigin, directionToTarget, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                var hitGhost = hit.collider.GetComponentInParent<GhostTarget>();
                if (hitGhost == ghost)
                {
                    ghost.ReportIlluminated();
                    m_IsIlluminatingGhost = true;
                }
            }
        }
    }

    private void FindBeamLightIfNeeded()
    {
        if (beamLight != null)
            return;

        // Try to find the child Light created by FlashlightController
        beamLight = GetComponentInChildren<Light>(true);
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos || beamLight == null)
            return;

        var ghost = GhostTarget.Instance;
        if (ghost == null)
            return;

        // Visual debug line in Scene view: Green when illuminating the ghost, Red when blocked or out of angle
        Gizmos.color = m_IsIlluminatingGhost ? Color.green : Color.red;
        Gizmos.DrawLine(beamLight.transform.position, ghost.TargetCenter);
    }
}