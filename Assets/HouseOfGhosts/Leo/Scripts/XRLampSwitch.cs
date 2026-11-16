using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class XRLampSwitch : MonoBehaviour
{
    [SerializeField] ToggleLight lamp;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip click;
    [SerializeField] Renderer switchRenderer;
    [SerializeField] Color offColor = new Color(0.65f, 0.58f, 0.39f);
    [SerializeField] Color onColor = new Color(0.78f, 0.68f, 0.38f);

    XRSimpleInteractable interactable;
    MaterialPropertyBlock colorProperties;

    public void Configure(ToggleLight targetLamp, AudioSource source, AudioClip clickClip)
    {
        lamp = targetLamp;
        audioSource = source;
        click = clickClip;
        switchRenderer = GetComponent<Renderer>();
    }

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        colorProperties = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        interactable.selectEntered.AddListener(OnPressed);
    }

    void Start()
    {
        UpdateAppearance();
    }

    void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnPressed);
    }

    void OnPressed(SelectEnterEventArgs args)
    {
        if (lamp == null)
            return;

        lamp.Flip();
        UpdateAppearance();
        if (audioSource != null && click != null)
            audioSource.PlayOneShot(click);
    }

    void UpdateAppearance()
    {
        if (switchRenderer == null || lamp == null)
            return;

        switchRenderer.GetPropertyBlock(colorProperties);
        var color = lamp.isOn ? onColor : offColor;
        colorProperties.SetColor("_BaseColor", color);
        colorProperties.SetColor("_Color", color);
        switchRenderer.SetPropertyBlock(colorProperties);
    }
}