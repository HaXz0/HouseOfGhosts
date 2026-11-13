using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

public class PlayerTestSceneComfortSettings : MonoBehaviour
{
    [SerializeField] GameObject m_VignettePrefab;
    [SerializeField] Vector3 m_PalmNormalLocalAxis = Vector3.up;
    [SerializeField, Range(0f, 1f)] float m_PalmUpThreshold = 0.65f;
    [SerializeField, Range(0f, 1f)] float m_GazeThreshold = 0.75f;
    [SerializeField, Min(0f)] float m_HideGracePeriod = 0.25f;
    [SerializeField] Vector3 m_WristMenuOffset = new Vector3(0f, 0.12f, 0.03f);

    const string k_MovementKey = "PlayerTestScene.Movement";
    const string k_TurnKey = "PlayerTestScene.Turn";
    const string k_MoveSpeedKey = "PlayerTestScene.MoveSpeed";
    const string k_SnapAngleKey = "PlayerTestScene.SnapAngle";
    const string k_TurnSpeedKey = "PlayerTestScene.TurnSpeed";
    const string k_VignetteKey = "PlayerTestScene.Vignette";
    const string k_VignetteStrengthKey = "PlayerTestScene.VignetteStrength";

    static readonly Color k_PanelColor = new Color(0.055f, 0.052f, 0.047f, 0.97f);
    static readonly Color k_ControlColor = new Color(0.16f, 0.15f, 0.13f, 1f);
    static readonly Color k_SelectedColor = new Color(0.43f, 0.34f, 0.21f, 1f);
    static readonly Color k_TextColor = new Color(0.89f, 0.84f, 0.74f, 1f);
    static readonly Color k_AccentColor = new Color(0.72f, 0.58f, 0.36f, 1f);

    readonly Dictionary<LocomotionProvider, bool> m_ProviderStates = new Dictionary<LocomotionProvider, bool>();
    readonly List<LocomotionVignetteProvider> m_VignetteProviders = new List<LocomotionVignetteProvider>();

    ControllerInputActionManager[] m_InputManagers;
    DynamicMoveProvider[] m_MoveProviders;
    SnapTurnProvider[] m_SnapTurnProviders;
    ContinuousTurnProvider[] m_ContinuousTurnProviders;
    LocomotionProvider[] m_LocomotionProviders;
    Camera m_PlayerCamera;
    Transform m_LeftWrist;
    CanvasGroup m_CanvasGroup;
    RectTransform m_CanvasRect;
    TunnelingVignetteController m_VignetteController;
    Button m_TeleportButton;
    Button m_SmoothMoveButton;
    Button m_SnapTurnButton;
    Button m_SmoothTurnButton;
    Button m_VignetteButton;
    Slider m_MoveSpeedSlider;
    Slider m_SnapAngleSlider;
    Slider m_TurnSpeedSlider;
    Slider m_VignetteStrengthSlider;
    Text m_MoveSpeedLabel;
    Text m_SnapAngleLabel;
    Text m_TurnSpeedLabel;
    Text m_VignetteStrengthLabel;
    Coroutine m_SaveRoutine;

    bool m_SmoothMovement;
    bool m_SmoothTurning;
    bool m_VignetteEnabled;
    bool m_MenuVisible;
    bool m_HideTimerStarted;
    float m_HideTimer;
    float m_MoveSpeed;
    float m_SnapAngle;
    float m_TurnSpeed;
    float m_VignetteStrength;

    void Start()
    {
        var xrOrigin = FindFirstObjectByType<XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogError("PlayerTestSceneComfortSettings requires an XR Origin in the scene.", this);
            enabled = false;
            return;
        }

        m_PlayerCamera = xrOrigin.Camera != null ? xrOrigin.Camera : Camera.main;
        m_LeftWrist = FindChildByName(xrOrigin.transform, "Left Controller");
        m_InputManagers = FindObjectsByType<ControllerInputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        m_MoveProviders = FindObjectsByType<DynamicMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        m_SnapTurnProviders = FindObjectsByType<SnapTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        m_ContinuousTurnProviders = FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        m_LocomotionProviders = FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        LoadSettings();
        ApplySettings();
        ApplyTeleportVisualStyle();
        CreateVignette();

        if (m_PlayerCamera == null || m_LeftWrist == null)
        {
            Debug.LogError("The XR Origin needs a Main Camera and a child named 'Left Controller' for the wrist settings menu.", this);
            enabled = false;
            return;
        }

        EnsureXRUIInputModule();
        CreateWristMenu();
    }

    void Update()
    {
        if (m_CanvasRect == null || m_LeftWrist == null || m_PlayerCamera == null)
            return;

        var menuPosition = m_LeftWrist.TransformPoint(m_WristMenuOffset);
        m_CanvasRect.position = menuPosition;
        var directionToCamera = m_PlayerCamera.transform.position - menuPosition;
        if (directionToCamera.sqrMagnitude > 0.0001f)
            m_CanvasRect.rotation = Quaternion.LookRotation(directionToCamera, m_PlayerCamera.transform.up);

        var palmNormal = m_LeftWrist.TransformDirection(m_PalmNormalLocalAxis.normalized);
        var palmIsUp = Vector3.Dot(palmNormal, Vector3.up) >= m_PalmUpThreshold;
        var gazeDirection = (menuPosition - m_PlayerCamera.transform.position).normalized;
        var isLookingAtWrist = Vector3.Dot(m_PlayerCamera.transform.forward, gazeDirection) >= m_GazeThreshold;

        if (palmIsUp && isLookingAtWrist)
        {
            m_HideTimerStarted = false;
            SetMenuVisible(true);
        }
        else if (m_MenuVisible)
        {
            if (!m_HideTimerStarted)
            {
                m_HideTimerStarted = true;
                m_HideTimer = Time.unscaledTime;
            }
            else if (Time.unscaledTime - m_HideTimer >= m_HideGracePeriod)
            {
                SetMenuVisible(false);
            }
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveSettingsNow();
    }

    void OnApplicationQuit()
    {
        SaveSettingsNow();
    }

    void OnDestroy()
    {
        RestoreLocomotionProviders();
    }

    void LoadSettings()
    {
        m_SmoothMovement = PlayerPrefs.GetInt(k_MovementKey, 0) == 1;
        m_SmoothTurning = PlayerPrefs.GetInt(k_TurnKey, 0) == 1;
        m_MoveSpeed = PlayerPrefs.GetFloat(k_MoveSpeedKey, m_MoveProviders.Length > 0 ? m_MoveProviders[0].moveSpeed : 2.5f);
        m_SnapAngle = PlayerPrefs.GetFloat(k_SnapAngleKey, m_SnapTurnProviders.Length > 0 ? m_SnapTurnProviders[0].turnAmount : 45f);
        m_TurnSpeed = PlayerPrefs.GetFloat(k_TurnSpeedKey, m_ContinuousTurnProviders.Length > 0 ? m_ContinuousTurnProviders[0].turnSpeed : 60f);
        m_VignetteEnabled = PlayerPrefs.GetInt(k_VignetteKey, 1) == 1;
        m_VignetteStrength = PlayerPrefs.GetFloat(k_VignetteStrengthKey, 0.55f);
    }

    void ApplySettings()
    {
        foreach (var inputManager in m_InputManagers)
        {
            inputManager.independentTurning = true;
            inputManager.smoothMotionEnabled = m_SmoothMovement;
            inputManager.smoothTurnEnabled = m_SmoothTurning;
        }

        foreach (var moveProvider in m_MoveProviders)
            moveProvider.moveSpeed = m_MoveSpeed;

        foreach (var snapTurnProvider in m_SnapTurnProviders)
        {
            snapTurnProvider.turnAmount = m_SnapAngle;
            snapTurnProvider.enableTurnAround = false;
        }

        foreach (var turnProvider in m_ContinuousTurnProviders)
        {
            turnProvider.turnSpeed = m_TurnSpeed;
            turnProvider.enableTurnAround = false;
        }

        ApplyVignetteSettings();
        UpdateControlStates();
    }

    void ApplyTeleportVisualStyle()
    {
        var lineVisuals = FindObjectsByType<XRInteractorLineVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var lineVisual in lineVisuals)
        {
            var rayInteractor = lineVisual.GetComponent<XRRayInteractor>();
            if (rayInteractor == null || !rayInteractor.gameObject.name.Contains("Teleport"))
                continue;

            lineVisual.validColorGradient = CreateGradient(new Color(0.76f, 0.67f, 0.48f, 0.82f));
            lineVisual.invalidColorGradient = CreateGradient(new Color(0.58f, 0.30f, 0.25f, 0.82f));
            lineVisual.blockedColorGradient = CreateGradient(new Color(0.67f, 0.48f, 0.29f, 0.9f));
        }
    }

    void CreateVignette()
    {
        if (m_PlayerCamera == null || m_VignettePrefab == null)
            return;

        var vignetteObject = Instantiate(m_VignettePrefab, m_PlayerCamera.transform, false);
        vignetteObject.name = "Player Tunneling Vignette";
        m_VignetteController = vignetteObject.GetComponent<TunnelingVignetteController>();
        if (m_VignetteController == null)
            m_VignetteController = vignetteObject.AddComponent<TunnelingVignetteController>();

        m_VignetteProviders.Clear();
        foreach (var locomotionProvider in m_LocomotionProviders)
        {
            if (!(locomotionProvider is ContinuousMoveProvider) &&
                !(locomotionProvider is SnapTurnProvider) &&
                !(locomotionProvider is ContinuousTurnProvider) &&
                !(locomotionProvider is TeleportationProvider))
                continue;

            m_VignetteProviders.Add(new LocomotionVignetteProvider
            {
                locomotionProvider = locomotionProvider,
                enabled = true,
            });
        }

        m_VignetteController.locomotionVignetteProviders = m_VignetteProviders;
        ApplyVignetteSettings();
    }

    void ApplyVignetteSettings()
    {
        if (m_VignetteController == null)
            return;

        var parameters = m_VignetteController.defaultParameters;
        parameters.apertureSize = m_VignetteEnabled ? Mathf.Lerp(1f, 0.35f, m_VignetteStrength) : 1f;
        m_VignetteController.defaultParameters = parameters;
    }

    void EnsureXRUIInputModule()
    {
        var eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("XR UI Event System", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<XRUIInputModule>() != null)
            return;

        foreach (var inputModule in eventSystem.GetComponents<BaseInputModule>())
            inputModule.enabled = false;

        eventSystem.gameObject.AddComponent<XRUIInputModule>();
    }

    void CreateWristMenu()
    {
        var canvasObject = new GameObject("Wrist Settings Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(m_LeftWrist, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = m_PlayerCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;

        m_CanvasRect = canvasObject.GetComponent<RectTransform>();
        m_CanvasRect.sizeDelta = new Vector2(480f, 660f);
        m_CanvasRect.localScale = Vector3.one * 0.0005f;
        m_CanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        m_CanvasGroup.alpha = 0f;
        m_CanvasGroup.interactable = false;
        m_CanvasGroup.blocksRaycasts = false;

        var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = 10f;

        var panel = CreateImage("Panel", m_CanvasRect, Vector2.zero, new Vector2(480f, 660f), k_PanelColor);
        panel.raycastTarget = false;
        CreateText("Title", "FIELD SETTINGS", new Vector2(0f, 305f), new Vector2(440f, 44f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateText("Movement Label", "MOVEMENT", new Vector2(-190f, 255f), new Vector2(400f, 28f), 19, TextAnchor.MiddleLeft, FontStyle.Bold);
        m_TeleportButton = CreateButton("Teleport", "TELEPORT", new Vector2(-108f, 215f), new Vector2(208f, 44f), () => SetMovementMode(false));
        m_SmoothMoveButton = CreateButton("Smooth Move", "WALK", new Vector2(108f, 215f), new Vector2(208f, 44f), () => SetMovementMode(true));
        CreateText("Turning Label", "TURNING", new Vector2(-190f, 155f), new Vector2(400f, 28f), 19, TextAnchor.MiddleLeft, FontStyle.Bold);
        m_SnapTurnButton = CreateButton("Snap Turn", "SNAP", new Vector2(-108f, 115f), new Vector2(208f, 44f), () => SetTurnMode(false));
        m_SmoothTurnButton = CreateButton("Smooth Turn", "SMOOTH", new Vector2(108f, 115f), new Vector2(208f, 44f), () => SetTurnMode(true));

        m_MoveSpeedLabel = CreateText("Move Speed Label", "WALK SPEED", new Vector2(0f, 52f), new Vector2(400f, 28f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
        m_MoveSpeedSlider = CreateSlider("Move Speed", new Vector2(0f, 20f), 0.5f, 3.5f, m_MoveSpeed, value =>
        {
            m_MoveSpeed = value;
            ApplySettings();
            ScheduleSave();
            UpdateValueLabels();
        });

        m_SnapAngleLabel = CreateText("Snap Angle Label", "SNAP ANGLE", new Vector2(0f, -35f), new Vector2(400f, 28f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
        m_SnapAngleSlider = CreateSlider("Snap Angle", new Vector2(0f, -67f), 15f, 90f, m_SnapAngle, value =>
        {
            m_SnapAngle = value;
            ApplySettings();
            ScheduleSave();
            UpdateValueLabels();
        });

        m_TurnSpeedLabel = CreateText("Turn Speed Label", "SMOOTH TURN SPEED", new Vector2(0f, -122f), new Vector2(400f, 28f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
        m_TurnSpeedSlider = CreateSlider("Turn Speed", new Vector2(0f, -154f), 20f, 120f, m_TurnSpeed, value =>
        {
            m_TurnSpeed = value;
            ApplySettings();
            ScheduleSave();
            UpdateValueLabels();
        });

        m_VignetteButton = CreateButton("Vignette", "VIGNETTE: ON", new Vector2(0f, -210f), new Vector2(420f, 42f), ToggleVignette);
        m_VignetteStrengthLabel = CreateText("Vignette Strength Label", "VIGNETTE STRENGTH", new Vector2(0f, -258f), new Vector2(400f, 28f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
        m_VignetteStrengthSlider = CreateSlider("Vignette Strength", new Vector2(0f, -290f), 0f, 0.8f, m_VignetteStrength, value =>
        {
            m_VignetteStrength = value;
            ApplySettings();
            ScheduleSave();
            UpdateValueLabels();
        });

        UpdateControlStates();
        UpdateValueLabels();
        SetMenuVisible(false);
    }

    void SetMovementMode(bool smooth)
    {
        m_SmoothMovement = smooth;
        ApplySettings();
        ScheduleSave();
    }

    void SetTurnMode(bool smooth)
    {
        m_SmoothTurning = smooth;
        ApplySettings();
        ScheduleSave();
    }

    void ToggleVignette()
    {
        m_VignetteEnabled = !m_VignetteEnabled;
        ApplySettings();
        ScheduleSave();
        UpdateControlStates();
    }

    void SetMenuVisible(bool visible)
    {
        if (m_MenuVisible == visible || m_CanvasGroup == null)
            return;

        m_MenuVisible = visible;
        m_CanvasGroup.alpha = visible ? 1f : 0f;
        m_CanvasGroup.interactable = visible;
        m_CanvasGroup.blocksRaycasts = visible;

        if (visible)
            SuppressLocomotionProviders();
        else
            RestoreLocomotionProviders();
    }

    void SuppressLocomotionProviders()
    {
        m_ProviderStates.Clear();
        foreach (var provider in m_LocomotionProviders)
        {
            m_ProviderStates[provider] = provider.enabled;
            provider.enabled = false;
        }
    }

    void RestoreLocomotionProviders()
    {
        foreach (var providerState in m_ProviderStates)
        {
            if (providerState.Key != null)
                providerState.Key.enabled = providerState.Value;
        }

        m_ProviderStates.Clear();
    }

    void UpdateControlStates()
    {
        SetButtonState(m_TeleportButton, !m_SmoothMovement);
        SetButtonState(m_SmoothMoveButton, m_SmoothMovement);
        SetButtonState(m_SnapTurnButton, !m_SmoothTurning);
        SetButtonState(m_SmoothTurnButton, m_SmoothTurning);

        if (m_VignetteButton != null)
        {
            var label = m_VignetteButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = m_VignetteEnabled ? "VIGNETTE: ON" : "VIGNETTE: OFF";
            SetButtonState(m_VignetteButton, m_VignetteEnabled);
        }
    }

    void UpdateValueLabels()
    {
        if (m_MoveSpeedLabel != null)
            m_MoveSpeedLabel.text = $"WALK SPEED   {m_MoveSpeed:0.0} m/s";
        if (m_SnapAngleLabel != null)
            m_SnapAngleLabel.text = $"SNAP ANGLE   {m_SnapAngle:0} degrees";
        if (m_TurnSpeedLabel != null)
            m_TurnSpeedLabel.text = $"SMOOTH TURN SPEED   {m_TurnSpeed:0} degrees/s";
        if (m_VignetteStrengthLabel != null)
            m_VignetteStrengthLabel.text = $"VIGNETTE STRENGTH   {m_VignetteStrength:0.00}";
    }

    void ScheduleSave()
    {
        if (m_SaveRoutine != null)
            StopCoroutine(m_SaveRoutine);
        m_SaveRoutine = StartCoroutine(SaveSettingsAfterDelay());
    }

    IEnumerator SaveSettingsAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        SaveSettingsNow();
        m_SaveRoutine = null;
    }

    void SaveSettingsNow()
    {
        PlayerPrefs.SetInt(k_MovementKey, m_SmoothMovement ? 1 : 0);
        PlayerPrefs.SetInt(k_TurnKey, m_SmoothTurning ? 1 : 0);
        PlayerPrefs.SetFloat(k_MoveSpeedKey, m_MoveSpeed);
        PlayerPrefs.SetFloat(k_SnapAngleKey, m_SnapAngle);
        PlayerPrefs.SetFloat(k_TurnSpeedKey, m_TurnSpeed);
        PlayerPrefs.SetInt(k_VignetteKey, m_VignetteEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(k_VignetteStrengthKey, m_VignetteStrength);
        PlayerPrefs.Save();
    }

    void SetButtonState(Button button, bool selected)
    {
        if (button == null)
            return;

        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = selected ? k_SelectedColor : k_ControlColor;
    }

    Button CreateButton(string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var image = CreateImage(name, m_CanvasRect, position, size, k_ControlColor);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onClick);
        var text = CreateText(name + " Label", label, position, size, 18, TextAnchor.MiddleCenter, FontStyle.Bold);
        text.transform.SetParent(image.transform, false);
        var textRect = text.rectTransform;
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = size;
        text.color = k_TextColor;
        return button;
    }

    Slider CreateSlider(string name, Vector2 position, float minimum, float maximum, float value, UnityEngine.Events.UnityAction<float> onChanged)
    {
        var sliderObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider));
        sliderObject.transform.SetParent(m_CanvasRect, false);
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        SetRect(sliderRect, position, new Vector2(360f, 28f));
        var sliderBackground = sliderObject.GetComponent<Image>();
        sliderBackground.color = new Color(0f, 0f, 0f, 0.01f);
        sliderBackground.raycastTarget = true;

        var track = CreateImage(name + " Track", sliderRect, Vector2.zero, new Vector2(360f, 8f), new Color(0.22f, 0.20f, 0.17f, 1f));
        track.raycastTarget = false;
        var fill = CreateImage(name + " Fill", sliderRect, Vector2.zero, Vector2.zero, k_AccentColor);
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = new Vector2(0f, 0.35f);
        fill.rectTransform.anchorMax = new Vector2(0f, 0.65f);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        var handle = CreateImage(name + " Handle", sliderRect, Vector2.zero, new Vector2(22f, 22f), k_TextColor);
        handle.raycastTarget = false;
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);

        var slider = sliderObject.GetComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = sliderBackground;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    Text CreateText(string name, string content, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, FontStyle style)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(m_CanvasRect, false);
        var textRect = textObject.GetComponent<RectTransform>();
        SetRect(textRect, position, size);

        var text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = k_TextColor;
        text.raycastTarget = false;
        text.text = content;
        return text;
    }

    Image CreateImage(string name, RectTransform parent, Vector2 position, Vector2 size, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var rect = imageObject.GetComponent<RectTransform>();
        SetRect(rect, position, size);
        var image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static Transform FindChildByName(Transform parent, string childName)
    {
        foreach (var child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    static Gradient CreateGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) });
        return gradient;
    }
}