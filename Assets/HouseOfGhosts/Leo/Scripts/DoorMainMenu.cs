using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// World-space main menu on the front door of the MainMenu scene.
/// Buttons and sliders call the public methods below through their OnClick / OnValueChanged events.
/// </summary>
[DisallowMultipleComponent]
public sealed class DoorMainMenu : MonoBehaviour
{
    [Header("Scene Loading")]
    [Tooltip("Scene loaded by the Play button. It must be listed in File > Build Profiles > Scene List.")]
    [SerializeField] string gameplaySceneName = "PlayerTestScene";
    [Tooltip("Disabled while the gameplay scene loads so Play cannot be pressed twice.")]
    [SerializeField] CanvasGroup menuCanvasGroup;

    [Header("Player")]
    [Tooltip("Tunneling vignette prefab placed on the player camera (comfort option).")]
    [SerializeField] GameObject vignettePrefab;
    [Tooltip("XR camera. Uses Camera.main when empty.")]
    [SerializeField] Transform playerCamera;
    [Tooltip("ArmsVR on the XR Origin. Searched once on Start when empty.")]
    [SerializeField] ArmsVRController arms;

    [Header("Pages")]
    [SerializeField] GameObject mainPage;
    [SerializeField] GameObject settingsPage;
    [SerializeField] GameObject comfortPage;
    [SerializeField] GameObject audioPage;
    [SerializeField] GameObject armsPage;
    [SerializeField] GameObject creditsPage;

    [Header("Movement & Comfort")]
    [SerializeField] Button teleportButton;
    [SerializeField] Button walkButton;
    [SerializeField] Button snapTurnButton;
    [SerializeField] Button smoothTurnButton;
    [SerializeField] Button vignetteButton;
    [SerializeField] TMP_Text vignetteButtonLabel;
    [SerializeField] Slider moveSpeedSlider;
    [SerializeField] TMP_Text moveSpeedLabel;
    [SerializeField] Slider snapAngleSlider;
    [SerializeField] TMP_Text snapAngleLabel;
    [SerializeField] Slider turnSpeedSlider;
    [SerializeField] TMP_Text turnSpeedLabel;
    [SerializeField] Slider vignetteStrengthSlider;
    [SerializeField] TMP_Text vignetteStrengthLabel;

    [Header("Audio")]
    [SerializeField] Slider masterVolumeSlider;
    [SerializeField] TMP_Text masterVolumeLabel;

    [Header("Arm Calibration")]
    [FormerlySerializedAs("leftArmSlider")]
    [SerializeField] Slider heightSlider;
    [FormerlySerializedAs("leftArmLabel")]
    [SerializeField] TMP_Text heightLabel;
    [SerializeField] TMP_Text armStatusLabel;
    [SerializeField] Button calibrateArmsButton;
    [SerializeField] Button resetArmsButton;
    [SerializeField] Button cancelCalibrationButton;

    [Header("Colors")]
    [SerializeField] Color normalButtonColor = new Color(0.16f, 0.15f, 0.13f, 1f);
    [SerializeField] Color selectedButtonColor = new Color(0.43f, 0.34f, 0.21f, 1f);

    const string k_ArmHint = "Stand up straight, look ahead and press CALIBRATE.\nFine-tune your height with the slider.";

    PlayerComfortRig rig;
    ComfortSettings settings;
    Coroutine saveRoutine;
    Coroutine calibrationRoutine;
    bool isLoading;
    bool isInitializing;

    bool ArmsAvailable => arms != null && arms.isActiveAndEnabled;

    void Start()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;
        if (arms == null)
            arms = FindFirstObjectByType<ArmsVRController>();

        rig = PlayerComfortRig.FindInScene();
        settings = rig.LoadSettings();
        rig.CreateVignette(vignettePrefab, playerCamera, settings);
        rig.Apply(settings);

        // Changing a slider's range can clamp its value and fire OnValueChanged; ignore those calls.
        isInitializing = true;
        InitializeSliders();
        isInitializing = false;

        RefreshComfortControls();
        RefreshAudioControls();
        SetArmStatus(k_ArmHint);
        RefreshArmControls();
        ShowMainPage();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
            return;

        CancelArmCalibration();
        SaveNow();
    }

    void OnDisable()
    {
        if (calibrationRoutine != null)
        {
            StopCoroutine(calibrationRoutine);
            calibrationRoutine = null;
        }
        rig?.RestoreLocomotion();
        SaveNow();
    }

    // ---------- Pages ----------

    public void ShowMainPage() => ShowPage(mainPage);
    public void ShowSettingsPage() => ShowPage(settingsPage);
    public void ShowComfortPage() => ShowPage(comfortPage);
    public void ShowAudioPage() => ShowPage(audioPage);
    public void ShowCreditsPage() => ShowPage(creditsPage);

    public void ShowArmsPage()
    {
        RefreshArmControls();
        ShowPage(armsPage);
    }

    void ShowPage(GameObject page)
    {
        if (page != armsPage)
            CancelArmCalibration();

        SetPageActive(mainPage, page);
        SetPageActive(settingsPage, page);
        SetPageActive(comfortPage, page);
        SetPageActive(audioPage, page);
        SetPageActive(armsPage, page);
        SetPageActive(creditsPage, page);
    }

    static void SetPageActive(GameObject page, GameObject visiblePage)
    {
        if (page != null)
            page.SetActive(page == visiblePage);
    }

    // ---------- Main buttons ----------

    public void Play()
    {
        if (isLoading)
            return;

        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError($"Cannot load '{gameplaySceneName}'. Check the scene name and that it is in the Build Profiles scene list.", this);
            return;
        }

        isLoading = true;
        CancelArmCalibration();
        SaveNow();
        if (menuCanvasGroup != null)
            menuCanvasGroup.interactable = false;
        SceneManager.LoadSceneAsync(gameplaySceneName);
    }

    public void Quit()
    {
        SaveNow();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Movement & Comfort ----------

    public void SetTeleportMovement() => SetSmoothMovement(false);
    public void SetWalkMovement() => SetSmoothMovement(true);
    public void SetSnapTurn() => SetSmoothTurning(false);
    public void SetSmoothTurn() => SetSmoothTurning(true);

    public void ToggleVignette()
    {
        settings.VignetteEnabled = !settings.VignetteEnabled;
        ApplyComfortSettings();
    }

    public void SetMoveSpeed(float value)
    {
        if (isInitializing)
            return;
        settings.MoveSpeed = value;
        ApplyComfortSettings();
    }

    public void SetSnapAngle(float value)
    {
        if (isInitializing)
            return;
        settings.SnapAngle = value;
        ApplyComfortSettings();
    }

    public void SetTurnSpeed(float value)
    {
        if (isInitializing)
            return;
        settings.TurnSpeed = value;
        ApplyComfortSettings();
    }

    public void SetVignetteStrength(float value)
    {
        if (isInitializing)
            return;
        settings.VignetteStrength = value;
        ApplyComfortSettings();
    }

    void SetSmoothMovement(bool smooth)
    {
        settings.SmoothMovement = smooth;
        ApplyComfortSettings();
    }

    void SetSmoothTurning(bool smooth)
    {
        settings.SmoothTurning = smooth;
        ApplyComfortSettings();
    }

    void ApplyComfortSettings()
    {
        rig.Apply(settings);
        RefreshComfortControls();
        ScheduleSave();
    }

    void RefreshComfortControls()
    {
        SetButtonSelected(teleportButton, !settings.SmoothMovement);
        SetButtonSelected(walkButton, settings.SmoothMovement);
        SetButtonSelected(snapTurnButton, !settings.SmoothTurning);
        SetButtonSelected(smoothTurnButton, settings.SmoothTurning);
        SetButtonSelected(vignetteButton, settings.VignetteEnabled);
        SetText(vignetteButtonLabel, settings.VignetteEnabled ? "COMFORT VIGNETTE: ON" : "COMFORT VIGNETTE: OFF");

        SetText(moveSpeedLabel, $"WALK SPEED   {settings.MoveSpeed:0.0} m/s");
        SetText(snapAngleLabel, $"SNAP ANGLE   {settings.SnapAngle:0} deg");
        SetText(turnSpeedLabel, $"SMOOTH TURN SPEED   {settings.TurnSpeed:0} deg/s");
        SetText(vignetteStrengthLabel, $"VIGNETTE STRENGTH   {settings.VignetteStrength * 100f:0}%");
        if (vignetteStrengthSlider != null)
            vignetteStrengthSlider.interactable = settings.VignetteEnabled;
    }

    // ---------- Audio ----------

    public void SetMasterVolume(float value)
    {
        if (isInitializing)
            return;
        AudioVolumeSettings.SetMasterVolume(value);
        RefreshAudioControls();
        ScheduleSave();
    }

    void RefreshAudioControls()
    {
        SetText(masterVolumeLabel, $"MASTER VOLUME   {AudioVolumeSettings.MasterVolume * 100f:0}%");
    }

    // ---------- Arm Calibration ----------

    /// <summary>Height slider callback; the slider value is in centimeters.</summary>
    public void SetPlayerHeight(float centimeters)
    {
        if (isInitializing || !ArmsAvailable)
            return;

        arms.SetPlayerHeight(centimeters / 100f);
        RefreshArmControls();
        ScheduleSave();
    }

    public void StartArmCalibration()
    {
        if (calibrationRoutine != null || !ArmsAvailable)
            return;

        // Locomotion is paused so the player cannot move or turn while standing still for the measurement.
        rig.SuppressLocomotion();
        calibrationRoutine = StartCoroutine(ArmCalibration.Run(arms, SetArmStatus, OnArmCalibrationApplied, FinishArmCalibration));
        RefreshArmButtons();
    }

    public void CancelArmCalibration()
    {
        if (calibrationRoutine == null)
            return;

        StopCoroutine(calibrationRoutine);
        FinishArmCalibration("CALIBRATION CANCELLED");
    }

    public void ResetArms()
    {
        CancelArmCalibration();
        if (!ArmsAvailable)
            return;

        arms.ResetPlayerHeight();
        arms.SetWristRotationOffsets(Vector3.zero, Vector3.zero);
        RefreshArmControls();
        SetArmStatus("HEIGHT RESET");
        ScheduleSave();
    }

    void OnArmCalibrationApplied()
    {
        RefreshArmControls();
        ScheduleSave();
    }

    void FinishArmCalibration(string message)
    {
        calibrationRoutine = null;
        rig.RestoreLocomotion();
        SetArmStatus(message);
        RefreshArmButtons();
    }

    void RefreshArmControls()
    {
        var available = ArmsAvailable;
        if (heightSlider != null)
        {
            heightSlider.interactable = available;
            if (available)
                heightSlider.SetValueWithoutNotify(arms.PlayerHeight * 100f);
        }

        if (!available)
            SetArmStatus("ARMS UNAVAILABLE");
        RefreshArmLabels();
        RefreshArmButtons();
    }

    void RefreshArmLabels()
    {
        var available = ArmsAvailable;
        SetText(heightLabel, available ? $"HEIGHT   {ArmCalibration.FormatHeight(arms.PlayerHeight)}" : "HEIGHT");
    }

    void RefreshArmButtons()
    {
        var available = ArmsAvailable;
        var calibrating = calibrationRoutine != null;
        if (calibrateArmsButton != null)
            calibrateArmsButton.interactable = available && !calibrating;
        if (resetArmsButton != null)
            resetArmsButton.interactable = available;
        if (cancelCalibrationButton != null)
            cancelCalibrationButton.interactable = calibrating;
    }

    void SetArmStatus(string message)
    {
        SetText(armStatusLabel, message);
    }

    void SetArmStatus(string message, float progress)
    {
        SetArmStatus(progress >= 0f ? $"{message}\n{Mathf.RoundToInt(progress * 100f)}%" : message);
    }

    // ---------- Helpers ----------

    void InitializeSliders()
    {
        InitializeSlider(moveSpeedSlider, ComfortSettings.MinMoveSpeed, ComfortSettings.MaxMoveSpeed, settings.MoveSpeed);
        InitializeSlider(snapAngleSlider, ComfortSettings.MinSnapAngle, ComfortSettings.MaxSnapAngle, settings.SnapAngle);
        InitializeSlider(turnSpeedSlider, ComfortSettings.MinTurnSpeed, ComfortSettings.MaxTurnSpeed, settings.TurnSpeed);
        InitializeSlider(vignetteStrengthSlider, ComfortSettings.MinVignetteStrength, ComfortSettings.MaxVignetteStrength, settings.VignetteStrength);
        InitializeSlider(masterVolumeSlider, 0f, 1f, AudioVolumeSettings.MasterVolume);
        InitializeSlider(heightSlider, ArmsVRController.MinimumHeight * 100f, ArmsVRController.MaximumHeight * 100f, ArmsVRController.DefaultHeight * 100f);
        if (heightSlider != null)
            heightSlider.wholeNumbers = true;
    }

    static void InitializeSlider(Slider slider, float minimum, float maximum, float value)
    {
        if (slider == null)
            return;

        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.SetValueWithoutNotify(value);
    }

    void SetButtonSelected(Button button, bool selected)
    {
        if (button != null && button.targetGraphic != null)
            button.targetGraphic.color = selected ? selectedButtonColor : normalButtonColor;
    }

    static void SetText(TMP_Text label, string text)
    {
        if (label != null)
            label.text = text;
    }

    void ScheduleSave()
    {
        if (saveRoutine != null)
            StopCoroutine(saveRoutine);
        saveRoutine = StartCoroutine(SaveAfterDelay());
    }

    IEnumerator SaveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        saveRoutine = null;
        SaveNow();
    }

    void SaveNow()
    {
        // ComfortSettings.Save also flushes the arm scale and volume keys to disk.
        settings?.Save();
    }
}
