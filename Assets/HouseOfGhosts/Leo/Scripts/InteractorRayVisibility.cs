using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Controls the near-far laser pointers. By default the laser only works on UI and only within a limited range;
/// objects have to be grabbed by reaching for them. The ray stays hidden until it points at something it can use.
/// Add this to the XR Origin.
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractorRayVisibility : MonoBehaviour
{
    [Tooltip("The laser only works on UI. Objects can then only be grabbed and used from up close.")]
    [SerializeField] bool uiOnly = true;
    [Tooltip("How far the laser reaches, in meters.")]
    [SerializeField, Min(0.5f)] float maxRayDistance = 5f;
    [Tooltip("With UI Only on: how close the controller has to be to an object to grab or use it, in meters.")]
    [SerializeField, Min(0.05f)] float nearGrabReach = 0.2f;
    [Tooltip("Show the ray when it points at a world-space UI element such as a menu button.")]
    [SerializeField] bool showOnUI = true;
    [Tooltip("Show the ray when it points at an object that can be grabbed or interacted with. Ignored while UI Only is on.")]
    [SerializeField] bool showOnInteractable = true;

    // Rejects hovering and selecting anything that is out of arm's reach, which leaves the far ray with only UI.
    sealed class NearOnlyFilter : IXRHoverFilter, IXRSelectFilter
    {
        readonly InteractorRayVisibility m_Owner;

        public NearOnlyFilter(InteractorRayVisibility owner)
        {
            m_Owner = owner;
        }

        public bool canProcess => m_Owner != null && m_Owner.isActiveAndEnabled && m_Owner.uiOnly;

        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
        {
            if (interactor is IXRSelectInteractor selector && interactable is IXRSelectInteractable selectable && selector.IsSelecting(selectable))
                return true;

            return m_Owner.IsWithinReach(interactor as NearFarInteractor, interactable);
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            // Never break a hold that already exists, e.g. a door handle that swings away from the controller.
            return interactor.IsSelecting(interactable) || m_Owner.IsWithinReach(interactor as NearFarInteractor, interactable);
        }
    }

    NearFarInteractor[] m_Interactors;
    CurveVisualController[] m_Visuals;
    float[] m_OriginalCastDistances;
    NearOnlyFilter m_Filter;

    void OnEnable()
    {
        m_Filter ??= new NearOnlyFilter(this);
        m_Interactors = GetComponentsInChildren<NearFarInteractor>(true);
        m_Visuals = new CurveVisualController[m_Interactors.Length];
        m_OriginalCastDistances = new float[m_Interactors.Length];
        for (var index = 0; index < m_Interactors.Length; index++)
        {
            var interactor = m_Interactors[index];
            m_Visuals[index] = interactor.GetComponentInChildren<CurveVisualController>(true);
            interactor.hoverFilters.Add(m_Filter);
            interactor.selectFilters.Add(m_Filter);
            if (interactor.farInteractionCaster is CurveInteractionCaster caster)
            {
                m_OriginalCastDistances[index] = caster.castDistance;
                caster.castDistance = maxRayDistance;
            }
        }
    }

    void OnDisable()
    {
        if (m_Interactors == null)
            return;

        // Give the rays back to XRI's default behaviour when this component is turned off.
        for (var index = 0; index < m_Interactors.Length; index++)
        {
            var interactor = m_Interactors[index];
            if (interactor != null)
            {
                interactor.hoverFilters.Remove(m_Filter);
                interactor.selectFilters.Remove(m_Filter);
                if (interactor.farInteractionCaster is CurveInteractionCaster caster && m_OriginalCastDistances[index] > 0f)
                    caster.castDistance = m_OriginalCastDistances[index];
            }

            if (m_Visuals[index] != null)
                m_Visuals[index].enabled = true;
        }
    }

    void LateUpdate()
    {
        for (var index = 0; index < m_Interactors.Length; index++)
        {
            var interactor = m_Interactors[index];
            var visual = m_Visuals[index];
            if (interactor == null || visual == null)
                continue;

            var show = interactor.isActiveAndEnabled && ShouldShow(interactor);
            if (visual.enabled != show)
                visual.enabled = show;
            // The controller stops updating the line when disabled, so hide the renderer it last drew too.
            var line = visual.lineRenderer;
            if (!show && line != null && line.enabled)
                line.enabled = false;
        }
    }

    bool ShouldShow(NearFarInteractor interactor)
    {
        // Holding something grabbed from a distance keeps the ray visible; near grabs do not need it.
        if (interactor.hasSelection)
            return interactor.selectionRegion.Value == NearFarInteractor.Region.Far;

        var endPointType = interactor.TryGetCurveEndPoint(out _);
        return (showOnInteractable && !uiOnly && endPointType == EndPointType.ValidCastHit) ||
            (showOnUI && endPointType == EndPointType.UI);
    }

    bool IsWithinReach(NearFarInteractor interactor, IXRInteractable interactable)
    {
        // Sockets, poke and teleport interactors are not laser pointers and keep working as they are.
        if (interactor == null)
            return true;

        var caster = interactor.nearInteractionCaster;
        var castOrigin = caster != null ? caster.effectiveCastOrigin : null;
        var origin = castOrigin != null ? castOrigin.position : interactor.transform.position;
        var reachSquared = nearGrabReach * nearGrabReach;
        var colliders = interactable.colliders;
        var hasCollider = false;
        for (var index = 0; index < colliders.Count; index++)
        {
            var collider = colliders[index];
            if (collider == null || !collider.enabled)
                continue;

            hasCollider = true;
            // ClosestPoint is not supported on non-convex mesh colliders, so those fall back to their bounds.
            var closest = collider is MeshCollider mesh && !mesh.convex
                ? collider.bounds.ClosestPoint(origin)
                : collider.ClosestPoint(origin);
            if ((closest - origin).sqrMagnitude <= reachSquared)
                return true;
        }

        return !hasCollider;
    }
}
