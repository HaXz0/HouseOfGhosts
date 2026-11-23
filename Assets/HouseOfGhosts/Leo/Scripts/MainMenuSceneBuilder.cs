#if UNITY_EDITOR
using System;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Editor tool: Tools > House of Ghosts > Build Main Menu.
/// Adds the XR player, UI event system, teleportable floor and the door main menu to the open MainMenu scene.
/// Everything is created as normal, editable GameObjects; save the scene afterwards.
/// </summary>
static class MainMenuSceneBuilder
{
    const string ScenePath = "Assets/HouseOfGhosts/Scenes/LeoScenes/MainMenu.unity";
    const string XROriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string VignettePrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/TunnelingVignette/TunnelingVignette.prefab";
    const string MenuObjectName = "Door Main Menu";
    const string EnvironmentName = "GreyboxedEnviroment";

    // Player starts on the floor facing the house (+Z).
    static readonly Vector3 k_SpawnPosition = new Vector3(0f, 0.5f, -4f);

    // Canvas size in pixels; 1 pixel = 1 mm in the world.
    static readonly Vector2 k_CanvasSize = new Vector2(1000f, 860f);
    const float k_CanvasScale = 0.001f;
    // Menu centre height above the bottom edge of the door, so it is readable from the floor below the steps.
    const float k_MenuHeightAboveDoorBottom = 0.68f;
    static readonly Vector3 k_FallbackMenuPosition = new Vector3(-0.05f, 2.6f, 14.09f);

    static readonly Color k_PanelColor = new Color(0.055f, 0.052f, 0.047f, 0.97f);
    static readonly Color k_ControlColor = new Color(0.16f, 0.15f, 0.13f, 1f);
    static readonly Color k_TrackColor = new Color(0.22f, 0.20f, 0.17f, 1f);
    static readonly Color k_TextColor = new Color(0.89f, 0.84f, 0.74f, 1f);
    static readonly Color k_AccentColor = new Color(0.72f, 0.58f, 0.36f, 1f);
    static readonly Color k_HintColor = new Color(0.62f, 0.58f, 0.51f, 1f);

    [MenuItem("Tools/House of Ghosts/Build Main Menu")]
    static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before building the main menu.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning("Open the MainMenu scene (and make it the active scene) before building the main menu.");
            return;
        }

        if (Object.FindFirstObjectByType<DoorMainMenu>(FindObjectsInactive.Include) != null)
        {
            Debug.LogWarning("MainMenu already contains a Door Main Menu; nothing was changed. Delete it first if you want to rebuild it.");
            return;
        }

        var originPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
        var vignettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VignettePrefabPath);
        if (originPrefab == null || vignettePrefab == null)
        {
            Debug.LogError("Could not find the XR Interaction Toolkit Starter Assets (XR Origin or Tunneling Vignette prefab); nothing was changed.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Main Menu");
        var undoGroup = Undo.GetCurrentGroup();

        RemoveNonXRCamera(scene);
        EnsureInteractionManager();
        var origin = EnsureXROrigin(scene, originPrefab);
        EnsureEventSystem();
        MakeFloorTeleportable(scene);
        var menu = BuildMenu(scene, origin, vignettePrefab);

        ArmsVRSceneSetup.SetUpScene(scene);
        var arms = origin.GetComponentInChildren<ArmsVRController>(true);
        if (arms != null)
        {
            var menuSettings = new SerializedObject(menu);
            SetReference(menuSettings, "arms", arms);
            menuSettings.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("ArmsVR could not be added; the arm calibration page will show 'ARMS UNAVAILABLE'.");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = menu.gameObject;
        Debug.Log("Main menu built on the front door. Save the scene (Ctrl+S / Cmd+S) to keep it.", menu);
    }

    // ---------- Player & scene setup ----------

    static void RemoveNonXRCamera(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Main Camera" && root.GetComponent<Camera>() != null && root.GetComponentInChildren<XROrigin>(true) == null)
                Undo.DestroyObjectImmediate(root);
        }
    }

    static void EnsureInteractionManager()
    {
        if (Object.FindFirstObjectByType<XRInteractionManager>(FindObjectsInactive.Include) != null)
            return;

        var managerObject = new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
        Undo.RegisterCreatedObjectUndo(managerObject, "Create XR Interaction Manager");
    }

    static XROrigin EnsureXROrigin(Scene scene, GameObject originPrefab)
    {
        var existing = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        var originObject = (GameObject)PrefabUtility.InstantiatePrefab(originPrefab, scene);
        Undo.RegisterCreatedObjectUndo(originObject, "Create XR Origin");
        originObject.name = "XR Origin (XR Rig)";
        originObject.transform.SetPositionAndRotation(k_SpawnPosition, Quaternion.identity);
        var origin = originObject.GetComponent<XROrigin>();

        // Same overrides as the XR Origin in PlayerTestScene.
        var originSettings = new SerializedObject(origin);
        originSettings.FindProperty("m_CameraYOffset").floatValue = 0f;
        originSettings.FindProperty("m_RequestedTrackingOriginMode").intValue = (int)XROrigin.TrackingOriginMode.Floor;
        originSettings.ApplyModifiedPropertiesWithoutUndo();

        foreach (var child in originObject.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "Jump")
                continue;
            child.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
        }

        foreach (var inputManager in originObject.GetComponentsInChildren<ControllerInputActionManager>(true))
        {
            var isLeft = inputManager.gameObject.name.Contains("Left");
            var managerSettings = new SerializedObject(inputManager);
            SetBool(managerSettings, "m_IndependentTurning", true);
            SetBool(managerSettings, "m_SmoothMotionEnabled", false);
            // Left hand: move + teleport. Right hand: turn + UI ray.
            if (isLeft)
            {
                SetBool(managerSettings, "m_UseTurnAction", false);
            }
            else
            {
                SetBool(managerSettings, "m_UseMoveAction", false);
                SetBool(managerSettings, "m_UseTeleportAction", false);
            }
            managerSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var snapTurn in originObject.GetComponentsInChildren<SnapTurnProvider>(true))
        {
            snapTurn.enableTurnAround = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(snapTurn);
        }
        foreach (var smoothTurn in originObject.GetComponentsInChildren<ContinuousTurnProvider>(true))
        {
            smoothTurn.enableTurnAround = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(smoothTurn);
        }

        return origin;
    }

    static void EnsureEventSystem()
    {
        var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<XRUIInputModule>() != null)
            return;

        foreach (var inputModule in eventSystem.GetComponents<BaseInputModule>())
        {
            Undo.RecordObject(inputModule, "Disable Input Module");
            inputModule.enabled = false;
        }
        Undo.AddComponent<XRUIInputModule>(eventSystem.gameObject);
    }

    static void MakeFloorTeleportable(Scene scene)
    {
        var floor = FindInEnvironment(scene, "Floor");
        if (floor == null)
        {
            Debug.LogWarning($"Could not find '{EnvironmentName}/Floor'; add a Teleportation Area to the floor manually.");
            return;
        }
        if (floor.GetComponent<Collider>() == null)
        {
            Debug.LogWarning("The Floor has no Collider, so it cannot be used for teleporting.", floor);
            return;
        }
        if (floor.GetComponent<TeleportationArea>() != null)
            return;

        var area = Undo.AddComponent<TeleportationArea>(floor.gameObject);
        int teleportLayers = InteractionLayerMask.GetMask("Teleport");
        area.interactionLayers = teleportLayers != 0 ? teleportLayers : 1 << 31;
    }

    static Transform FindInEnvironment(Scene scene, string childName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != EnvironmentName)
                continue;
            var child = root.transform.Find(childName);
            if (child != null)
                return child;
        }
        return null;
    }

    // ---------- Menu ----------

    static DoorMainMenu BuildMenu(Scene scene, XROrigin origin, GameObject vignettePrefab)
    {
        var canvasObject = new GameObject(MenuObjectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Door Main Menu");
        SceneManager.MoveGameObjectToScene(canvasObject, scene);

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = k_CanvasSize;
        canvasRect.localScale = Vector3.one * k_CanvasScale;
        // Not parented to the Door cube: its non-uniform scale would stretch the menu.
        canvasRect.SetPositionAndRotation(GetMenuPosition(scene), Quaternion.identity);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = origin.Camera;
        canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        var panel = CreateImage("Background", canvasRect, Vector2.zero, k_CanvasSize, k_PanelColor);
        panel.raycastTarget = true;
        CreateImage("Top Accent", canvasRect, new Vector2(0f, k_CanvasSize.y * 0.5f - 6f), new Vector2(k_CanvasSize.x - 40f, 4f), k_AccentColor);
        CreateImage("Bottom Accent", canvasRect, new Vector2(0f, -k_CanvasSize.y * 0.5f + 6f), new Vector2(k_CanvasSize.x - 40f, 4f), k_AccentColor);

        var menu = canvasObject.AddComponent<DoorMainMenu>();
        var refs = new SerializedObject(menu);
        SetReference(refs, "menuCanvasGroup", canvasObject.GetComponent<CanvasGroup>());
        SetReference(refs, "vignettePrefab", vignettePrefab);
        SetReference(refs, "playerCamera", origin.Camera.transform);

        BuildMainPage(canvasRect, menu, refs);
        BuildSettingsPage(canvasRect, menu, refs);
        BuildComfortPage(canvasRect, menu, refs);
        BuildAudioPage(canvasRect, menu, refs);
        BuildArmsPage(canvasRect, menu, refs);
        BuildCreditsPage(canvasRect, menu, refs);
        refs.ApplyModifiedPropertiesWithoutUndo();

        SetLayerRecursively(canvasObject.transform, LayerMask.NameToLayer("UI"));
        return menu;
    }

    static Vector3 GetMenuPosition(Scene scene)
    {
        var door = FindInEnvironment(scene, "Door");
        var doorRenderer = door != null ? door.GetComponent<Renderer>() : null;
        if (doorRenderer == null)
        {
            Debug.LogWarning($"Could not find '{EnvironmentName}/Door'; the menu was placed at a default position. Move it onto the door manually.");
            return k_FallbackMenuPosition;
        }

        var bounds = doorRenderer.bounds;
        // Slightly in front of the door's front face (the side facing the player).
        return new Vector3(bounds.center.x, bounds.min.y + k_MenuHeightAboveDoorBottom, bounds.min.z - 0.01f);
    }

    static void BuildMainPage(RectTransform canvas, DoorMainMenu menu, SerializedObject refs)
    {
        var page = CreatePage("Main Page", canvas);
        SetReference(refs, "mainPage", page.gameObject);
        CreateText("Title", page, "KAMER VOL SPOKEN", new Vector2(0f, 300f), new Vector2(920f, 100f), 76, TextAlignmentOptions.Center, FontStyles.Bold);
        var subtitle = CreateText("Subtitle", page, "HOUSE OF GHOSTS", new Vector2(0f, 225f), new Vector2(920f, 50f), 32, TextAlignmentOptions.Center, FontStyles.Normal);
        subtitle.color = k_AccentColor;
        subtitle.characterSpacing = 12f;

        AddClick(CreateButton("Play Button", page, "PLAY", new Vector2(0f, 100f), new Vector2(500f, 84f), 40), menu.Play);
        AddClick(CreateButton("Settings Button", page, "SETTINGS", new Vector2(0f, 0f), new Vector2(500f, 84f), 40), menu.ShowSettingsPage);
        AddClick(CreateButton("Credits Button", page, "CREDITS", new Vector2(0f, -100f), new Vector2(500f, 84f), 40), menu.ShowCreditsPage);
        AddClick(CreateButton("Quit Button", page, "QUIT", new Vector2(0f, -200f), new Vector2(500f, 84f), 40), menu.Quit);

        var hint = CreateText("Hint", page, "Point with your controller and pull the trigger to select", new Vector2(0f, -340f), new Vector2(920f, 40f), 24, TextAlignmentOptions.Center, FontStyles.Italic);
        hint.color = k_HintColor;
    }

    static void BuildSettingsPage(RectTransform canvas, DoorMainMenu menu, SerializedObject refs)
    {
        var page = CreatePage("Settings Page", canvas);
        SetReference(refs, "settingsPage", page.gameObject);
        CreateText("Heading", page, "SETTINGS", new Vector2(0f, 330f), new Vector2(920f, 80f), 56, TextAlignmentOptions.Center, FontStyles.Bold);

        AddClick(CreateButton("Comfort Button", page, "MOVEMENT & COMFORT", new Vector2(0f, 170f), new Vector2(600f, 84f), 36), menu.ShowComfortPage);
        AddClick(CreateButton("Audio Button", page, "AUDIO", new Vector2(0f, 70f), new Vector2(600f, 84f), 36), menu.ShowAudioPage);
        AddClick(CreateButton("Arms Button", page, "ARM CALIBRATION", new Vector2(0f, -30f), new Vector2(600f, 84f), 36), menu.ShowArmsPage);
        AddClick(CreateButton("Back Button", page, "BACK", new Vector2(0f, -250f), new Vector2(320f, 72f), 32), menu.ShowMainPage);
    }

    static void BuildComfortPage(RectTransform canvas, DoorMainMenu menu, SerializedObject refs)
    {
        var page = CreatePage("Comfort Page", canvas);
        SetReference(refs, "comfortPage", page.gameObject);
        CreatePageHeader(page, "MOVEMENT & COMFORT", menu.ShowSettingsPage);

        CreateText("Movement Heading", page, "MOVEMENT", new Vector2(0f, 300f), new Vector2(820f, 40f), 28, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var teleport = CreateButton("Teleport Button", page, "TELEPORT", new Vector2(-205f, 250f), new Vector2(400f, 60f), 28);
        var walk = CreateButton("Walk Button", page, "WALK", new Vector2(205f, 250f), new Vector2(400f, 60f), 28);
        AddClick(teleport, menu.SetTeleportMovement);
        AddClick(walk, menu.SetWalkMovement);
        SetReference(refs, "teleportButton", teleport);
        SetReference(refs, "walkButton", walk);

        CreateText("Turning Heading", page, "TURNING", new Vector2(0f, 185f), new Vector2(820f, 40f), 28, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var snap = CreateButton("Snap Turn Button", page, "SNAP", new Vector2(-205f, 135f), new Vector2(400f, 60f), 28);
        var smooth = CreateButton("Smooth Turn Button", page, "SMOOTH", new Vector2(205f, 135f), new Vector2(400f, 60f), 28);
        AddClick(snap, menu.SetSnapTurn);
        AddClick(smooth, menu.SetSmoothTurn);
        SetReference(refs, "snapTurnButton", snap);
        SetReference(refs, "smoothTurnButton", smooth);

        CreateLabeledSlider(page, refs, "Move Speed", 70f, ComfortSettings.MinMoveSpeed, ComfortSettings.MaxMoveSpeed, "moveSpeedSlider", "moveSpeedLabel", menu.SetMoveSpeed);
        CreateLabeledSlider(page, refs, "Snap Angle", -20f, ComfortSettings.MinSnapAngle, ComfortSettings.MaxSnapAngle, "snapAngleSlider", "snapAngleLabel", menu.SetSnapAngle);
        CreateLabeledSlider(page, refs, "Turn Speed", -110f, ComfortSettings.MinTurnSpeed, ComfortSettings.MaxTurnSpeed, "turnSpeedSlider", "turnSpeedLabel", menu.SetTurnSpeed);

        var vignette = CreateButton("Vignette Button", page, "COMFORT VIGNETTE: ON", new Vector2(0f, -205f), new Vector2(820f, 58f), 26);
        AddClick(vignette, menu.ToggleVignette);
        SetReference(refs, "vignetteButton", vignette);
        SetReference(refs, "vignetteButtonLabel", vignette.GetComponentInChildren<TMP_Text>());
        CreateLabeledSlider(page, refs, "Vignette Strength", -265f, ComfortSettings.MinVignetteStrength, ComfortSettings.MaxVignetteStrength, "vignetteStrengthSlider", "vignetteStrengthLabel", menu.SetVignetteStrength);
    }

    static void BuildAudioPage(RectTransform canvas, DoorMainMenu menu, SerializedObject refs)
    {
        var page = CreatePage("Audio Page", canvas);
        SetReference(refs, "audioPage", page.gameObject);
        CreatePageHeader(page, "AUDIO", menu.ShowSettingsPage);
        CreateLabeledSlider(page, refs, "Master Volume", 120f, 0f, 1f, "masterVolumeSlider", "masterVolumeLabel", menu.SetMasterVolume);
        var hint = CreateText("Hint", page, "Listen closely: sound tells you where the ghost is hiding.", new Vector2(0f, -40f), new Vector2(820f, 80f), 26, TextAlignmentOptions.Center, FontStyles.Italic);
        hint.color = k_HintColor;
    }

    static void BuildArmsPage(RectTransform canvas, DoorMainMenu menu, SerializedObject refs)
    {
        var page = CreatePage("Arm Calibration Page", canvas);
        SetReference(refs, "armsPage", page.gameObject);
        CreatePageHeader(page, "ARM CALIBRATION", menu.ShowSettingsPage);

        CreateLabeledSlider(page, refs, "Left Arm", 270f, ArmsVRController.MinimumArmScale, ArmsVRController.MaximumArmScale, "leftArmSlider", "leftArmLabel", menu.SetLeftArmScale);
        CreateLabeledSlider(page, refs, "Right Arm", 170f, ArmsVRController.MinimumArmScale, ArmsVRController.MaximumArmScale, "rightArmSlider", "rightArmLabel", menu.SetRightArmScale);

        var status = CreateText("Status", page, "", new Vector2(0f, 40f), new Vector2(820f, 120f), 28, TextAlignmentOptions.Center, FontStyles.Normal);
        status.color = k_AccentColor;
        SetReference(refs, "armStatusLabel", status);

        var calibrate = CreateButton("Calibrate Button", page, "CALIBRATE ARMS", new Vector2(0f, -90f), new Vector2(600f, 72f), 32);
        var reset = CreateButton("Reset Button", page, "RESET ARMS", new Vector2(0f, -180f), new Vector2(600f, 72f), 32);
        var cancel = CreateButton("Cancel Button", page, "CANCEL CALIBRATION", new Vector2(0f, -270f), new Vector2(600f, 72f), 32);
        AddClick(calibrate, menu.StartArmCalibration);
        AddClick(reset, menu.ResetArms);
        AddClick(cancel, menu.CancelArmCalibration);
        SetReference(refs, "calibrateArmsButton", calibrate);
        SetReference(refs, "resetArmsButton", reset);
        SetReference(refs, "cancelCalibrationButton", cancel);
    }

    static void BuildCreditsPage(RectTransform canvas, DoorMainMenu menu, SerializedObject refs)
    {
        var page = CreatePage("Credits Page", canvas);
        SetReference(refs, "creditsPage", page.gameObject);
        CreatePageHeader(page, "CREDITS", menu.ShowMainPage);
        const string credits =
            "KAMER VOL SPOKEN\n<size=70%>A student VR project</size>\n\n" +
            "PROGRAMMING\n<size=80%>Name\nName</size>\n\n" +
            "ART & DESIGN\n<size=80%>Name\nName</size>\n\n" +
            "<size=60%>(Edit this text on Door Main Menu > Credits Page > Credits Text)</size>";
        CreateText("Credits Text", page, credits, new Vector2(0f, -40f), new Vector2(820f, 640f), 34, TextAlignmentOptions.Center, FontStyles.Normal);
    }

    // ---------- UI building helpers ----------

    static RectTransform CreatePage(string name, RectTransform canvas)
    {
        var page = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        page.SetParent(canvas, false);
        SetRect(page, Vector2.zero, k_CanvasSize);
        return page;
    }

    static void CreatePageHeader(RectTransform page, string title, UnityAction onBack)
    {
        CreateText("Heading", page, title, new Vector2(60f, 370f), new Vector2(720f, 70f), 44, TextAlignmentOptions.Center, FontStyles.Bold);
        AddClick(CreateButton("Back Button", page, "BACK", new Vector2(-400f, 370f), new Vector2(150f, 60f), 28), onBack);
    }

    static void CreateLabeledSlider(RectTransform page, SerializedObject refs, string name, float y, float minimum, float maximum, string sliderField, string labelField, UnityAction<float> onChanged)
    {
        var label = CreateText(name + " Label", page, name.ToUpperInvariant(), new Vector2(0f, y), new Vector2(820f, 36f), 26, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        var slider = CreateSlider(name + " Slider", page, new Vector2(0f, y - 38f), new Vector2(820f, 40f));
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.SetValueWithoutNotify(Mathf.Lerp(minimum, maximum, 0.5f));
        UnityEventTools.AddPersistentListener(slider.onValueChanged, onChanged);
        SetReference(refs, sliderField, slider);
        SetReference(refs, labelField, label);
    }

    static TextMeshProUGUI CreateText(string name, RectTransform parent, string content, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, FontStyles style)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        SetRect(textObject.GetComponent<RectTransform>(), position, size);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = k_TextColor;
        text.raycastTarget = false;
        return text;
    }

    static Image CreateImage(string name, RectTransform parent, Vector2 position, Vector2 size, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        SetRect(imageObject.GetComponent<RectTransform>(), position, size);
        var image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Button CreateButton(string name, RectTransform parent, string label, Vector2 position, Vector2 size, float fontSize)
    {
        var image = CreateImage(name, parent, position, size, k_ControlColor);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateColorBlock();

        var text = CreateText("Label", image.rectTransform, label, Vector2.zero, size, fontSize, TextAlignmentOptions.Center, FontStyles.Bold);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.sizeDelta = Vector2.zero;
        return button;
    }

    static Slider CreateSlider(string name, RectTransform parent, Vector2 position, Vector2 size)
    {
        var sliderObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        SetRect(sliderRect, position, size);
        // Invisible background that catches the ray across the whole slider area.
        var hitArea = sliderObject.GetComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0.01f);
        hitArea.raycastTarget = true;

        CreateImage("Track", sliderRect, Vector2.zero, new Vector2(size.x, 10f), k_TrackColor);

        var fill = CreateImage("Fill", sliderRect, Vector2.zero, Vector2.zero, k_AccentColor);
        fill.rectTransform.anchorMin = new Vector2(0f, 0.375f);
        fill.rectTransform.anchorMax = new Vector2(0f, 0.625f);
        fill.rectTransform.sizeDelta = Vector2.zero;

        var handle = CreateImage("Handle", sliderRect, Vector2.zero, Vector2.zero, k_TextColor);
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(30f, 0f);

        var slider = sliderObject.GetComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.transition = Selectable.Transition.ColorTint;
        slider.colors = CreateColorBlock();
        return slider;
    }

    static ColorBlock CreateColorBlock()
    {
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        colors.fadeDuration = 0.08f;
        return colors;
    }

    static void AddClick(Button button, UnityAction action)
    {
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        if (layer < 0)
            return;
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    static void SetReference(SerializedObject settings, string propertyName, Object value)
    {
        var property = settings.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException("DoorMainMenu is missing the field: " + propertyName);
        property.objectReferenceValue = value;
    }

    static void SetBool(SerializedObject settings, string propertyName, bool value)
    {
        var property = settings.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"{settings.targetObject.GetType().Name} has no field '{propertyName}'; skipped.", settings.targetObject);
            return;
        }
        property.boolValue = value;
    }
}
#endif
