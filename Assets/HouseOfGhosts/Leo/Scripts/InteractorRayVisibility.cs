using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Hides the near-far laser pointers until they point at something useful: an interactable object, a UI element,
/// or while an object is held from a distance. Add this to the XR Origin.
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractorRayVisibility : MonoBehaviour
{
    [Tooltip("Show the ray when it points at a world-space UI element such as a menu button.")]
    [SerializeField] bool showOnUI = true;
    [Tooltip("Show the ray when it points at an object that can be grabbed or interacted with.")]
    [SerializeField] bool showOnInteractable = true;

    NearFarInteractor[] m_Interactors;
    CurveVisualController[] m_Visuals;

    void OnEnable()
    {
        m_Interactors = GetComponentsInChildren<NearFarInteractor>(true);
        m_Visuals = new CurveVisualController[m_Interactors.Length];
        for (var index = 0; index < m_Interactors.Length; index++)
            m_Visuals[index] = m_Interactors[index].GetComponentInChildren<CurveVisualController>(true);
    }

    void OnDisable()
    {
        if (m_Visuals == null)
            return;

        // Give the rays back to XRI's default behaviour when this component is turned off.
        foreach (var visual in m_Visuals)
        {
            if (visual != null)
                visual.enabled = true;
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
        return (showOnInteractable && endPointType == EndPointType.ValidCastHit) ||
            (showOnUI && endPointType == EndPointType.UI);
    }
}
