using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public sealed class FlashlightController : MonoBehaviour
{
    [SerializeField] float beamRange = 8f;
    [SerializeField] float beamAngle = 35f;
    [SerializeField] float beamIntensity = 4f;
    [SerializeField] float pickupHapticAmplitude = 0.3f;
    [SerializeField] float pickupHapticDuration = 0.08f;
    [SerializeField] Vector3 beamLocalPosition = new Vector3(0f, 0f, 0.12f);
    [SerializeField] Vector3 beamLocalEulerAngles;
    [SerializeField] float colliderPadding = 0.015f;

    XRGrabInteractable m_Interactable;
    Light m_Beam;

    void Awake()
    {
        SetUpGrab();
        SetUpBeam();
    }

    void OnEnable()
    {
        if (m_Interactable != null)
        {
            m_Interactable.activated.AddListener(ToggleBeam);
            m_Interactable.selectEntered.AddListener(PlayPickupHaptic);
        }
    }

    void OnDisable()
    {
        if (m_Interactable != null)
        {
            m_Interactable.activated.RemoveListener(ToggleBeam);
            m_Interactable.selectEntered.RemoveListener(PlayPickupHaptic);
        }
    }

    void SetUpGrab()
    {
        var body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        body.interpolation = RigidbodyInterpolation.Interpolate;

        if (GetComponent<BoxCollider>() != null || GetComponentInChildren<Collider>(true) == null)
            AddBoundsCollider();

        m_Interactable = GetComponent<XRGrabInteractable>();
        if (m_Interactable == null)
            m_Interactable = gameObject.AddComponent<XRGrabInteractable>();
    }

    void AddBoundsCollider()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("Flashlight has no renderers to size its grab collider.", this);
            return;
        }

        var bounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);

        var scale = transform.lossyScale;
        var minimumScale = 0.0001f;
        var box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(bounds.center);
        box.size = new Vector3(
            bounds.size.x / Mathf.Max(Mathf.Abs(scale.x), minimumScale) + colliderPadding,
            bounds.size.y / Mathf.Max(Mathf.Abs(scale.y), minimumScale) + colliderPadding,
            bounds.size.z / Mathf.Max(Mathf.Abs(scale.z), minimumScale) + colliderPadding);
    }

    void SetUpBeam()
    {
        var beamTransform = transform.Find("Flashlight Beam");
        if (beamTransform == null)
        {
            var createdBeamObject = new GameObject("Flashlight Beam");
            beamTransform = createdBeamObject.transform;
            beamTransform.SetParent(transform, false);
        }

        var beamObject = beamTransform.gameObject;
        beamObject.transform.localPosition = beamLocalPosition;
        beamObject.transform.localRotation = Quaternion.Euler(beamLocalEulerAngles);

        var lensRenderer = FindLensRenderer();
        if (lensRenderer != null)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++)
                    bounds.Encapsulate(renderers[index].bounds);

                var direction = lensRenderer.bounds.center - bounds.center;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    direction.Normalize();
                    beamObject.transform.position = lensRenderer.bounds.center + direction * 0.005f;
                    beamObject.transform.rotation = Quaternion.LookRotation(direction, transform.up);
                }
            }
        }

        m_Beam = beamObject.GetComponent<Light>();
        if (m_Beam == null)
            m_Beam = beamObject.AddComponent<Light>();
        m_Beam.type = LightType.Spot;
        m_Beam.range = beamRange;
        m_Beam.spotAngle = beamAngle;
        m_Beam.innerSpotAngle = beamAngle * 0.65f;
        m_Beam.intensity = beamIntensity;
        m_Beam.shadows = LightShadows.None;
        m_Beam.enabled = false;
    }

    Renderer FindLensRenderer()
    {
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name.IndexOf("Lens", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var renderer = child.GetComponent<Renderer>();
            if (renderer == null)
                renderer = child.GetComponentInChildren<Renderer>(true);
            if (renderer != null)
                return renderer;
        }

        return null;
    }

    void PlayPickupHaptic(SelectEnterEventArgs args)
    {
        var interactorComponent = args.interactorObject as Component;
        if (interactorComponent == null)
            return;

        var hapticPlayer = interactorComponent.GetComponentInParent<HapticImpulsePlayer>();
        hapticPlayer?.SendHapticImpulse(pickupHapticAmplitude, pickupHapticDuration);
    }

    void ToggleBeam(ActivateEventArgs args)
    {
        if (m_Beam != null)
            m_Beam.enabled = !m_Beam.enabled;
    }
}