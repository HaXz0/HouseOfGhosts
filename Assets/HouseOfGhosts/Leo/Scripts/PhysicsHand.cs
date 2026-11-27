using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Describes the visual hand that a <see cref="PhysicsHand"/> shadows.
/// </summary>
public sealed class PhysicsHandSetup
{
    public string name;
    public Transform handBone;
    public Transform controller;
    // One chain per finger, ordered from the knuckle to the last joint.
    public Transform[][] fingerBones;
    // Fingertip position in the last bone's local space; fingertips have no bone of their own.
    public Vector3[] fingerTipLocal;
    // The thumb sticks out sideways, so it is left out of the palm collider.
    public int thumbIndex;
}

[Serializable]
public sealed class PhysicsHandSettings
{
    [Tooltip("Layer of the hand colliders. 'Ignore Raycast' (2) stops the laser, teleport ray and grab casts from hitting the player's own hands.")]
    [Range(0, 31)] public int layer = 2;
    [Tooltip("Heavier hands push objects harder.")]
    [Min(0.05f)] public float mass = 0.6f;
    [Tooltip("A hand that is held back further than this from the controller snaps back to it, unless something solid is in between. At 2.5x this distance it always snaps back, e.g. after teleporting.")]
    [Min(0.1f)] public float snapBackDistance = 0.3f;
    [Min(0.003f)] public float fingerRadius = 0.009f;
    [Min(0.01f)] public float palmThickness = 0.03f;
    [Range(0f, 1f)] public float friction = 0.6f;

    [Header("Fingers")]
    [Tooltip("Fingers stop closing when they touch something, so they wrap around objects instead of clipping through.")]
    public bool fingersStopOnContact = true;
    [Tooltip("Full finger curls per second.")]
    [Min(1f)] public float fingerCurlSpeed = 8f;
    [Tooltip("While holding something the fingers always close at least this far, even if the object's collider is bulky.")]
    [Range(0f, 1f)] public float minimumHeldCurl = 0.45f;

    [Header("Grabbing")]
    [Tooltip("Grabbed objects follow the hand with physics, so they bump into walls instead of passing through.")]
    public bool heldObjectsCollide = true;
    [Tooltip("The hand stays on what it holds (a blocked object or a door handle) instead of floating away with the controller.")]
    public bool handsStayOnHeldObjects = true;
    [Tooltip("Seconds before a released object collides with the hand again, so throws are not knocked off course.")]
    [Min(0f)] public float releaseCollisionDelay = 0.35f;
}

/// <summary>
/// A rigidbody copy of one hand. It chases the tracked hand pose every physics step, so it is stopped by walls
/// and pushes loose objects. The visual arm is then told to follow it whenever it is held back.
/// </summary>
public sealed class PhysicsHand
{
    // Distance between where the body should be and where physics let it go, mapped to "is it touching something".
    const float k_ContactStart = 0.004f;
    const float k_ContactFull = 0.02f;
    // Metres of fingertip travel per radian of wrist error.
    const float k_AngleToDistance = 0.08f;
    const float k_ContactFadeSeconds = 0.06f;
    const float k_HoldFadeSeconds = 0.12f;
    // The hand lets go visually when the held object is this far from the controller.
    const float k_HoldStretchStart = 0.25f;
    const float k_HoldStretchEnd = 0.4f;
    const float k_ProbeMargin = 0.004f;
    const float k_MaxSpeed = 20f;
    const float k_MaxAngularSpeed = 50f;
    // Squared radius of gyration; larger than a real hand so contacts cannot spin it wildly.
    const float k_GyrationRadiusSquared = 0.004f;
    const int k_SegmentsPerFinger = 2;
    // Beyond this many times the snap back distance the hand always returns, even through a wall.
    const float k_AlwaysSnapMultiplier = 2.5f;

    static readonly Collider[] s_Overlaps = new Collider[8];
    static readonly RaycastHit[] s_Hits = new RaycastHit[8];

    readonly PhysicsHandSetup m_Setup;
    readonly PhysicsHandSettings m_Settings;
    readonly Transform m_PlayerRoot;
    readonly Collider m_PlayerBody;
    readonly GameObject m_Root;
    readonly Rigidbody m_Body;
    readonly BoxCollider m_Palm;
    readonly CapsuleCollider[] m_FingerColliders;
    readonly Quaternion[] m_FingerSignatures;
    readonly List<Collider> m_Colliders = new List<Collider>();
    readonly NearFarInteractor m_Interactor;
    readonly int m_ProbeMask;

    readonly List<Collider> m_HeldColliders = new List<Collider>();
    readonly List<Collider> m_ReleasedColliders = new List<Collider>();
    readonly List<Collider> m_BodyIgnoredColliders = new List<Collider>();
    IXRSelectInteractable m_Held;
    IXRSelectInteractable m_Released;
    XRGrabInteractable m_HeldGrab;
    XRGrabInteractable m_WatchedGrab;
    Vector3 m_PinPosition;
    Quaternion m_PinRotation = Quaternion.identity;
    Vector3 m_HoldOffsetPosition;
    Quaternion m_HoldOffsetRotation = Quaternion.identity;
    float m_HoldBlend;
    float m_RestoreTime;

    Pose m_ChaseTarget;
    Pose m_LastChased;
    bool m_HasChaseTarget;
    bool m_HasChased;
    bool m_Active;
    bool m_Teleport = true;
    float m_Contact;
    float m_ContactBlend;
    float m_PalmScale = -1f;

    public IReadOnlyList<Collider> Colliders => m_Colliders;

    public PhysicsHand(PhysicsHandSetup setup, PhysicsHandSettings settings, Transform playerRoot, Collider playerBody, PhysicsMaterial material, Scene scene)
    {
        m_Setup = setup;
        m_Settings = settings;
        m_PlayerRoot = playerRoot;
        m_PlayerBody = playerBody;
        m_ProbeMask = ~(1 << settings.layer);

        m_Root = new GameObject("Physics Hand " + setup.name) { layer = settings.layer };
        if (scene.IsValid() && m_Root.scene != scene)
            SceneManager.MoveGameObjectToScene(m_Root, scene);
        m_Root.transform.SetPositionAndRotation(setup.handBone.position, setup.handBone.rotation);

        m_Body = m_Root.AddComponent<Rigidbody>();
        m_Body.mass = settings.mass;
        m_Body.useGravity = false;
        m_Body.linearDamping = 0f;
        m_Body.angularDamping = 0f;
        m_Body.interpolation = RigidbodyInterpolation.Interpolate;
        m_Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        m_Body.maxAngularVelocity = k_MaxAngularSpeed;
        m_Body.solverIterations = 12;

        m_Palm = m_Root.AddComponent<BoxCollider>();
        m_Palm.sharedMaterial = material;
        m_Colliders.Add(m_Palm);

        var fingerCount = setup.fingerBones.Length;
        m_FingerColliders = new CapsuleCollider[fingerCount * k_SegmentsPerFinger];
        m_FingerSignatures = new Quaternion[fingerCount];
        for (var finger = 0; finger < fingerCount; finger++)
        {
            var chain = setup.fingerBones[finger];
            if (chain == null || chain.Length == 0)
                continue;

            // A finger with a single bone only gets the fingertip segment.
            if (chain.Length > 1)
                m_FingerColliders[finger * k_SegmentsPerFinger] = CreateFingerCollider(chain[0].name, material);
            m_FingerColliders[finger * k_SegmentsPerFinger + 1] = CreateFingerCollider(chain[chain.Length - 1].name, material);
        }

        RefreshColliders(true);
        // Fixed mass properties: the finger colliders move every step and must not shift the balance of the hand.
        m_Body.automaticCenterOfMass = false;
        m_Body.centerOfMass = m_Palm.center;
        m_Body.automaticInertiaTensor = false;
        m_Body.inertiaTensor = Vector3.one * (settings.mass * k_GyrationRadiusSquared);
        m_Body.inertiaTensorRotation = Quaternion.identity;
        m_Body.detectCollisions = false;

        m_Interactor = setup.controller.GetComponentInChildren<NearFarInteractor>(true);
        if (m_Interactor != null)
        {
            m_Interactor.hoverEntered.AddListener(OnHoverEntered);
            m_Interactor.selectEntered.AddListener(OnSelectEntered);
            m_Interactor.selectExited.AddListener(OnSelectExited);
        }
    }

    CapsuleCollider CreateFingerCollider(string boneName, PhysicsMaterial material)
    {
        var segment = new GameObject(boneName + " Collider") { layer = m_Settings.layer };
        segment.transform.SetParent(m_Root.transform, false);
        var capsule = segment.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = m_Settings.fingerRadius;
        capsule.sharedMaterial = material;
        m_Colliders.Add(capsule);
        return capsule;
    }

    public void Dispose()
    {
        if (m_Interactor != null)
        {
            m_Interactor.hoverEntered.RemoveListener(OnHoverEntered);
            m_Interactor.selectEntered.RemoveListener(OnSelectEntered);
            m_Interactor.selectExited.RemoveListener(OnSelectExited);
        }

        StopWatchingGrab();
        if (m_Held != null)
            ReleaseHeld();
        RestoreReleasedCollisions(true);
        if (m_Root != null)
            UnityEngine.Object.Destroy(m_Root);
    }

    public void IgnoreCollisionsWith(IReadOnlyList<Collider> others)
    {
        for (var index = 0; index < others.Count; index++)
            SetIgnored(others[index], true);
    }

    /// <summary>
    /// Call once per frame after the tracked hand pose is known. Moves <paramref name="target"/> to where the
    /// hand may actually be: on the controller when free, at the obstacle when blocked, on the object when holding.
    /// </summary>
    public void Resolve(Transform target, bool tracked)
    {
        RestoreReleasedCollisions(false);
        SetActive(tracked);
        if (!tracked)
            return;

        var wanted = new Pose(target.position, target.rotation);
        var result = wanted;

        // A free hand follows the controller exactly, so it never lags. Only a hand that physics held back
        // switches over to the rigidbody's pose.
        m_ContactBlend = Mathf.MoveTowards(m_ContactBlend, m_Contact, Time.deltaTime / k_ContactFadeSeconds);
        if (m_ContactBlend > 0f)
            result = Blend(wanted, new Pose(m_Root.transform.position, m_Root.transform.rotation), m_ContactBlend);

        var held = wanted;
        var holding = m_Held != null && m_Settings.handsStayOnHeldObjects && TryGetHeldPose(wanted, out held);
        if (holding)
        {
            m_HoldOffsetPosition = held.position - wanted.position;
            m_HoldOffsetRotation = held.rotation * Quaternion.Inverse(wanted.rotation);
        }

        m_HoldBlend = Mathf.MoveTowards(m_HoldBlend, holding ? 1f : 0f, Time.deltaTime / k_HoldFadeSeconds);
        m_ChaseTarget = wanted;
        if (m_HoldBlend > 0f)
        {
            // The offset is kept after letting go, so the hand eases back to the controller instead of popping.
            var heldPose = new Pose(wanted.position + m_HoldOffsetPosition, m_HoldOffsetRotation * wanted.rotation);
            var stretch = Mathf.InverseLerp(k_HoldStretchStart, k_HoldStretchEnd, m_HoldOffsetPosition.magnitude);
            var weight = m_HoldBlend * (1f - stretch);
            result = Blend(result, heldPose, weight);
            m_ChaseTarget = Blend(wanted, heldPose, weight);
        }

        m_HasChaseTarget = true;
        target.SetPositionAndRotation(result.position, result.rotation);
    }

    /// <summary>
    /// Call from FixedUpdate. Drives the rigidbody towards the latest hand pose.
    /// </summary>
    public void FixedStep()
    {
        if (!m_Active || !m_HasChaseTarget)
            return;

        RefreshColliders(false);
        if (m_HasChased)
        {
            // Unobstructed, the body lands exactly on the pose it chased. Any shortfall means something was in the way.
            var error = Vector3.Distance(m_Body.position, m_LastChased.position) +
                Quaternion.Angle(m_Body.rotation, m_LastChased.rotation) * Mathf.Deg2Rad * k_AngleToDistance;
            m_Contact = Mathf.InverseLerp(k_ContactStart, k_ContactFull, error);
        }

        var offset = m_ChaseTarget.position - m_Body.position;
        if (m_Teleport || ShouldSnapBack(offset.magnitude))
        {
            m_Body.position = m_ChaseTarget.position;
            m_Body.rotation = m_ChaseTarget.rotation;
            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
            m_Teleport = false;
            m_HasChased = false;
            m_Contact = 0f;
            return;
        }

        var deltaTime = Time.fixedDeltaTime;
        m_Body.linearVelocity = Vector3.ClampMagnitude(offset / deltaTime, k_MaxSpeed);

        var rotationDelta = m_ChaseTarget.rotation * Quaternion.Inverse(m_Body.rotation);
        rotationDelta.ToAngleAxis(out var angle, out var axis);
        if (angle > 180f)
            angle -= 360f;
        var hasRotation = Mathf.Abs(angle) > 0.01f && !float.IsInfinity(axis.x) && !float.IsNaN(axis.x);
        m_Body.angularVelocity = hasRotation
            ? Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad / deltaTime), k_MaxAngularSpeed)
            : Vector3.zero;

        m_LastChased = m_ChaseTarget;
        m_HasChased = true;
    }

    // A hand pressed against a wall must stay there, however far the controller is pushed into it.
    // It only jumps back when the way to the controller is free, or when it is hopelessly far away.
    bool ShouldSnapBack(float distance)
    {
        if (distance <= m_Settings.snapBackDistance)
            return false;
        if (distance > m_Settings.snapBackDistance * k_AlwaysSnapMultiplier)
            return true;

        var from = m_Body.position + m_Body.rotation * m_Palm.center;
        var to = m_ChaseTarget.position + m_ChaseTarget.rotation * m_Palm.center;
        var direction = to - from;
        var length = direction.magnitude;
        if (length < 0.001f)
            return true;

        var count = Physics.RaycastNonAlloc(from, direction / length, s_Hits, length, m_ProbeMask, QueryTriggerInteraction.Ignore);
        for (var index = 0; index < count; index++)
        {
            if (IsObstacle(s_Hits[index].collider))
                return false;
        }
        return true;
    }

    // The player's own body and whatever the hand is holding never count as something in the way.
    bool IsObstacle(Collider other)
    {
        return other != m_PlayerBody && !other.transform.IsChildOf(m_PlayerRoot) && !m_HeldColliders.Contains(other);
    }

    /// <summary>
    /// Returns how far a finger may curl this frame: it closes at a limited speed and stops when it touches something.
    /// </summary>
    public float LimitFingerCurl(int finger, float current, float desired)
    {
        if (!m_Active || !m_Settings.fingersStopOnContact || desired <= current)
            return desired;

        var floor = m_Held != null ? m_Settings.minimumHeldCurl : 0f;
        var next = Mathf.Min(desired, current + m_Settings.fingerCurlSpeed * Time.deltaTime);
        return next <= floor || !IsFingerTouching(finger) ? next : current;
    }

    bool IsFingerTouching(int finger)
    {
        var chain = m_Setup.fingerBones[finger];
        if (chain == null || chain.Length == 0)
            return false;

        var last = chain[chain.Length - 1];
        var start = (chain.Length > 1 ? chain[1] : chain[0]).position;
        var tip = last.TransformPoint(m_Setup.fingerTipLocal[finger]);
        var count = Physics.OverlapCapsuleNonAlloc(start, tip, m_Settings.fingerRadius + k_ProbeMargin, s_Overlaps,
            m_ProbeMask, QueryTriggerInteraction.Ignore);
        for (var index = 0; index < count; index++)
        {
            var hit = s_Overlaps[index];
            // The held object does count here: the fingers should wrap around it.
            if (hit != m_PlayerBody && !hit.transform.IsChildOf(m_PlayerRoot))
                return true;
        }
        return false;
    }

    void SetActive(bool active)
    {
        if (m_Active == active)
            return;

        m_Active = active;
        m_Body.detectCollisions = active;
        if (active)
        {
            m_Teleport = true;
        }
        else
        {
            // Stop the body, otherwise it keeps drifting with its last velocity while the controller is not tracked.
            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
            m_HasChaseTarget = false;
            m_HasChased = false;
            m_Contact = 0f;
            m_ContactBlend = 0f;
        }
    }

    // Rebuilds the collider shapes from the visual bones, in the hand bone's rotation space and in world units.
    void RefreshColliders(bool force)
    {
        var hand = m_Setup.handBone;
        var toLocal = Quaternion.Inverse(hand.rotation);
        var origin = hand.position;
        var scale = hand.lossyScale.x;
        var scaleChanged = force || Mathf.Abs(scale - m_PalmScale) > 0.001f;
        if (scaleChanged)
        {
            m_PalmScale = scale;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            for (var finger = 0; finger < m_Setup.fingerBones.Length; finger++)
            {
                var chain = m_Setup.fingerBones[finger];
                if (finger != m_Setup.thumbIndex && chain != null && chain.Length > 0)
                    bounds.Encapsulate(toLocal * (chain[0].position - origin));
            }
            m_Palm.center = bounds.center;
            m_Palm.size = Vector3.Max(bounds.size, Vector3.one * m_Settings.palmThickness);
        }

        for (var finger = 0; finger < m_Setup.fingerBones.Length; finger++)
        {
            var chain = m_Setup.fingerBones[finger];
            if (chain == null || chain.Length == 0)
                continue;

            // The colliders only need to move when the finger's pose changed.
            var signature = chain[0].localRotation;
            for (var joint = 1; joint < chain.Length; joint++)
                signature *= chain[joint].localRotation;
            if (!scaleChanged && Mathf.Abs(Quaternion.Dot(signature, m_FingerSignatures[finger])) > 0.999999f)
                continue;
            m_FingerSignatures[finger] = signature;

            var knuckle = toLocal * (chain[0].position - origin);
            var tip = toLocal * (chain[chain.Length - 1].TransformPoint(m_Setup.fingerTipLocal[finger]) - origin);
            if (chain.Length > 1)
            {
                var middle = toLocal * (chain[1].position - origin);
                PlaceCapsule(m_FingerColliders[finger * k_SegmentsPerFinger], knuckle, middle);
                PlaceCapsule(m_FingerColliders[finger * k_SegmentsPerFinger + 1], middle, tip);
            }
            else
            {
                PlaceCapsule(m_FingerColliders[finger * k_SegmentsPerFinger + 1], knuckle, tip);
            }
        }
    }

    static void PlaceCapsule(CapsuleCollider capsule, Vector3 start, Vector3 end)
    {
        if (capsule == null)
            return;

        var axis = end - start;
        var length = axis.magnitude;
        var capsuleTransform = capsule.transform;
        capsuleTransform.localPosition = (start + end) * 0.5f;
        if (length > 0.0001f)
            capsuleTransform.localRotation = Quaternion.FromToRotation(Vector3.up, axis / length);
        capsule.height = length + capsule.radius * 2f;
    }

    bool TryGetHeldPose(Pose wanted, out Pose held)
    {
        held = wanted;
        if (m_Held is UnityEngine.Object heldObject && heldObject == null)
            return false;

        if (m_HeldGrab != null)
        {
            if (m_Interactor.selectionRegion.Value == NearFarInteractor.Region.Far)
                return false;

            // XRI moves the object so these two attach points meet. Whatever gap is left is how far the object
            // was held back (by a wall, or while it is still flying into the hand), and the hand follows it.
            var objectAttach = m_HeldGrab.GetAttachTransform(m_Interactor);
            var handAttach = m_Interactor.GetAttachTransform(m_HeldGrab);
            if (objectAttach == null || handAttach == null)
                return false;

            var rotationDelta = objectAttach.rotation * Quaternion.Inverse(handAttach.rotation);
            held = new Pose(objectAttach.position + rotationDelta * (wanted.position - handAttach.position),
                rotationDelta * wanted.rotation);
            return true;
        }

        // Handles, sliders and other fixed interactables: stay where the hand first took hold.
        var heldTransform = m_Held.transform;
        held = new Pose(heldTransform.position + heldTransform.rotation * m_PinPosition, heldTransform.rotation * m_PinRotation);
        return true;
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (!m_Settings.heldObjectsCollide || !(args.interactableObject is XRGrabInteractable grab) ||
            grab.movementType == XRBaseInteractable.MovementType.VelocityTracking)
            return;

        // The movement type cannot be swapped safely mid-grab (e.g. while holstered in a belt socket),
        // so wait until whoever holds it lets go.
        if (!grab.isSelected)
        {
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            return;
        }

        StopWatchingGrab();
        m_WatchedGrab = grab;
        grab.selectExited.AddListener(OnWatchedGrabReleased);
    }

    void OnWatchedGrabReleased(SelectExitEventArgs args)
    {
        var grab = m_WatchedGrab;
        StopWatchingGrab();
        if (grab != null && !grab.isSelected)
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
    }

    void StopWatchingGrab()
    {
        if (m_WatchedGrab != null)
            m_WatchedGrab.selectExited.RemoveListener(OnWatchedGrabReleased);
        m_WatchedGrab = null;
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        RestoreReleasedCollisions(true);
        m_Held = args.interactableObject;
        m_HeldGrab = m_Held as XRGrabInteractable;

        var heldTransform = m_Held.transform;
        var hand = m_Setup.handBone;
        var toHeld = Quaternion.Inverse(heldTransform.rotation);
        m_PinPosition = toHeld * (hand.position - heldTransform.position);
        m_PinRotation = toHeld * hand.rotation;

        // The hand must not shove the thing it is holding, and a held object must not block the player's body.
        heldTransform.GetComponentsInChildren(false, m_HeldColliders);
        m_BodyIgnoredColliders.Clear();
        for (var index = 0; index < m_HeldColliders.Count; index++)
        {
            var heldCollider = m_HeldColliders[index];
            SetIgnored(heldCollider, true);
            if (m_HeldGrab != null && m_PlayerBody != null && !Physics.GetIgnoreCollision(m_PlayerBody, heldCollider))
            {
                Physics.IgnoreCollision(m_PlayerBody, heldCollider, true);
                m_BodyIgnoredColliders.Add(heldCollider);
            }
        }
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (ReferenceEquals(args.interactableObject, m_Held))
            ReleaseHeld();
    }

    void ReleaseHeld()
    {
        RestoreReleasedCollisions(true);
        m_ReleasedColliders.AddRange(m_HeldColliders);
        m_HeldColliders.Clear();
        m_Released = m_Held;
        m_RestoreTime = Time.time + m_Settings.releaseCollisionDelay;
        m_Held = null;
        m_HeldGrab = null;
    }

    void RestoreReleasedCollisions(bool immediately)
    {
        if (m_ReleasedColliders.Count == 0 || (!immediately && Time.time < m_RestoreTime))
            return;

        for (var index = 0; index < m_ReleasedColliders.Count; index++)
            SetIgnored(m_ReleasedColliders[index], false);
        m_ReleasedColliders.Clear();

        // If the other hand or a belt socket took the object over, it must keep passing through the player's body.
        var stillHeld = m_Released is UnityEngine.Object released && released != null && m_Released.isSelected;
        if (!stillHeld && m_PlayerBody != null)
        {
            for (var index = 0; index < m_BodyIgnoredColliders.Count; index++)
            {
                if (m_BodyIgnoredColliders[index] != null)
                    Physics.IgnoreCollision(m_PlayerBody, m_BodyIgnoredColliders[index], false);
            }
        }
        m_BodyIgnoredColliders.Clear();
        m_Released = null;
    }

    void SetIgnored(Collider other, bool ignored)
    {
        if (other == null)
            return;

        for (var index = 0; index < m_Colliders.Count; index++)
        {
            if (m_Colliders[index] != null && m_Colliders[index] != other)
                Physics.IgnoreCollision(m_Colliders[index], other, ignored);
        }
    }

    static Pose Blend(Pose from, Pose to, float weight)
    {
        return new Pose(Vector3.Lerp(from.position, to.position, weight), Quaternion.Slerp(from.rotation, to.rotation, weight));
    }
}
