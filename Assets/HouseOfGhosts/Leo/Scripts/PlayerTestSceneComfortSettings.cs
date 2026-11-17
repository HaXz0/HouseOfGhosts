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
    [SerializeField] Vector3 m_ControllerTopLocalAxis = Vector3.up;
    [SerializeField, Range(0f, 1f)] float m_ControllerSideThreshold = 0.65f;
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
    ArmsVRController m_Arms;
    NearFarInteractor m_RightNearFarInteractor;
    XRInteractorLineVisual[] m_MenuRayVisuals;
    CanvasGroup m_CanvasGroup;
    RectTransform m_CanvasRect;
    RectTransform m_MenuContent;
    GameObject m_MainPage;
    GameObject m_ArmPage;
    GameObject m_WristPosePage;
    GameObject m_CalibrationStatusObject;
    Text m_CalibrationStatusText;
    Text m_ArmScaleLabel;
    Text m_LeftArmScaleLabel;
    Text m_RightArmScaleLabel;
    Slider m_LeftArmScaleSlider;
    Slider m_RightArmScaleSlider;
    readonly Slider[] m_WristPoseSliders = new Slider[6];
    readonly Text[] m_WristPoseLabels = new Text[6];
    Coroutine m_CalibrationRoutine;
    Coroutine m_HideCalibrationStatusRoutine;
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
    bool m_IsCalibrating;
    bool m_MenuRayHovering;
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
        m_Arms = xrOrigin.GetComponentInChildren<ArmsVRController>(true);
        if (m_Arms == null || !m_Arms.isActiveAndEnabled)
        {
            BindHandModel(xrOrigin.transform, "Left Controller", "VR_Hand_Blue_Left");
            BindHandModel(xrOrigin.transform, "Right Controller", "VR_Hand_Blue_Right");
        }
        HidePokeVisuals(xrOrigin.transform, "Left Controller");
        HidePokeVisuals(xrOrigin.transform, "Right Controller");
        ConfigureMenuRay(xrOrigin.transform);
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
        if (m_IsCalibrating && m_CalibrationStatusObject != null && m_PlayerCamera != null)
            PlaceCalibrationStatusInFrontOfPlayer();

        if (m_CanvasRect == null || m_LeftWrist == null || m_PlayerCamera == null)
            return;

        var menuPosition = m_LeftWrist.TransformPoint(m_WristMenuOffset);
        m_CanvasRect.position = menuPosition;
        var directionFromCamera = menuPosition - m_PlayerCamera.transform.position;
        if (directionFromCamera.sqrMagnitude > 0.0001f)
            m_CanvasRect.rotation = Quaternion.LookRotation(directionFromCamera, m_PlayerCamera.transform.up);

        var controllerTop = m_LeftWrist.TransformDirection(m_ControllerTopLocalAxis.normalized);
        var controllerFacesRight = Vector3.Dot(controllerTop, m_PlayerCamera.transform.right) >= m_ControllerSideThreshold;
        var gazeDirection = (menuPosition - m_PlayerCamera.transform.position).normalized;
        var isLookingAtWrist = Vector3.Dot(m_PlayerCamera.transform.forward, gazeDirection) >= m_GazeThreshold;

        if (controllerFacesRight && isLookingAtWrist)
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
        {
            CancelCalibration();
            SaveSettingsNow();
        }
    }

    void OnApplicationQuit()
    {
        SaveSettingsNow();
    }

    void OnDisable()
    {
        if (m_CalibrationRoutine != null)
            StopCoroutine(m_CalibrationRoutine);
        if (m_HideCalibrationStatusRoutine != null)
            StopCoroutine(m_HideCalibrationStatusRoutine);
        m_CalibrationRoutine = null;
        m_HideCalibrationStatusRoutine = null;
        m_IsCalibrating = false;
        if (m_CalibrationStatusObject != null)
            m_CalibrationStatusObject.SetActive(false);
        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.interactable = false;
            m_CanvasGroup.blocksRaycasts = false;
        }
        m_MenuVisible = false;
        m_MenuRayHovering = false;
        UpdateMenuRayVisuals();
        RestoreLocomotionProviders();
    }

    void OnDestroy()
    {
        if (m_CalibrationStatusObject != null)
            Destroy(m_CalibrationStatusObject);
        if (m_RightNearFarInteractor != null)
        {
            m_RightNearFarInteractor.uiHoverEntered.RemoveListener(OnMenuUIHoverEntered);
            m_RightNearFarInteractor.uiHoverExited.RemoveListener(OnMenuUIHoverExited);
        }

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
        m_MainPage = CreateMenuPage("Movement Settings");
        m_MenuContent = m_MainPage.GetComponent<RectTransform>();
        CreateText("Title", "FIELD SETTINGS", new Vector2(-72f, 305f), new Vector2(285f, 44f), 27, TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateButton("Open Arm Settings", "CALIBRATE ARMS", new Vector2(158f, 305f), new Vector2(174f, 40f), () => SetArmPage(true));
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

        m_ArmPage = CreateMenuPage("Arm Calibration");
        m_MenuContent = m_ArmPage.GetComponent<RectTransform>();
        CreateText("Arm Title", "ARM CALIBRATION", new Vector2(-38f, 305f), new Vector2(320f, 44f), 25, TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateButton("Back to Settings", "BACK", new Vector2(-175f, 305f), new Vector2(90f, 40f), () => SetArmPage(false));
        CreateButton("Open Wrist Pose", "WRIST POSE", new Vector2(150f, 305f), new Vector2(130f, 40f), () => SetWristPosePage(true));
        var leftStart = m_Arms != null ? m_Arms.LeftArmScale : 1f;
        var rightStart = m_Arms != null ? m_Arms.RightArmScale : 1f;
        m_LeftArmScaleLabel = CreateText("Left Arm Length Label", "LEFT ARM", new Vector2(0f, 230f), new Vector2(400f, 30f), 19, TextAnchor.MiddleLeft, FontStyle.Normal);
        m_LeftArmScaleSlider = CreateSlider("Left Arm Length", new Vector2(0f, 195f), ArmsVRController.MinimumArmScale, ArmsVRController.MaximumArmScale, leftStart, SetLeftArmScale);
        m_RightArmScaleLabel = CreateText("Right Arm Length Label", "RIGHT ARM", new Vector2(0f, 140f), new Vector2(400f, 30f), 19, TextAnchor.MiddleLeft, FontStyle.Normal);
        m_RightArmScaleSlider = CreateSlider("Right Arm Length", new Vector2(0f, 105f), ArmsVRController.MinimumArmScale, ArmsVRController.MaximumArmScale, rightStart, SetRightArmScale);
        m_ArmScaleLabel = CreateText("Calibration Value Label", "T-pose estimates reach; adjust for comfort", new Vector2(0f, 52f), new Vector2(420f, 36f), 17, TextAnchor.MiddleCenter, FontStyle.Normal);
        var calibrateButton = CreateButton("Calibrate Arms", "CALIBRATE ARMS", new Vector2(0f, -25f), new Vector2(360f, 48f), StartArmCalibration);
        var resetButton = CreateButton("Reset Arms", "RESET ARMS", new Vector2(0f, -90f), new Vector2(360f, 48f), ResetArmCalibration);
        CreateButton("Cancel Calibration", "CANCEL CALIBRATION", new Vector2(0f, -155f), new Vector2(360f, 48f), CancelCalibration);
        if (m_Arms == null || !m_Arms.isActiveAndEnabled)
        {
            calibrateButton.interactable = false;
            resetButton.interactable = false;
            m_LeftArmScaleSlider.interactable = false;
            m_RightArmScaleSlider.interactable = false;
        }
        UpdateArmScaleLabel();
        CreateWristPosePage();
        SetArmPage(false);

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

    GameObject CreateMenuPage(string pageName)
    {
        var page = new GameObject(pageName, typeof(RectTransform));
        page.transform.SetParent(m_CanvasRect, false);
        SetRect(page.GetComponent<RectTransform>(), Vector2.zero, m_CanvasRect.sizeDelta);
        return page;
    }

    void SetArmPage(bool showArms)
    {
        m_MainPage.SetActive(!showArms);
        m_ArmPage.SetActive(showArms);
        if (m_WristPosePage != null)
            m_WristPosePage.SetActive(false);
    }

    void SetWristPosePage(bool showWristPose)
    {
        m_ArmPage.SetActive(!showWristPose);
        m_WristPosePage.SetActive(showWristPose);
        if (showWristPose)
            RefreshWristPoseControls();
    }

    void SetLeftArmScale(float value)
    {
        if (m_Arms == null || !m_Arms.isActiveAndEnabled)
            return;

        m_Arms.SetLeftArmScale(value);
        UpdateArmScaleLabel();
        ScheduleSave();
    }

    void SetRightArmScale(float value)
    {
        if (m_Arms == null || !m_Arms.isActiveAndEnabled)
            return;

        m_Arms.SetRightArmScale(value);
        UpdateArmScaleLabel();
        ScheduleSave();
    }

    void ResetArmCalibration()
    {
        CancelCalibration();
        if (m_Arms == null || !m_Arms.isActiveAndEnabled)
            return;

        m_Arms.ResetArmScales();
        m_Arms.SetWristRotationOffsets(Vector3.zero, Vector3.zero);
        m_LeftArmScaleSlider.SetValueWithoutNotify(1f);
        m_RightArmScaleSlider.SetValueWithoutNotify(1f);
        RefreshWristPoseControls();
        UpdateArmScaleLabel();
        ScheduleSave();
    }

    void UpdateArmScaleLabel()
    {
        if (m_ArmScaleLabel != null)
            m_ArmScaleLabel.text = m_Arms != null && m_Arms.isActiveAndEnabled
                ? "T-pose gives an estimate; use sliders to fine-tune"
                : "ARMS UNAVAILABLE";
        if (m_LeftArmScaleLabel != null && m_Arms != null)
            m_LeftArmScaleLabel.text = $"LEFT ARM   {m_Arms.LeftArmScale * 100f:0}%";
        if (m_RightArmScaleLabel != null && m_Arms != null)
            m_RightArmScaleLabel.text = $"RIGHT ARM   {m_Arms.RightArmScale * 100f:0}%";
    }

    void CreateWristPosePage()
    {
        m_WristPosePage = CreateMenuPage("Wrist Pose Adjustment");
        m_MenuContent = m_WristPosePage.GetComponent<RectTransform>();
        CreateText("Wrist Pose Title", "WRIST POSE", new Vector2(0f, 305f), new Vector2(360f, 42f), 25, TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateButton("Back to Arm Calibration", "BACK", new Vector2(-175f, 305f), new Vector2(90f, 40f), () => SetWristPosePage(false));
        CreateText("Left Wrist Heading", "LEFT WRIST", new Vector2(0f, 250f), new Vector2(400f, 28f), 19, TextAnchor.MiddleLeft, FontStyle.Bold);
        var axisNames = new[] { "PITCH", "YAW", "ROLL" };
        var savedLeftRotation = m_Arms != null ? m_Arms.LeftWristRotationOffset : Vector3.zero;
        var savedRightRotation = m_Arms != null ? m_Arms.RightWristRotationOffset : Vector3.zero;
        var wristValues = new[] { savedLeftRotation.x, savedLeftRotation.y, savedLeftRotation.z, savedRightRotation.x, savedRightRotation.y, savedRightRotation.z };
        var rows = new[] { 220f, 165f, 110f, -5f, -60f, -115f };
        for (var index = 0; index < 6; index++)
        {
            if (index == 3)
                CreateText("Right Wrist Heading", "RIGHT WRIST", new Vector2(0f, 40f), new Vector2(400f, 28f), 19, TextAnchor.MiddleLeft, FontStyle.Bold);
            var side = index < 3 ? "LEFT" : "RIGHT";
            var axis = axisNames[index % 3];
            var row = rows[index];
            m_WristPoseLabels[index] = CreateText(side + " " + axis + " Label", side + " " + axis, new Vector2(0f, row), new Vector2(400f, 24f), 16, TextAnchor.MiddleLeft, FontStyle.Normal);
            var capturedIndex = index;
            m_WristPoseSliders[index] = CreateSlider(side + " " + axis, new Vector2(0f, row - 25f), -90f, 90f, wristValues[index], value => SetWristPoseValue(capturedIndex, value));
        }
        var resetButton = CreateButton("Reset Wrist Pose", "RESET WRIST POSE", new Vector2(0f, -230f), new Vector2(360f, 44f), () => SetWristRotations(Vector3.zero, Vector3.zero));
        if (m_Arms == null || !m_Arms.isActiveAndEnabled)
        {
            resetButton.interactable = false;
            foreach (var slider in m_WristPoseSliders)
                slider.interactable = false;
        }
        RefreshWristPoseControls();
    }

    void SetWristPoseValue(int index, float value)
    {
        if (m_Arms == null || !m_Arms.isActiveAndEnabled)
            return;

        var left = m_Arms.LeftWristRotationOffset;
        var right = m_Arms.RightWristRotationOffset;
        var angles = index < 3 ? left : right;
        var axis = index % 3;
        if (axis == 0) angles.x = value;
        else if (axis == 1) angles.y = value;
        else angles.z = value;
        if (index < 3) left = angles;
        else right = angles;
        SetWristRotations(left, right);
    }

    void SetWristRotations(Vector3 left, Vector3 right)
    {
        if (m_Arms == null || !m_Arms.isActiveAndEnabled)
            return;

        m_Arms.SetWristRotationOffsets(left, right);
        RefreshWristPoseControls();
        ScheduleSave();
    }

    void RefreshWristPoseControls()
    {
        if (m_Arms == null || !m_Arms.isActiveAndEnabled)
            return;

        var values = new[]
        {
            m_Arms.LeftWristRotationOffset.x, m_Arms.LeftWristRotationOffset.y, m_Arms.LeftWristRotationOffset.z,
            m_Arms.RightWristRotationOffset.x, m_Arms.RightWristRotationOffset.y, m_Arms.RightWristRotationOffset.z,
        };
        for (var index = 0; index < values.Length; index++)
        {
            if (m_WristPoseSliders[index] != null)
                m_WristPoseSliders[index].SetValueWithoutNotify(values[index]);
            if (m_WristPoseLabels[index] != null)
                m_WristPoseLabels[index].text = $"{(index < 3 ? "LEFT" : "RIGHT")} {(index % 3 == 0 ? "PITCH" : index % 3 == 1 ? "YAW" : "ROLL")}   {values[index]:0} deg";
        }
    }

    void StartArmCalibration()
    {
        if (m_IsCalibrating || m_Arms == null || !m_Arms.isActiveAndEnabled)
            return;

        if (m_HideCalibrationStatusRoutine != null)
        {
            StopCoroutine(m_HideCalibrationStatusRoutine);
            m_HideCalibrationStatusRoutine = null;
        }
        m_IsCalibrating = true;
        if (!m_MenuVisible)
            SuppressLocomotionProviders();
        ShowCalibrationStatus();
        m_CalibrationRoutine = StartCoroutine(CalibrateArms());
    }

    IEnumerator CalibrateArms()
    {
        const float preparationTime = 5f;
        const float sampleTime = 1.25f;
        const float maximumDrift = 0.06f;
        var elapsed = 0f;
        while (elapsed < preparationTime)
        {
            SetCalibrationStatus($"ARM CALIBRATION\nStretch both arms sideways\nHold still in {Mathf.CeilToInt(preparationTime - elapsed)}");
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        var sampleCount = 0;
        var leftScaleTotal = 0f;
        var rightScaleTotal = 0f;
        var firstLeft = Vector3.zero;
        var firstRight = Vector3.zero;
        while (elapsed < sampleTime)
        {
            if (m_Arms == null)
            {
                FinishCalibration("ARMS UNAVAILABLE");
                yield break;
            }
            if (!m_Arms.TryGetCalibrationSample(out var leftScale, out var rightScale, out var leftWrist, out var rightWrist, out var error))
            {
                FinishCalibration("CALIBRATION FAILED\n" + error);
                yield break;
            }

            if (sampleCount == 0)
            {
                firstLeft = leftWrist;
                firstRight = rightWrist;
            }
            else if (Vector3.Distance(firstLeft, leftWrist) > maximumDrift || Vector3.Distance(firstRight, rightWrist) > maximumDrift)
            {
                FinishCalibration("CALIBRATION FAILED\nHold both arms still");
                yield break;
            }

            SetCalibrationStatus("ARM CALIBRATION\nHold still...");
            leftScaleTotal += leftScale;
            rightScaleTotal += rightScale;
            sampleCount++;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (sampleCount == 0)
        {
            FinishCalibration("CALIBRATION FAILED\nNo tracking samples");
            yield break;
        }
        if (m_Arms == null || !m_Arms.isActiveAndEnabled)
        {
            FinishCalibration("ARMS UNAVAILABLE");
            yield break;
        }

        m_Arms.SetArmScales(leftScaleTotal / sampleCount, rightScaleTotal / sampleCount);
        m_LeftArmScaleSlider.SetValueWithoutNotify(m_Arms.LeftArmScale);
        m_RightArmScaleSlider.SetValueWithoutNotify(m_Arms.RightArmScale);
        UpdateArmScaleLabel();
        ScheduleSave();
        FinishCalibration("CALIBRATION COMPLETE\nEstimated reach - adjust to fit");
    }

    void CancelCalibration()
    {
        if (!m_IsCalibrating)
            return;

        if (m_CalibrationRoutine != null)
            StopCoroutine(m_CalibrationRoutine);
        FinishCalibration("CALIBRATION CANCELLED");
    }

    void FinishCalibration(string message)
    {
        m_IsCalibrating = false;
        m_CalibrationRoutine = null;
        SetCalibrationStatus(message);
        if (!m_MenuVisible)
            RestoreLocomotionProviders();
        m_HideCalibrationStatusRoutine = StartCoroutine(HideCalibrationStatus());
    }

    IEnumerator HideCalibrationStatus()
    {
        yield return new WaitForSecondsRealtime(3f);
        if (m_CalibrationStatusObject != null)
            m_CalibrationStatusObject.SetActive(false);
        m_HideCalibrationStatusRoutine = null;
    }

    void ShowCalibrationStatus()
    {
        if (m_CalibrationStatusObject == null)
        {
            m_CalibrationStatusObject = new GameObject("Arm Calibration Status", typeof(RectTransform), typeof(Canvas));
            var statusRect = m_CalibrationStatusObject.GetComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(700f, 240f);
            statusRect.localScale = Vector3.one * 0.001f;
            var canvas = m_CalibrationStatusObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = m_PlayerCamera;
            var background = CreateImage("Background", statusRect, Vector2.zero, statusRect.sizeDelta, k_PanelColor);
            background.raycastTarget = false;
            var textObject = new GameObject("Status Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(statusRect, false);
            SetRect(textObject.GetComponent<RectTransform>(), Vector2.zero, new Vector2(660f, 210f));
            m_CalibrationStatusText = textObject.GetComponent<Text>();
            m_CalibrationStatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_CalibrationStatusText.fontSize = 34;
            m_CalibrationStatusText.alignment = TextAnchor.MiddleCenter;
            m_CalibrationStatusText.color = k_TextColor;
            m_CalibrationStatusText.raycastTarget = false;
        }

        m_CalibrationStatusObject.SetActive(true);
        PlaceCalibrationStatusInFrontOfPlayer();
    }

    void PlaceCalibrationStatusInFrontOfPlayer()
    {
        var forward = Vector3.ProjectOnPlane(m_PlayerCamera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        forward.Normalize();
        m_CalibrationStatusObject.transform.SetPositionAndRotation(
            m_PlayerCamera.transform.position + forward * 1.3f,
            Quaternion.LookRotation(forward, Vector3.up));
    }

    void SetCalibrationStatus(string message)
    {
        if (m_CalibrationStatusText != null)
            m_CalibrationStatusText.text = message;
    }

    void SetMenuVisible(bool visible)
    {
        if (m_MenuVisible == visible || m_CanvasGroup == null)
            return;

        m_MenuVisible = visible;
        m_CanvasGroup.alpha = visible ? 1f : 0f;
        m_CanvasGroup.interactable = visible;
        m_CanvasGroup.blocksRaycasts = visible;
        if (!visible)
            m_MenuRayHovering = false;
        UpdateMenuRayVisuals();

        if (!m_IsCalibrating)
        {
            if (visible)
                SuppressLocomotionProviders();
            else
                RestoreLocomotionProviders();
        }
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
        var image = CreateImage(name, m_MenuContent, position, size, k_ControlColor);
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
        sliderObject.transform.SetParent(m_MenuContent, false);
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
        textObject.transform.SetParent(m_MenuContent, false);
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

    void BindHandModel(Transform xrOrigin, string controllerName, string handName)
    {
        var controller = FindChildByName(xrOrigin, controllerName);
        var hand = GameObject.Find(handName);
        if (controller == null || hand == null)
        {
            Debug.LogWarning($"Could not bind {handName} to {controllerName}; check that both objects exist in PlayerTestScene.", this);
            return;
        }

        hand.transform.SetParent(controller, false);
        hand.transform.localPosition = Vector3.zero;
        hand.transform.localRotation = Quaternion.identity;
        hand.transform.localScale = Vector3.one;
    }

    void HidePokeVisuals(Transform xrOrigin, string controllerName)
    {
        var controller = FindChildByName(xrOrigin, controllerName);
        if (controller == null)
            return;
        var pokeInteractor = FindChildByName(controller, "Poke Interactor");
        if (pokeInteractor != null)
        {
            foreach (var renderer in pokeInteractor.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }
    }

    void ConfigureMenuRay(Transform xrOrigin)
    {
        foreach (var nearFarInteractor in xrOrigin.GetComponentsInChildren<NearFarInteractor>(true))
        {
            foreach (var lineVisual in nearFarInteractor.GetComponentsInChildren<XRInteractorLineVisual>(true))
                lineVisual.enabled = false;
        }

        var rightController = FindChildByName(xrOrigin, "Right Controller");
        if (rightController == null)
            return;

        m_RightNearFarInteractor = rightController.GetComponentInChildren<NearFarInteractor>(true);
        if (m_RightNearFarInteractor == null)
            return;

        m_MenuRayVisuals = m_RightNearFarInteractor.GetComponentsInChildren<XRInteractorLineVisual>(true);
        m_RightNearFarInteractor.uiHoverEntered.AddListener(OnMenuUIHoverEntered);
        m_RightNearFarInteractor.uiHoverExited.AddListener(OnMenuUIHoverExited);
        UpdateMenuRayVisuals();
    }

    void OnMenuUIHoverEntered(UIHoverEventArgs args)
    {
        m_MenuRayHovering = true;
        UpdateMenuRayVisuals();
    }

    void OnMenuUIHoverExited(UIHoverEventArgs args)
    {
        m_MenuRayHovering = false;
        UpdateMenuRayVisuals();
    }

    void UpdateMenuRayVisuals()
    {
        if (m_MenuRayVisuals == null)
            return;

        var shouldShow = m_MenuVisible && m_MenuRayHovering;
        foreach (var lineVisual in m_MenuRayVisuals)
        {
            if (lineVisual != null)
                lineVisual.enabled = shouldShow;
        }
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