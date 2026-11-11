using UnityEngine;

[DisallowMultipleComponent]
public class GhostHidingSpot : MonoBehaviour
{
    [SerializeField] private string spotName = "Hiding Spot";

    public string SpotName => spotName;
    public Vector3 Position => transform.position;

    private void OnDrawGizmos()
    {
        // Visual indicator in Scene view: yellow sphere at the hiding spot
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
    }
}