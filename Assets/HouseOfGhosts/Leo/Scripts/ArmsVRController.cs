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
    const string LeftArmScaleKey = "PlayerTestScene.LeftArmScale";
    const string RightArmScaleKey = "PlayerTestScene.RightArmScale";
    const string LeftWristRotationKey = "PlayerTestScene.LeftWristRotation";
    const string RightWristRotationKey = "PlayerTestScene.RightWristRotation";
    public const float MinimumArmScale = 0.65f;
    public const float MaximumArmScale = 1.4f;
    const float k_TrackingFadeSeconds = 0.16f;

    // Base hand pose relative to each controller. The hand bone's fingers point along its local Y axis,
    // so Y follows the controller's forward and the palm faces inward.
    static readonly Quaternion k_LeftHandOrientation = Quaternion.LookRotation(Vector3.left, Vector3.forward);
    static readonly Quaternion k_RightHandOrientation = Quaternion.LookRotation(Vector3.right, Vector3.forward);

    struct ArmLengthJob : IAnimationJob
    {
        public TransformStreamHandle leftElbow;
        public TransformStreamHandle rightElbow;
        public TransformStreamHandle leftWrist;
        public TransformStreamHandle rightWrist;
        public float leftScale;
        public float rightScale;

        public void ProcessAnimation(AnimationStream stream)
        {
            leftElbow.SetLocalPosition(stream, leftElbow.GetLocalPosition(stream) * leftScale);
            leftWrist.SetLocalPosition(stream, leftWrist.GetLocalPosition(stream) * leftScale);
            rightElbow.SetLocalPosition(stream, rightElbow.GetLocalPosition(stream) * rightScale);
            rightWrist.SetLocalPosition(stream, rightWrist.GetLocalPosition(stream) * rightScale);
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

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

    [Header("Arm Feel")]
    [Tooltip("How much the elbow follows the wrist's rotation (0 = body only, 1 = wrist only).")]
    [SerializeField, Range(0f, 1f)] float wristDrivenElbow = 0.6f;
    [Tooltip("How far the elbow sits below the forearm, relative to the back of the hand.")]
    [SerializeField] float elbowDropBelowForearm = 0.12f;
    [Tooltip("Higher = hands follow the controllers more tightly. Lower = heavier arms.")]
    [SerializeField, Range(5f, 60f)] float handPositionSharpness = 22f;
    [SerializeField, Range(5f, 60f)] float handRotationSharpness = 16f;
    [SerializeField, Range(2f, 40f)] float elbowSharpness = 10f;

    Animator animator;
    RigBuilder rigBuilder;
    PlayableGraph graph;
    AnimationClipPlayable leftGripPlayable;
    AnimationClipPlayable rightGripPlayable;
    AnimationScriptPlayable armLengthPlayable;
    AvatarMask leftMask;
    AvatarMask rightMask;
    Avatar runtimeAvatar;
    InputAction leftGrip;
    InputAction rightGrip;
    InputAction leftTracked;
    InputAction rightTracked;
    Vector3 shoulderCenterInModel;
    Pose leftPose;
    Pose rightPose;
    bool hasLeftPose;
    bool hasRightPose;
    Vector3 leftHintLocal;
    Vector3 rightHintLocal;
    bool hasLeftHint;
    bool hasRightHint;
    Quaternion bodyRotation;
    float leftArmScale = 1f;
    float rightArmScale = 1f;
    float leftConstraintWeight;
    float rightConstraintWeight;
    float baselineShoulderWidth;
    float baselineLeftArmLength;
    float baselineRightArmLength;
    bool baselineCaptured;
    bool ownsLeftGripAction;
    bool ownsRightGripAction;
    bool ownsLeftTrackingAction;
    bool ownsRightTrackingAction;

    public float LeftArmScale => leftArmScale;
    public float RightArmScale => rightArmScale;
    public Vector3 LeftWristRotationOffset => leftWristRotationOffset;
    public Vector3 RightWristRotationOffset => rightWristRotationOffset;

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

        if (animator.avatar == null || !animator.avatar.isValid)
        {
            runtimeAvatar = AvatarBuilder.BuildGenericAvatar(gameObject, string.Empty);
            if (runtimeAvatar == null || !runtimeAvatar.isValid)
            {
                Debug.LogError("ArmsVR could not build a Generic Avatar; finger animations cannot bind to the model.", this);
                UseFallbackHands();
                return;
            }

            animator.avatar = runtimeAvatar;
            animator.Rebind();
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

        EnableActionIfNeeded(leftGrip, ref ownsLeftGripAction);
        EnableActionIfNeeded(rightGrip, ref ownsRightGripAction);
        EnableActionIfNeeded(leftTracked, ref ownsLeftTrackingAction);
        EnableActionIfNeeded(rightTracked, ref ownsRightTrackingAction);

        if (!baselineCaptured)
        {
            shoulderCenterInModel = transform.InverseTransformPoint((leftShoulder.position + rightShoulder.position) * 0.5f);
            baselineShoulderWidth = Vector3.Distance(leftShoulder.position, rightShoulder.position);
            baselineLeftArmLength = Vector3.Distance(leftShoulder.position, leftHand.parent.position) +
                Vector3.Distance(leftHand.parent.position, leftHand.position);
            baselineRightArmLength = Vector3.Distance(rightShoulder.position, rightHand.parent.position) +
                Vector3.Distance(rightHand.parent.position, rightHand.position);
            baselineCaptured = true;
        }
        bodyRotation = transform.rotation;
        leftArmScale = LoadArmScale(LeftArmScaleKey);
        rightArmScale = LoadArmScale(RightArmScaleKey);
        leftWristRotationOffset = LoadWristRotation(LeftWristRotationKey, leftWristRotationOffset);
        rightWristRotationOffset = LoadWristRotation(RightWristRotationKey, rightWristRotationOffset);
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
        var lengthJob = new ArmLengthJob
        {
            leftElbow = animator.BindStreamTransform(leftHand.parent),
            rightElbow = animator.BindStreamTransform(rightHand.parent),
            leftWrist = animator.BindStreamTransform(leftHand),
            rightWrist = animator.BindStreamTransform(rightHand),
            leftScale = leftArmScale,
            rightScale = rightArmScale,
        };
        armLengthPlayable = AnimationScriptPlayable.Create(graph, lengthJob, 1);
        graph.Connect(layers, 0, armLengthPlayable, 0);
        armLengthPlayable.SetInputWeight(0, 1f);
        var output = AnimationPlayableOutput.Create(graph, "ArmsVR Grip", animator);
        output.SetSourcePlayable(armLengthPlayable);

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

        UpdateTarget(leftController, leftTarget, leftTracked, leftWristPositionOffset, k_LeftHandOrientation, leftWristRotationOffset, ref leftPose, ref hasLeftPose, ref leftConstraintWeight);
        UpdateTarget(rightController, rightTarget, rightTracked, rightWristPositionOffset, k_RightHandOrientation, rightWristRotationOffset, ref rightPose, ref hasRightPose, ref rightConstraintWeight);
        leftConstraint.weight = leftConstraintWeight;
        rightConstraint.weight = rightConstraintWeight;
        leftTarget.position = SoftenReach(leftShoulder.position, leftHand.parent.position, leftHand.position, leftTarget.position);
        rightTarget.position = SoftenReach(rightShoulder.position, rightHand.parent.position, rightHand.position, rightTarget.position);
        UpdateElbowHint(leftShoulder, leftHand, leftTarget, leftHint, leftController.parent, -1f, ref leftHintLocal, ref hasLeftHint);
        UpdateElbowHint(rightShoulder, rightHand, rightTarget, rightHint, rightController.parent, 1f, ref rightHintLocal, ref hasRightHint);

        leftGripPlayable.SetTime(ReadGrip(leftGrip) * Math.Max(0d, leftGripClip.length - 0.0001d));
        rightGripPlayable.SetTime(ReadGrip(rightGrip) * Math.Max(0d, rightGripClip.length - 0.0001d));
        rigBuilder.SyncLayers();
        graph.Evaluate(Time.deltaTime);
    }

    // The pose is smoothed in the controllers' parent space (inside the XR Origin), so walking,
    // teleporting and snap turning never make the arms trail behind the player.
    void UpdateTarget(Transform controller, Transform target, InputAction tracking, Vector3 positionOffset, Quaternion handOrientation, Vector3 rotationOffset, ref Pose smoothedLocalPose, ref bool hasPose, ref float constraintWeight)
    {
        var space = controller.parent;
        var tracked = tracking.enabled && tracking.IsPressed();
        if (tracked)
        {
            var worldPosition = controller.TransformPoint(positionOffset);
            var worldRotation = controller.rotation * handOrientation * Quaternion.Euler(rotationOffset);
            var localPosition = space != null ? space.InverseTransformPoint(worldPosition) : worldPosition;
            var localRotation = space != null ? Quaternion.Inverse(space.rotation) * worldRotation : worldRotation;

            if (!hasPose)
            {
                smoothedLocalPose = new Pose(localPosition, localRotation);
            }
            else
            {
                smoothedLocalPose.position = Vector3.Lerp(smoothedLocalPose.position, localPosition, SmoothingBlend(handPositionSharpness));
                smoothedLocalPose.rotation = Quaternion.Slerp(smoothedLocalPose.rotation, localRotation, SmoothingBlend(handRotationSharpness));
            }

            hasPose = true;
        }

        if (hasPose)
        {
            var position = space != null ? space.TransformPoint(smoothedLocalPose.position) : smoothedLocalPose.position;
            var rotation = space != null ? space.rotation * smoothedLocalPose.rotation : smoothedLocalPose.rotation;
            target.SetPositionAndRotation(position, rotation);
        }

        var desiredWeight = tracked && hasPose ? 1f : 0f;
        constraintWeight = Mathf.MoveTowards(constraintWeight, desiredWeight, Time.deltaTime / k_TrackingFadeSeconds);
    }

    static float SmoothingBlend(float sharpness)
    {
        return 1f - Mathf.Exp(-sharpness * Time.deltaTime);
    }

    Vector3 SoftenReach(Vector3 root, Vector3 mid, Vector3 tip, Vector3 desired)
    {
        var maximumReach = Vector3.Distance(root, mid) + Vector3.Distance(mid, tip);
        if (maximumReach < 0.001f)
            return desired;

        var offset = desired - root;
        var distance = offset.magnitude;
        var softStart = maximumReach * 0.78f;
        if (distance <= softStart || distance < 0.001f)
            return desired;

        var softRange = Mathf.Max(maximumReach - softStart, 0.001f);
        var softenedDistance = softStart + softRange * (1f - Mathf.Exp(-(distance - softStart) / softRange));
        return root + offset / distance * Mathf.Min(softenedDistance, maximumReach * 0.98f);
    }

    void UpdateElbowHint(Transform shoulder, Transform hand, Transform target, Transform hint, Transform space, float side, ref Vector3 smoothedLocalHint, ref bool hasHint)
    {
        var shoulderToTarget = target.position - shoulder.position;
        var lateral = bodyRotation * Vector3.right * side;
        var backward = -(bodyRotation * Vector3.forward);
        var bodyHint = shoulder.position + shoulderToTarget * 0.4f + lateral * elbowHintOffset.x +
            Vector3.down * Mathf.Abs(elbowHintOffset.y) + backward * Mathf.Abs(elbowHintOffset.z);

        // The fingers point along the hand's local Y axis, so the forearm runs the opposite way.
        // Below the forearm is +X for the left hand and -X for the right hand. Rolling the palm down
        // therefore swings the elbow outward, and rolling it up tucks the elbow in.
        var forearmDirection = target.rotation * Vector3.down;
        var belowForearm = target.rotation * Vector3.right * -side;
        var forearmLength = Vector3.Distance(hand.parent.position, hand.position);
        var wristHint = target.position + forearmDirection * forearmLength + belowForearm * elbowDropBelowForearm;

        var desiredHint = Vector3.Lerp(bodyHint, wristHint, wristDrivenElbow);
        var localHint = space != null ? space.InverseTransformPoint(desiredHint) : desiredHint;
        smoothedLocalHint = hasHint ? Vector3.Lerp(smoothedLocalHint, localHint, SmoothingBlend(elbowSharpness)) : localHint;
        hasHint = true;
        hint.position = space != null ? space.TransformPoint(smoothedLocalHint) : smoothedLocalHint;
    }

    static float ReadGrip(InputAction grip)
    {
        return grip.enabled ? Mathf.Clamp01(grip.ReadValue<float>()) : 0f;
    }

    static void EnableActionIfNeeded(InputAction action, ref bool enabledHere)
    {
        if (action.enabled)
            return;

        action.Enable();
        enabledHere = true;
    }

    public void SetArmScale(float value)
    {
        SetArmScales(value, value);
    }

    public void SetLeftArmScale(float value)
    {
        SetArmScales(value, rightArmScale);
    }

    public void SetRightArmScale(float value)
    {
        SetArmScales(leftArmScale, value);
    }

    public void SetArmScales(float left, float right)
    {
        if (!IsFinite(left) || !IsFinite(right))
            return;

        leftArmScale = Mathf.Clamp(left, MinimumArmScale, MaximumArmScale);
        rightArmScale = Mathf.Clamp(right, MinimumArmScale, MaximumArmScale);
        if (armLengthPlayable.IsValid())
        {
            var job = armLengthPlayable.GetJobData<ArmLengthJob>();
            job.leftScale = leftArmScale;
            job.rightScale = rightArmScale;
            armLengthPlayable.SetJobData(job);
        }

        PlayerPrefs.SetFloat(LeftArmScaleKey, leftArmScale);
        PlayerPrefs.SetFloat(RightArmScaleKey, rightArmScale);
    }

    public void SetWristRotationOffsets(Vector3 left, Vector3 right)
    {
        if (!IsFinite(left) || !IsFinite(right))
            return;

        leftWristRotationOffset = ClampAngles(left);
        rightWristRotationOffset = ClampAngles(right);
        PlayerPrefs.SetString(LeftWristRotationKey, JsonUtility.ToJson(leftWristRotationOffset));
        PlayerPrefs.SetString(RightWristRotationKey, JsonUtility.ToJson(rightWristRotationOffset));
    }

    public void ResetArmScales()
    {
        SetArmScales(1f, 1f);
    }

    static float LoadArmScale(string key)
    {
        var savedScale = PlayerPrefs.GetFloat(key, 1f);
        return IsFinite(savedScale) ? Mathf.Clamp(savedScale, MinimumArmScale, MaximumArmScale) : 1f;
    }

    static Vector3 LoadWristRotation(string key, Vector3 fallback)
    {
        if (!PlayerPrefs.HasKey(key))
            return ClampAngles(fallback);

        var saved = JsonUtility.FromJson<Vector3>(PlayerPrefs.GetString(key));
        return IsFinite(saved) ? ClampAngles(saved) : ClampAngles(fallback);
    }

    static Vector3 ClampAngles(Vector3 angles)
    {
        return new Vector3(Mathf.Clamp(angles.x, -180f, 180f), Mathf.Clamp(angles.y, -180f, 180f), Mathf.Clamp(angles.z, -180f, 180f));
    }

    public bool TryGetCalibrationSample(out float estimatedLeftScale, out float estimatedRightScale, out Vector3 leftWrist, out Vector3 rightWrist, out string error)
    {
        estimatedLeftScale = 1f;
        estimatedRightScale = 1f;
        leftWrist = Vector3.zero;
        rightWrist = Vector3.zero;
        error = "Tracking unavailable";
        if (!isActiveAndEnabled || leftTracked == null || rightTracked == null ||
            !leftTracked.enabled || !rightTracked.enabled || !leftTracked.IsPressed() || !rightTracked.IsPressed())
            return false;

        if (!IsFinite(baselineLeftArmLength) || !IsFinite(baselineRightArmLength) || !IsFinite(baselineShoulderWidth) ||
            baselineLeftArmLength < 0.1f || baselineRightArmLength < 0.1f || baselineShoulderWidth < 0.05f)
        {
            error = "Arm rig has invalid proportions";
            return false;
        }

        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f)
        {
            error = "Look forward";
            return false;
        }

        var heading = Quaternion.LookRotation(forward.normalized, Vector3.up);
        var shoulderCenter = head.position + heading * shoulderOffset;
        leftWrist = leftController.TransformPoint(leftWristPositionOffset);
        rightWrist = rightController.TransformPoint(rightWristPositionOffset);
        if (!IsFinite(shoulderCenter) || !IsFinite(leftWrist) || !IsFinite(rightWrist))
        {
            error = "Tracking position invalid";
            return false;
        }
        var leftRelative = Quaternion.Inverse(heading) * (leftWrist - shoulderCenter);
        var rightRelative = Quaternion.Inverse(heading) * (rightWrist - shoulderCenter);
        var measuredSpan = rightRelative.x - leftRelative.x;
        if (leftRelative.x >= -0.2f || rightRelative.x <= 0.2f ||
            Mathf.Abs(leftRelative.y) > 0.35f || Mathf.Abs(rightRelative.y) > 0.35f ||
            Mathf.Abs(leftRelative.z) > 0.45f || Mathf.Abs(rightRelative.z) > 0.45f ||
            measuredSpan < 0.8f || measuredSpan > 2.6f || measuredSpan <= baselineShoulderWidth)
        {
            error = "Extend both arms sideways at shoulder height";
            return false;
        }

        var leftLength = Mathf.Abs(leftRelative.x) - baselineShoulderWidth * 0.5f;
        var rightLength = rightRelative.x - baselineShoulderWidth * 0.5f;
        if (!TryEstimateArmScales(leftLength, rightLength, baselineLeftArmLength, baselineRightArmLength,
            out estimatedLeftScale, out estimatedRightScale))
        {
            error = "Arm measurement invalid";
            return false;
        }
        error = null;
        return true;
    }

    public static bool TryEstimateArmScales(float leftLength, float rightLength, float baselineLeftLength, float baselineRightLength, out float leftScale, out float rightScale)
    {
        leftScale = 1f;
        rightScale = 1f;
        if (!IsFinite(leftLength) || !IsFinite(rightLength) || !IsFinite(baselineLeftLength) || !IsFinite(baselineRightLength) ||
            leftLength < 0.25f || rightLength < 0.25f || leftLength > 1.3f || rightLength > 1.3f ||
            baselineLeftLength < 0.1f || baselineRightLength < 0.1f)
            return false;

        var estimatedLeft = leftLength / baselineLeftLength;
        var estimatedRight = rightLength / baselineRightLength;
        if (!IsFinite(estimatedLeft) || !IsFinite(estimatedRight))
            return false;

        leftScale = Mathf.Clamp(estimatedLeft, MinimumArmScale, MaximumArmScale);
        rightScale = Mathf.Clamp(estimatedRight, MinimumArmScale, MaximumArmScale);
        return true;
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
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
        DisableActionIfOwned(leftGrip, ownsLeftGripAction);
        DisableActionIfOwned(rightGrip, ownsRightGripAction);
        DisableActionIfOwned(leftTracked, ownsLeftTrackingAction);
        DisableActionIfOwned(rightTracked, ownsRightTrackingAction);
        ownsLeftGripAction = false;
        ownsRightGripAction = false;
        ownsLeftTrackingAction = false;
        ownsRightTrackingAction = false;
        if (rigBuilder != null)
            rigBuilder.Clear();
        if (graph.IsValid())
            graph.Destroy();
        if (leftMask != null)
            Destroy(leftMask);
        if (rightMask != null)
            Destroy(rightMask);
        if (runtimeAvatar != null)
        {
            if (animator != null && animator.avatar == runtimeAvatar)
                animator.avatar = null;
            Destroy(runtimeAvatar);
            runtimeAvatar = null;
        }
    }

    static void DisableActionIfOwned(InputAction action, bool enabledHere)
    {
        if (enabledHere && action != null && action.enabled)
            action.Disable();
    }
}