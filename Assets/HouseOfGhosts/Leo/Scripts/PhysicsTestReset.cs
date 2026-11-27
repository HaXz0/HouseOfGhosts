using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Puts every loose object below this GameObject back where it started. Playtesters press the reset button,
/// and anything that falls off the floor comes back by itself.
/// </summary>
[DisallowMultipleComponent]
public sealed class PhysicsTestReset : MonoBehaviour
{
    [Tooltip("Pressing or grabbing this button resets all objects.")]
    [SerializeField] XRSimpleInteractable resetButton;
    [Tooltip("Objects that fall below this height are put back automatically.")]
    [SerializeField] float fallHeight = -3f;
    [Tooltip("Seconds between two checks for fallen objects.")]
    [SerializeField, Min(0.5f)] float fallCheckInterval = 2f;

    struct StartPose
    {
        public Rigidbody body;
        public XRGrabInteractable grab;
        public Vector3 position;
        public Quaternion rotation;
    }

    StartPose[] m_StartPoses;

    public void Configure(XRSimpleInteractable button)
    {
        resetButton = button;
    }

    void Awake()
    {
        var bodies = GetComponentsInChildren<Rigidbody>(true);
        m_StartPoses = new StartPose[bodies.Length];
        for (var index = 0; index < bodies.Length; index++)
        {
            var body = bodies[index];
            m_StartPoses[index] = new StartPose
            {
                body = body,
                grab = body.GetComponent<XRGrabInteractable>(),
                position = body.transform.position,
                rotation = body.transform.rotation,
            };
        }
    }

    void OnEnable()
    {
        if (resetButton != null)
            resetButton.selectEntered.AddListener(OnResetPressed);
        InvokeRepeating(nameof(ResetFallenObjects), fallCheckInterval, fallCheckInterval);
    }

    void OnDisable()
    {
        if (resetButton != null)
            resetButton.selectEntered.RemoveListener(OnResetPressed);
        CancelInvoke(nameof(ResetFallenObjects));
    }

    void OnResetPressed(SelectEnterEventArgs args)
    {
        ResetAll();
    }

    public void ResetAll()
    {
        for (var index = 0; index < m_StartPoses.Length; index++)
            Restore(m_StartPoses[index]);
    }

    void ResetFallenObjects()
    {
        for (var index = 0; index < m_StartPoses.Length; index++)
        {
            var pose = m_StartPoses[index];
            if (pose.body != null && pose.body.position.y < fallHeight)
                Restore(pose);
        }
    }

    static void Restore(StartPose pose)
    {
        // Objects in a hand or in a belt slot stay where they are.
        if (pose.body == null || (pose.grab != null && pose.grab.isSelected))
            return;

        pose.body.transform.SetPositionAndRotation(pose.position, pose.rotation);
        if (!pose.body.isKinematic)
        {
            pose.body.linearVelocity = Vector3.zero;
            pose.body.angularVelocity = Vector3.zero;
        }
    }
}
