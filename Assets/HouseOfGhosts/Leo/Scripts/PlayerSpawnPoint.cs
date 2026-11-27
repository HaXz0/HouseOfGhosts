using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

/// <summary>
/// Places the player on this point when the scene starts, facing its blue (forward) arrow.
/// Also catches the player when they fall off the map and puts them back here.
/// Put one in every scene that has an XR Origin.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("Optional. When empty, the scene's XR Origin is found once at start.")]
    [SerializeField] XROrigin origin;
    [Tooltip("The player is put back here when they fall this many meters below the spawn point.")]
    [SerializeField, Min(1f)] float fallDistance = 5f;
    [Tooltip("Seconds to wait for the headset to start tracking before the final placement.")]
    [SerializeField, Min(0f)] float trackingWait = 1f;

    // Layer 2 (Ignore Raycast) holds the player's own colliders, such as the physics hands.
    const int k_FloorMask = ~(1 << 2);
    const float k_FloorSearchHeight = 1f;
    const float k_FloorSearchDistance = 3f;

    CharacterController m_Body;
    GravityProvider m_Gravity;

    void Start()
    {
        if (origin == null)
            origin = FindFirstObjectByType<XROrigin>();
        if (origin == null || origin.Camera == null)
        {
            Debug.LogError("PlayerSpawnPoint could not find the XR Origin and its camera.", this);
            enabled = false;
            return;
        }

        m_Body = origin.GetComponent<CharacterController>();
        m_Gravity = origin.GetComponentInChildren<GravityProvider>(true);
        Spawn();
        StartCoroutine(SpawnAfterTrackingStarts());
    }

    void Update()
    {
        if (origin.transform.position.y < transform.position.y - fallDistance)
            Spawn();
    }

    // The headset reports where the player stands in their room only after a few frames, so the player
    // is placed again once that is known. Otherwise they could start beside the spawn point instead of on it.
    IEnumerator SpawnAfterTrackingStarts()
    {
        var camera = origin.Camera.transform;
        var startLocal = camera.localPosition;
        var waited = 0f;
        while (waited < trackingWait && camera.localPosition == startLocal)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        // One more frame so the tracked pose has settled.
        yield return null;
        Spawn();
    }

    /// <summary>
    /// Puts the player's feet on the spawn point, facing its forward direction.
    /// </summary>
    public void Spawn()
    {
        var bodyWasEnabled = m_Body != null && m_Body.enabled;
        // A CharacterController keeps its own position while enabled and would undo the move.
        if (bodyWasEnabled)
            m_Body.enabled = false;

        var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.0001f)
            origin.MatchOriginUpCameraForward(Vector3.up, forward.normalized);

        // Move the origin so the point under the headset, not the origin's own pivot, lands on the floor point.
        var originTransform = origin.transform;
        var headOffset = origin.Camera.transform.position - originTransform.position;
        headOffset.y = 0f;
        originTransform.position = FindFloor() - headOffset;

        if (bodyWasEnabled)
            m_Body.enabled = true;
        if (m_Gravity != null)
            m_Gravity.ResetFallForce();
        Physics.SyncTransforms();
    }

    Vector3 FindFloor()
    {
        var start = transform.position + Vector3.up * k_FloorSearchHeight;
        return Physics.Raycast(start, Vector3.down, out var hit, k_FloorSearchHeight + k_FloorSearchDistance, k_FloorMask, QueryTriggerInteraction.Ignore)
            ? hit.point
            : transform.position;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.3f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.05f, transform.position + Vector3.up * 0.05f + transform.forward * 0.6f);
    }
}
