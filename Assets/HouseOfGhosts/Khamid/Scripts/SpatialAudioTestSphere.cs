using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Test button: selecting this sphere (grip/trigger) plays the test sound from its direction.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class SpatialAudioTestSphere : MonoBehaviour
{
    [SerializeField] private SpatialAudioDirectionPlayer audioPlayer;
    [SerializeField] private SpatialDirection direction = SpatialDirection.Front;

    [Header("Press Feedback")]
    [SerializeField] private Renderer sphereRenderer;
    [SerializeField] private Color pressedColor = new Color(0.4f, 0.9f, 1f);
    [SerializeField, Min(0f)] private float flashDuration = 0.25f;

    private XRSimpleInteractable interactable;
    private MaterialPropertyBlock colorProperties;
    private WaitForSeconds flashWait;
    private Coroutine flashRoutine;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        colorProperties = new MaterialPropertyBlock();
        flashWait = new WaitForSeconds(flashDuration);
        if (sphereRenderer == null)
            sphereRenderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnPressed);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnPressed);
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
            ClearFlash();
        }
    }

    private void OnPressed(SelectEnterEventArgs args)
    {
        if (audioPlayer == null)
        {
            Debug.LogWarning("SpatialAudioTestSphere has no Audio Player assigned.", this);
            return;
        }

        audioPlayer.PlayFromDirection(direction);

        if (sphereRenderer != null)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
        }
    }

    private IEnumerator Flash()
    {
        sphereRenderer.GetPropertyBlock(colorProperties);
        colorProperties.SetColor("_BaseColor", pressedColor);
        colorProperties.SetColor("_Color", pressedColor);
        sphereRenderer.SetPropertyBlock(colorProperties);
        yield return flashWait;
        ClearFlash();
        flashRoutine = null;
    }

    private void ClearFlash()
    {
        if (sphereRenderer != null)
            sphereRenderer.SetPropertyBlock(null);
    }
}
