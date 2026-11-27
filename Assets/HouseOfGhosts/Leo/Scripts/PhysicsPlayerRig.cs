using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Makes the player physical: rigidbody hands and fingers that collide with the world, a body that pushes
/// loose objects, and a laser pointer that only reaches nearby UI.
/// <see cref="ArmsVRController"/> adds this automatically. Add it to the ArmsVR object yourself to tune it in the
/// Inspector, or disable it there to go back to hands that pass through everything.
/// </summary>
[DisallowMultipleComponent]
public sealed class PhysicsPlayerRig : MonoBehaviour
{
    [SerializeField] PhysicsHandSettings hands = new PhysicsHandSettings();

    [Header("Body")]
    [Tooltip("Walking into a loose object pushes it away instead of stopping the player dead.")]
    [SerializeField] bool bodyPushesObjects = true;
    [Tooltip("Push strength in newtons.")]
    [SerializeField, Min(0f)] float bodyPushForce = 40f;

    [Header("Laser Pointer")]
    [Tooltip("Adds InteractorRayVisibility to the XR Origin when it is missing, so the laser only works on UI within a limited range.")]
    [SerializeField] bool restrictLaserPointer = true;

    XROrigin m_Origin;
    PhysicsHandSetup[] m_Setups;
    PhysicsHand[] m_Hands;
    PhysicsMaterial m_Material;
    PlayerBodyPhysics m_BodyPhysics;
    bool m_OwnsBodyPhysics;

    /// <summary>
    /// Connects the physics rig to the visual arms. Index 0 is the left hand, index 1 the right hand.
    /// </summary>
    public void Attach(XROrigin origin, PhysicsHandSetup left, PhysicsHandSetup right)
    {
        Teardown();
        m_Origin = origin;
        m_Setups = new[] { left, right };
        if (enabled && gameObject.activeInHierarchy)
            Build();
    }

    public void Detach()
    {
        Teardown();
        m_Setups = null;
        m_Origin = null;
    }

    public void ResolveHand(int index, Transform target, bool tracked)
    {
        if (m_Hands != null)
            m_Hands[index].Resolve(target, tracked);
    }

    public float LimitFingerCurl(int index, int finger, float current, float desired)
    {
        return m_Hands != null ? m_Hands[index].LimitFingerCurl(finger, current, desired) : desired;
    }

    void OnEnable()
    {
        if (m_Setups != null && m_Origin != null)
            Build();
    }

    void OnDisable()
    {
        Teardown();
    }

    void FixedUpdate()
    {
        if (m_Hands == null)
            return;

        m_Hands[0].FixedStep();
        m_Hands[1].FixedStep();
    }

    void Build()
    {
        if (m_Hands != null)
            return;

        m_Material = new PhysicsMaterial("Physics Hand")
        {
            dynamicFriction = hands.friction,
            staticFriction = hands.friction,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };

        var playerBody = m_Origin.GetComponentInChildren<CharacterController>(true);
        m_Hands = new PhysicsHand[m_Setups.Length];
        for (var index = 0; index < m_Setups.Length; index++)
            m_Hands[index] = new PhysicsHand(m_Setups[index], hands, m_Origin.transform, playerBody, m_Material, gameObject.scene);

        // The hands must never push the player's own body, belt, or each other around.
        var playerColliders = m_Origin.GetComponentsInChildren<Collider>(false);
        m_Hands[0].IgnoreCollisionsWith(playerColliders);
        m_Hands[1].IgnoreCollisionsWith(playerColliders);
        m_Hands[0].IgnoreCollisionsWith(m_Hands[1].Colliders);

        if (bodyPushesObjects && playerBody != null)
        {
            m_BodyPhysics = playerBody.GetComponent<PlayerBodyPhysics>();
            m_OwnsBodyPhysics = m_BodyPhysics == null;
            if (m_OwnsBodyPhysics)
                m_BodyPhysics = playerBody.gameObject.AddComponent<PlayerBodyPhysics>();
            m_BodyPhysics.PushForce = bodyPushForce;
        }

        if (restrictLaserPointer && m_Origin.GetComponent<InteractorRayVisibility>() == null)
            m_Origin.gameObject.AddComponent<InteractorRayVisibility>();
    }

    void Teardown()
    {
        if (m_Hands != null)
        {
            foreach (var hand in m_Hands)
                hand.Dispose();
            m_Hands = null;
        }

        if (m_OwnsBodyPhysics && m_BodyPhysics != null)
            Destroy(m_BodyPhysics);
        m_BodyPhysics = null;
        m_OwnsBodyPhysics = false;

        if (m_Material != null)
            Destroy(m_Material);
        m_Material = null;
    }
}
