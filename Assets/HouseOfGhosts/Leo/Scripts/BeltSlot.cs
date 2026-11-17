using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// One invisible inventory slot on the PlayerBelt. Holds a single grab interactable and,
// whenever no hand is holding it, smoothly pulls it back into the slot.
[DisallowMultipleComponent]
[RequireComponent(typeof(XRSocketInteractor))]
public sealed class BeltSlot : MonoBehaviour
{
    [SerializeField] XRGrabInteractable item;
    [SerializeField] Transform attachPoint;

    [Tooltip("The player's body collider. The item never collides with it, so it can't push or block the player.")]
    [SerializeField] CharacterController playerBody;

    [Header("Return to Belt")]
    [SerializeField] float returnSpeed = 3f;
    [SerializeField] float minimumReturnTime = 0.2f;
    [SerializeField] float maximumReturnTime = 0.6f;

    [Header("Events")]
    public UnityEvent holstered = new UnityEvent();
    public UnityEvent unholstered = new UnityEvent();

    XRSocketInteractor m_Socket;
    float m_ItemEaseInTime;
    bool m_ItemThrowOnDetach;
    bool m_HasHolsteredOnce;
    bool m_Started;

    public XRGrabInteractable Item => item;
    public bool IsHolstered => m_Socket != null && item != null && m_Socket.IsSelecting(item);

    void Awake()
    {
        m_Socket = GetComponent<XRSocketInteractor>();
        if (attachPoint != null)
            m_Socket.attachTransform = attachPoint;

        // Keep the slot invisible and passive: it only holds what this script gives it.
        m_Socket.showInteractableHoverMeshes = false;
        m_Socket.hoverSocketSnapping = false;
        m_Socket.keepSelectedTargetValid = true;
    }

    void Start()
    {
        if (item == null)
        {
            Debug.LogError("BeltSlot has no item assigned.", this);
            enabled = false;
            return;
        }

        // Done in Start because the item may create its colliders in its own Awake.
        m_ItemEaseInTime = item.attachEaseInTime;
        m_ItemThrowOnDetach = item.throwOnDetach;
        m_Started = true;
        TakeOverItem();
        IgnorePlayerCollisions();
    }

    void OnEnable()
    {
        m_Socket.selectEntered.AddListener(OnItemHolstered);
        m_Socket.selectExited.AddListener(OnItemUnholstered);
        if (m_Started)
            TakeOverItem();
    }

    void OnDisable()
    {
        m_Socket.selectEntered.RemoveListener(OnItemHolstered);
        m_Socket.selectExited.RemoveListener(OnItemUnholstered);

        if (m_Started && item != null)
        {
            item.attachEaseInTime = m_ItemEaseInTime;
            item.throwOnDetach = m_ItemThrowOnDetach;
        }
    }

    // The belt always brings the item back, so it must never be thrown away on release.
    void TakeOverItem()
    {
        if (item != null)
            item.throwOnDetach = false;
    }

    // Runs after the XR Interaction Manager has processed this frame's grabs and releases,
    // so swapping the item directly between hands never sends it back to the belt.
    void LateUpdate()
    {
        if (!m_Started || item == null || !item.isActiveAndEnabled || item.isSelected)
            return;

        var manager = m_Socket.interactionManager;
        if (manager == null || !m_Socket.isActiveAndEnabled)
            return;

        item.attachEaseInTime = m_HasHolsteredOnce ? GetReturnTime() : 0f;
        manager.SelectEnter((IXRSelectInteractor)m_Socket, (IXRSelectInteractable)item);
        if (!IsHolstered)
            item.attachEaseInTime = m_ItemEaseInTime;
    }

    float GetReturnTime()
    {
        var target = m_Socket.GetAttachTransform(item).position;
        var distance = Vector3.Distance(item.GetAttachTransform(m_Socket).position, target);
        return Mathf.Clamp(distance / Mathf.Max(returnSpeed, 0.01f), minimumReturnTime, maximumReturnTime);
    }

    void OnItemHolstered(SelectEnterEventArgs args)
    {
        if (!ReferenceEquals(args.interactableObject, item))
            return;

        m_HasHolsteredOnce = true;
        holstered.Invoke();
    }

    void OnItemUnholstered(SelectExitEventArgs args)
    {
        if (!ReferenceEquals(args.interactableObject, item))
            return;

        // Hands should grab instantly; the ease is only for the return trip.
        item.attachEaseInTime = m_ItemEaseInTime;
        unholstered.Invoke();
    }

    void IgnorePlayerCollisions()
    {
        if (playerBody == null)
            return;

        foreach (var itemCollider in item.GetComponentsInChildren<Collider>(true))
        {
            if (!itemCollider.isTrigger)
                Physics.IgnoreCollision(itemCollider, playerBody, true);
        }
    }
}
