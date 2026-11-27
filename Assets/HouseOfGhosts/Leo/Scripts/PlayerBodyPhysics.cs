using UnityEngine;

/// <summary>
/// Lets the player's body push loose rigidbodies while walking. A CharacterController stops against them by
/// default. Must sit on the same GameObject as the CharacterController; <see cref="PhysicsPlayerRig"/> adds it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerBodyPhysics : MonoBehaviour
{
    [Tooltip("Push strength in newtons.")]
    [SerializeField, Min(0f)] float pushForce = 40f;

    public float PushForce
    {
        get => pushForce;
        set => pushForce = Mathf.Max(0f, value);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var body = hit.rigidbody;
        if (body == null || body.isKinematic)
            return;

        // Standing on top of something is not pushing it.
        if (hit.moveDirection.y < -0.3f)
            return;

        var direction = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (direction.sqrMagnitude < 0.0001f)
            return;

        // This runs once per rendered frame, so scale by the frame time to keep the push frame-rate independent.
        body.AddForceAtPosition(direction.normalized * (pushForce * Time.deltaTime), hit.point, ForceMode.Impulse);
    }
}
