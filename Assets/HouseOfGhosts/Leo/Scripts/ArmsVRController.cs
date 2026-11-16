using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(RigBuilder))]
public sealed class ArmsVRController : MonoBehaviour
{
    [SerializeField] Transform head;
    [SerializeField] Transform leftController;
    [SerializeField] Transform rightController;
    [SerializeField] Transform leftShoulder;
    [SerializeField] Transform rightShoulder;
    [SerializeField] Transform leftHand;
    [SerializeField] Transform rightHand;
    [SerializeField] Transform leftTarget;
    [SerializeField] Transform rightTarget;
    [SerializeField] Transform leftHint;
    [SerializeField] Transform rightHint;
    [SerializeField] TwoBoneIKConstraint leftConstraint;
    [SerializeField] TwoBoneIKConstraint rightConstraint;
    [SerializeField] InputActionAsset actions;
    [SerializeField] AnimationClip leftGripClip;
    [SerializeField] AnimationClip rightGripClip;
    [SerializeField] Vector3 shoulderOffset = new Vector3(0f, -0.25f, -0.06f);
    [SerializeField] Vector3 elbowHintOffset = new Vector3(0.18f, -0.25f, -0.12f);
    [SerializeField] Vector3 leftWristPositionOffset;
    [SerializeField] Vector3 rightWristPositionOffset;
    [SerializeField] Vector3 leftWristRotationOffset;
    [SerializeField] Vector3 rightWristRotationOffset;

    Animator animator;
    RigBuilder rigBuilder;
    PlayableGraph graph;
    AnimationClipPlayable leftGripPlayable;
    AnimationClipPlayable rightGripPlayable;
    AvatarMask leftMask;
    AvatarMask rightMask;
    InputAction leftGrip;
    InputAction rightGrip;
    InputAction leftTracked;
    InputAction rightTracked;
    Vector3 shoulderCenterInModel;
    Pose leftPose;
    Pose rightPose;
    bool hasLeftPose;
    bool hasRightPose;
    Quaternion bodyRotation;

    void OnEnable()
    {
        animator = GetComponent<Animator>();
        rigBuilder = GetComponent<RigBuilder>();
        if (head == null || leftController == null || rightController == null || leftShoulder == null ||
            rightShoulder == null || leftHand == null || rightHand == null || leftTarget == null ||
            rightTarget == null || leftHint == null || rightHint == null || leftConstraint == null ||
            rightConstraint == null || actions == null || leftGripClip == null || rightGripClip == null)
        {
            Debug.LogError("ArmsVR is missing rig, controller, input, or animation references.", this);
            UseFallbackHands();
            return;
        }

        leftGrip = actions.FindAction("XRI Left Interaction/Select Value");
        rightGrip = actions.FindAction("XRI Right Interaction/Select Value");
        leftTracked = actions.FindAction("XRI Left/Is Tracked");
        rightTracked = actions.FindAction("XRI Right/Is Tracked");
        if (leftGrip == null || rightGrip == null || leftTracked == null || rightTracked == null)
        {
            Debug.LogError("ArmsVR could not find the existing grip or tracking actions.", this);
            UseFallbackHands();
            return;
        }

        shoulderCenterInModel = transform.InverseTransformPoint((leftShoulder.position + rightShoulder.position) * 0.5f);
        bodyRotation = transform.rotation;
        leftConstraint.weight = 0f;
        rightConstraint.weight = 0f;
        try
        {
            if (!BuildGraph())
                UseFallbackHands();
        }
        catch (Exception exception)
        {
            Debug.LogError("ArmsVR could not initialize its animation graph: " + exception.Message, this);
            UseFallbackHands();
        }
    }

    bool BuildGraph()
    {
        rigBuilder.enabled = false;
        graph = PlayableGraph.Create("ArmsVR Animation and IK");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var layers = AnimationLayerMixerPlayable.Create(graph, 3);
        var rest = AnimationClipPlayable.Create(graph, leftGripClip);
        leftGripPlayable = AnimationClipPlayable.Create(graph, leftGripClip);
        rightGripPlayable = AnimationClipPlayable.Create(graph, rightGripClip);
        rest.SetSpeed(0d);
        leftGripPlayable.SetSpeed(0d);
        rightGripPlayable.SetSpeed(0d);
        graph.Connect(rest, 0, layers, 0);
        graph.Connect(leftGripPlayable, 0, layers, 1);
        graph.Connect(rightGripPlayable, 0, layers, 2);
        layers.SetInputWeight(0, 1f);
        layers.SetInputWeight(1, 1f);
        layers.SetInputWeight(2, 1f);
        leftMask = CreateFingerMask(leftHand);
        rightMask = CreateFingerMask(rightHand);
        if (leftMask == null || rightMask == null)
        {
            Debug.LogError("ArmsVR could not create independent finger masks for both hands.", this);
            return false;
        }
        layers.SetLayerMaskFromAvatarMask(1, leftMask);
        layers.SetLayerMaskFromAvatarMask(2, rightMask);
        var output = AnimationPlayableOutput.Create(graph, "ArmsVR Grip", animator);
        output.SetSourcePlayable(layers);

        if (!rigBuilder.Build(graph) || rigBuilder.layers.Count == 0 || !rigBuilder.layers[0].IsValid())
        {
            Debug.LogError("ArmsVR could not build its IK rig.", this);
            return false;
        }

        graph.Play();
        return true;
    }

    AvatarMask CreateFingerMask(Transform hand)
    {
        var mask = new AvatarMask();
        mask.AddTransformPath(animator.transform, true);
        var handPath = GetRelativePath(animator.transform, hand);
        var activeCount = 0;
        for (var index = 0; index < mask.transformCount; index++)
        {
            var path = mask.GetTransformPath(index);
            var isFinger = path.StartsWith(handPath + "/", StringComparison.Ordinal) ||
                path.Contains("/" + handPath + "/");
            mask.SetTransformActive(index, isFinger);
            if (isFinger)
                activeCount++;
        }
        if (activeCount == 0)
        {
            Destroy(mask);
            return null;
        }
        return mask;
    }

    static string GetRelativePath(Transform root, Transform child)
    {
        var path = child.name;
        while (child.parent != null && child.parent != root)
        {
            child = child.parent;
            path = child.name + "/" + path;
        }

        return path;
    }

    void LateUpdate()
    {
        if (!graph.IsValid())
            return;

        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.0001f)
            bodyRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        transform.rotation = bodyRotation;
        transform.position = head.position + bodyRotation * shoulderOffset - transform.TransformVector(shoulderCenterInModel);

        UpdateTarget(leftController, leftTarget, leftTracked, leftWristPositionOffset, leftWristRotationOffset, ref leftPose, ref hasLeftPose);
        UpdateTarget(rightController, rightTarget, rightTracked, rightWristPositionOffset, rightWristRotationOffset, ref rightPose, ref hasRightPose);
        leftConstraint.weight = hasLeftPose ? 1f : 0f;
        rightConstraint.weight = hasRightPose ? 1f : 0f;
        leftHint.position = leftShoulder.position + bodyRotation * new Vector3(-elbowHintOffset.x, elbowHintOffset.y, elbowHintOffset.z);
        rightHint.position = rightShoulder.position + bodyRotation * elbowHintOffset;

        leftGripPlayable.SetTime(ReadGrip(leftGrip) * Math.Max(0d, leftGripClip.length - 0.0001d));
        rightGripPlayable.SetTime(ReadGrip(rightGrip) * Math.Max(0d, rightGripClip.length - 0.0001d));
        rigBuilder.SyncLayers();
        graph.Evaluate(Time.deltaTime);
    }

    static void UpdateTarget(Transform controller, Transform target, InputAction tracking, Vector3 positionOffset, Vector3 rotationOffset, ref Pose lastPose, ref bool hasPose)
    {
        if (tracking.enabled && tracking.IsPressed())
        {
            lastPose = new Pose(controller.TransformPoint(positionOffset), controller.rotation * Quaternion.Euler(rotationOffset));
            hasPose = true;
        }

        if (hasPose)
            target.SetPositionAndRotation(lastPose.position, lastPose.rotation);
    }

    static float ReadGrip(InputAction grip)
    {
        return grip.enabled ? Mathf.Clamp01(grip.ReadValue<float>()) : 0f;
    }

    void UseFallbackHands()
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
        foreach (var root in gameObject.scene.GetRootGameObjects())
            if (root.name == "VR_Hand_Blue_Left" || root.name == "VR_Hand_Blue_Right")
                root.SetActive(true);
        enabled = false;
    }

    void OnDisable()
    {
        if (rigBuilder != null)
            rigBuilder.Clear();
        if (graph.IsValid())
            graph.Destroy();
        if (leftMask != null)
            Destroy(leftMask);
        if (rightMask != null)
            Destroy(rightMask);
    }
}