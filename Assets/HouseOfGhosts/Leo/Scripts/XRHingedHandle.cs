using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class XRHingedHandle : MonoBehaviour
{
    [SerializeField] Transform hinge;
    [SerializeField] float minimumAngle = 0f;
    [SerializeField] float maximumAngle = 100f;

    XRSimpleInteractable interactable;
    IXRSelectInteractor selectingInteractor;
    Quaternion closedRotation;
    Vector3 previousDirection;
    float angle;

    public void Configure(Transform hingeTransform, float minAngle, float maxAngle)
    {
        hinge = hingeTransform;
        minimumAngle = minAngle;
        maximumAngle = maxAngle;
        closedRotation = hinge.localRotation;
    }

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (hinge == null)
        {
            Debug.LogError("A hinge transform is required.", this);
            enabled = false;
            return;
        }

        closedRotation = hinge.localRotation;

        // A collider that moves needs a rigidbody; kinematic lets the door push hands and loose objects out of its way.
        if (hinge.GetComponentInParent<Rigidbody>() == null)
            hinge.gameObject.AddComponent<Rigidbody>().isKinematic = true;
    }

    void OnEnable()
    {
        interactable.selectEntered.AddListener(BeginGrab);
        interactable.selectExited.AddListener(EndGrab);
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(BeginGrab);
            interactable.selectExited.RemoveListener(EndGrab);
        }

        selectingInteractor = null;
    }

    void Update()
    {
        if (selectingInteractor == null || hinge == null)
            return;

        var attachment = selectingInteractor.GetAttachTransform(interactable);
        if (attachment == null)
            return;

        var direction = Vector3.ProjectOnPlane(attachment.position - hinge.position, hinge.up);
        if (direction.sqrMagnitude < 0.0001f)
            return;

        if (previousDirection.sqrMagnitude > 0.0001f)
        {
            angle = Mathf.Clamp(angle + Vector3.SignedAngle(previousDirection, direction, hinge.up), minimumAngle, maximumAngle);
            hinge.localRotation = closedRotation * Quaternion.AngleAxis(angle, Vector3.up);
        }

        previousDirection = direction;
    }

    void BeginGrab(SelectEnterEventArgs args)
    {
        selectingInteractor = args.interactorObject;
        var attachment = selectingInteractor.GetAttachTransform(interactable);
        previousDirection = attachment != null
            ? Vector3.ProjectOnPlane(attachment.position - hinge.position, hinge.up)
            : Vector3.zero;
    }

    void EndGrab(SelectExitEventArgs args)
    {
        if (selectingInteractor == args.interactorObject)
            selectingInteractor = null;
    }
}