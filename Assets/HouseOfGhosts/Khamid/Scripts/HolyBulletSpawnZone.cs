using UnityEngine;

[DisallowMultipleComponent]
public class HolyBulletSpawnZone : MonoBehaviour
{
    public enum ZoneShape
    {
        Circle = 0,
        Box = 1
    }

    [Header("Area Identity & Room Unlocking")]
    [Tooltip("Friendly name for this spawn zone (e.g. LivingRoom_Floor, Table_Top, Attic_Corner).")]
    [SerializeField] private string areaName = "Spawn Zone";

    [Tooltip("Set FALSE for Attic zones so bullets never spawn in the locked Attic until Capture #3 unlocks it!")]
    [SerializeField] private bool startsUnlocked = true;

    [Header("Spawn Area Geometry")]
    [Tooltip("Circle is ideal for rooms/open areas; Box is ideal for rectangular tables, shelves, or hallways.")]
    [SerializeField] private ZoneShape zoneShape = ZoneShape.Circle;

    [Tooltip("Radius (meters) for Circle shape. Use ~0.35m for small tables (centimeter variation) or ~2.5-4.0m for room floors (meter variation).")]
    [Range(0.15f, 12.0f)]
    [SerializeField] private float spawnRadius = 2.8f;

    [Tooltip("Half-extents (X width, Z length in meters) for Box shape.")]
    [SerializeField] private Vector2 boxHalfExtentsXZ = new Vector2(2.2f, 2.2f);

    [Header("Surface Snap")]
    [Tooltip("How far above this zone's Y to start the downward floor/tabletop raycast.")]
    [SerializeField] private float raycastStartHeight = 1.2f;

    [Tooltip("Maximum downward distance (meters) to search for a floor or tabletop surface.")]
    [SerializeField] private float raycastMaxDistance = 3.2f;

    private bool m_IsUnlocked;

    public string AreaName => string.IsNullOrEmpty(areaName) ? gameObject.name : areaName;
    public bool IsUnlocked => Application.isPlaying ? m_IsUnlocked : startsUnlocked;
    public ZoneShape Shape => zoneShape;
    public float SpawnRadius => spawnRadius;
    public Vector2 BoxHalfExtentsXZ => boxHalfExtentsXZ;
    public float RaycastStartHeight => raycastStartHeight;
    public float RaycastMaxDistance => raycastMaxDistance;

    private void Awake()
    {
        m_IsUnlocked = startsUnlocked;
    }

    public void UnlockZone()
    {
        m_IsUnlocked = true;
    }

    public void LockZone()
    {
        m_IsUnlocked = false;
    }

    /// <summary>
    /// Samples a continuous random world-space XZ point inside this zone's Circle or Box footprint.
    /// Guarantees bullets vary by centimeters or meters instead of repeating a fixed point.
    /// </summary>
    public Vector3 SampleRandomWorldPoint()
    {
        if (zoneShape == ZoneShape.Box)
        {
            float lx = Random.Range(-boxHalfExtentsXZ.x, boxHalfExtentsXZ.x);
            float lz = Random.Range(-boxHalfExtentsXZ.y, boxHalfExtentsXZ.y);
            return transform.TransformPoint(new Vector3(lx, 0f, lz));
        }

        // Uniform area distribution across the circle
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt(Random.value) * spawnRadius;
        Vector3 localOffset = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
        return transform.position + transform.rotation * localOffset;
    }

    private void OnDrawGizmos()
    {
        DrawZoneGizmo(selected: false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawZoneGizmo(selected: true);
    }

    private void DrawZoneGizmo(bool selected)
    {
        bool unlocked = IsUnlocked;
        Color baseColor = unlocked
            ? new Color(1.0f, 0.82f, 0.18f, selected ? 0.95f : 0.40f)
            : new Color(0.65f, 0.45f, 0.95f, selected ? 0.90f : 0.30f);

        Gizmos.color = baseColor;
        Gizmos.DrawWireSphere(transform.position, 0.12f);

        if (zoneShape == ZoneShape.Box)
        {
            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxHalfExtentsXZ.x * 2f, 0.06f, boxHalfExtentsXZ.y * 2f));
            Gizmos.matrix = prevMatrix;
        }
        else
        {
            const int segments = 28;
            Vector3 prev = transform.position + transform.rotation * new Vector3(spawnRadius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float rad = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = transform.position + transform.rotation * new Vector3(Mathf.Cos(rad) * spawnRadius, 0f, Mathf.Sin(rad) * spawnRadius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
