using UnityEngine;

// Invisible belt that stays at the player's hips. Place it as a direct child of the XR Origin,
// so walking, teleporting and snap turning carry it along without any physics.
// Runs before the XR Interaction Manager (-105) so held belt items use this frame's hip pose.
[DefaultExecutionOrder(-110)]
[DisallowMultipleComponent]
public sealed class PlayerBelt : MonoBehaviour
{
    [SerializeField] Transform head;

    [Tooltip("Hip height as a fraction of the head height above the floor.")]
    [SerializeField, Range(0.4f, 0.8f)] float hipHeightRatio = 0.6f;
    [SerializeField] float minimumHipHeight = 0.35f;

    [Tooltip("Offset from the eyes to the base of the neck, in head space. Keeps the belt still when you look down.")]
    [SerializeField] Vector3 neckOffset = new Vector3(0f, -0.1f, -0.08f);

    [Tooltip("The head can turn this many degrees before the belt starts turning with it.")]
    [SerializeField, Range(0f, 90f)] float turnDeadZone = 45f;
    [SerializeField] float turnSharpness = 10f;

    float m_BodyYaw;
    bool m_HasYaw;

    void OnEnable()
    {
        m_HasYaw = false;
        Application.onBeforeRender += OnBeforeRender;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    void Update()
    {
        UpdatePose(Time.deltaTime);
    }

    void OnBeforeRender()
    {
        // Re-place with the latest head pose; no extra turning so the smoothing stays frame-rate independent.
        UpdatePose(0f);
    }

    void UpdatePose(float deltaTime)
    {
        var origin = transform.parent;
        if (head == null || origin == null)
            return;

        var headPosition = origin.InverseTransformPoint(head.position);
        var headRotation = Quaternion.Inverse(origin.rotation) * head.rotation;
        var neck = headPosition + headRotation * neckOffset;
        var hipHeight = Mathf.Max(headPosition.y * hipHeightRatio, minimumHipHeight);

        var headYaw = GetHeadYaw(headRotation);
        if (!m_HasYaw)
        {
            m_BodyYaw = headYaw;
            m_HasYaw = true;
        }
        else if (deltaTime > 0f)
        {
            var difference = Mathf.DeltaAngle(m_BodyYaw, headYaw);
            var targetYaw = headYaw - Mathf.Clamp(difference, -turnDeadZone, turnDeadZone);
            var blend = 1f - Mathf.Exp(-turnSharpness * deltaTime);
            m_BodyYaw = Mathf.LerpAngle(m_BodyYaw, targetYaw, blend);
        }

        transform.localPosition = new Vector3(neck.x, hipHeight, neck.z);
        transform.localRotation = Quaternion.Euler(0f, m_BodyYaw, 0f);
    }

    static float GetHeadYaw(Quaternion headRotation)
    {
        var forward = headRotation * Vector3.forward;
        var flat = new Vector3(forward.x, 0f, forward.z);

        // Looking almost straight down or up: the head's up vector still points where the body faces.
        if (flat.sqrMagnitude < 0.04f)
        {
            var up = headRotation * Vector3.up;
            flat = forward.y < 0f ? new Vector3(up.x, 0f, up.z) : new Vector3(-up.x, 0f, -up.z);
        }

        return Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
    }
}
