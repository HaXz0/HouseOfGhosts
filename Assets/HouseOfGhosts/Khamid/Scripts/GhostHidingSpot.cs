using UnityEngine;

[DisallowMultipleComponent]
public class GhostHidingSpot : MonoBehaviour
{
    [SerializeField] private string spotName = "Hiding Spot";

    [Tooltip("Optional manual corner/doorway point where the ghost peeks out when Lurking from this spot. Leave empty for automatic wall-edge detection!")]
    [SerializeField] private Transform customPeekPoint;

    public string SpotName => spotName;
    public Vector3 Position => transform.position;
    public Transform CustomPeekPoint => customPeekPoint;

    private void OnDrawGizmos()
    {
        // Visual indicator in Scene view: yellow sphere at the hiding spot
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);

        if (customPeekPoint != null)
        {
            Gizmos.color = new Color(0.16f, 0.72f, 1.0f, 0.9f);
            Gizmos.DrawLine(transform.position, customPeekPoint.position);
            Gizmos.DrawWireSphere(customPeekPoint.position, 0.25f);
        }
    }
}