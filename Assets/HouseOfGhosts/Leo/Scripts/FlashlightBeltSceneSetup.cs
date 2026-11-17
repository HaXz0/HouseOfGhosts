#if UNITY_EDITOR
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Sets up the invisible belt in PlayerTestScene automatically when the scene is open
// (same approach as ArmsVRSceneSetup). Can also be run from Tools > House of Ghosts > Set Up Flashlight Belt.
[InitializeOnLoad]
static class FlashlightBeltSceneSetup
{
    const string ScenePath = "Assets/HouseOfGhosts/Scenes/LeoScenes/PlayerTestScene.unity";
    static readonly Vector3 k_LeftHipPosition = new Vector3(-0.18f, 0f, 0.04f);
    static readonly Vector3 k_HangingRotation = new Vector3(90f, 0f, 0f);

    static FlashlightBeltSceneSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += () => SetUp(false);
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == ScenePath)
            EditorApplication.delayCall += () => SetUp(false);
    }

    [MenuItem("Tools/House of Ghosts/Set Up Flashlight Belt")]
    static void SetUpFromMenu()
    {
        SetUp(true);
    }

    static void SetUp(bool fromMenu)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (fromMenu)
                Debug.LogWarning("Exit Play Mode before setting up the flashlight belt.");
            return;
        }

        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (fromMenu)
                Debug.LogWarning("Open PlayerTestScene before setting up the flashlight belt.");
            return;
        }

        var origin = FindSingle<XROrigin>(scene, "XR Origin");
        var flashlight = FindSingle<FlashlightController>(scene, "flashlight");
        if (origin == null || flashlight == null)
            return;

        var existingSlot = origin.GetComponentInChildren<BeltSlot>(true);
        if (existingSlot != null)
        {
            if (fromMenu)
            {
                Debug.Log("The flashlight belt is already set up.", existingSlot);
                Selection.activeGameObject = existingSlot.gameObject;
            }
            return;
        }

        if (origin.Camera == null)
        {
            Debug.LogError("The XR Origin has no camera assigned; no changes were made.", origin);
            return;
        }

        var grab = flashlight.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            Debug.LogError("The flashlight has no XR Grab Interactable; no changes were made.", flashlight);
            return;
        }

        var sceneWasDirty = scene.isDirty;

        Undo.SetCurrentGroupName("Set Up Flashlight Belt");
        var undoGroup = Undo.GetCurrentGroup();

        var beltObject = new GameObject("Player Belt");
        Undo.RegisterCreatedObjectUndo(beltObject, "Create Player Belt");
        Undo.SetTransformParent(beltObject.transform, origin.transform, "Parent Player Belt");
        beltObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        var belt = Undo.AddComponent<PlayerBelt>(beltObject);
        var beltSettings = new SerializedObject(belt);
        beltSettings.FindProperty("head").objectReferenceValue = origin.Camera.transform;
        beltSettings.ApplyModifiedProperties();

        var slotObject = new GameObject("Left Hip Slot");
        Undo.RegisterCreatedObjectUndo(slotObject, "Create Left Hip Slot");
        slotObject.transform.SetParent(beltObject.transform, false);
        slotObject.transform.SetLocalPositionAndRotation(k_LeftHipPosition, Quaternion.identity);

        var attachObject = new GameObject("Attach");
        Undo.RegisterCreatedObjectUndo(attachObject, "Create Belt Attach");
        attachObject.transform.SetParent(slotObject.transform, false);
        attachObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(k_HangingRotation));

        var socket = Undo.AddComponent<XRSocketInteractor>(slotObject);
        socket.attachTransform = attachObject.transform;
        socket.showInteractableHoverMeshes = false;
        socket.hoverSocketSnapping = false;

        var slot = Undo.AddComponent<BeltSlot>(slotObject);
        var slotSettings = new SerializedObject(slot);
        slotSettings.FindProperty("item").objectReferenceValue = grab;
        slotSettings.FindProperty("attachPoint").objectReferenceValue = attachObject.transform;
        slotSettings.FindProperty("playerBody").objectReferenceValue = origin.GetComponentInChildren<CharacterController>(true);
        slotSettings.ApplyModifiedProperties();

        UnityEventTools.AddBoolPersistentListener(slot.holstered, flashlight.SetBeamEnabled, false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(socket);
        EditorUtility.SetDirty(slot);
        EditorSceneManager.MarkSceneDirty(scene);
        if (fromMenu)
            Selection.activeGameObject = slotObject;

        if (!sceneWasDirty && EditorSceneManager.SaveScene(scene))
            Debug.Log("Flashlight belt added to the left hip and PlayerTestScene was saved.", slotObject);
        else
            Debug.Log("Flashlight belt added to the left hip. Save PlayerTestScene to keep it.", slotObject);
    }

    static T FindSingle<T>(Scene scene, string label) where T : Component
    {
        T found = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var candidate in root.GetComponentsInChildren<T>(true))
            {
                if (found != null)
                {
                    Debug.LogError($"Found more than one {label} in PlayerTestScene; no changes were made.", candidate);
                    return null;
                }

                found = candidate;
            }
        }

        if (found == null)
            Debug.LogError($"Could not find the {label} in PlayerTestScene; no changes were made.");
        return found;
    }
}
#endif
