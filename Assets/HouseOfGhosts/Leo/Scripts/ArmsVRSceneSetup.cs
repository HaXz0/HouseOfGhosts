#if UNITY_EDITOR
using System;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class ArmsVRSceneSetup
{
    static readonly string[] ScenePaths =
    {
        "Assets/HouseOfGhosts/Scenes/LeoScenes/PlayerTestScene.unity",
        "Assets/HouseOfGhosts/Scenes/LeoScenes/MainMenu.unity",
    };
    const string PrefabPath = "Assets/HouseOfGhosts/Ferron/Models/Prefabs/ArmsVR.prefab";
    const string ModelPath = "Assets/HouseOfGhosts/Ferron/Models/Models/ArmsVR.fbx";
    const string GeneratedAvatarPath = "Assets/HouseOfGhosts/Leo/Prefabs/ArmsVR_Generic_Avatar.asset";
    const string ActionsPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/XRI Default Input Actions.inputactions";
    const string LeftClipPath = "Assets/HouseOfGhosts/Ferron/Models/Animations/Armature_Squeeze.L.anim";
    const string RightClipPath = "Assets/HouseOfGhosts/Ferron/Models/Animations/Armature_Squeeze.R.anim";

    static ArmsVRSceneSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += TrySetupOpenScene;
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (Array.IndexOf(ScenePaths, scene.path) >= 0)
            EditorApplication.delayCall += TrySetupOpenScene;
    }

    [MenuItem("Tools/House of Ghosts/Set Up ArmsVR")]
    static void SetUpFromMenu()
    {
        var foundScene = false;
        foreach (var path in ScenePaths)
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;
            foundScene = true;
            SetUpScene(scene);
        }

        if (!foundScene)
            Debug.LogWarning("Open PlayerTestScene or MainMenu before setting up ArmsVR.");
    }

    static void TrySetupOpenScene()
    {
        foreach (var path in ScenePaths)
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded)
                SetUpScene(scene);
        }
    }

    /// <summary>
    /// Adds ArmsVR to the scene's XR Origin if it does not have it yet. Safe to call repeatedly.
    /// </summary>
    internal static void SetUpScene(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!scene.IsValid() || !scene.isLoaded)
            return;

        XROrigin origin = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var candidate = root.GetComponentInChildren<XROrigin>(true);
            if (candidate == null)
                continue;
            if (origin != null)
            {
                Debug.LogError("ArmsVR setup found multiple XR Origins; no scene changes were made.");
                return;
            }
            origin = candidate;
        }

        if (origin == null || origin.Camera == null)
            return;

        var existingArms = origin.GetComponentInChildren<ArmsVRController>(true);
        if (existingArms != null)
        {
            var existingAnimator = existingArms.GetComponent<Animator>();
            if (existingAnimator != null && existingAnimator.avatar == null)
            {
                var sceneWasDirty = scene.isDirty;
                Undo.RecordObject(existingAnimator, "Assign ArmsVR Avatar");
                existingAnimator.avatar = ResolveAvatar(existingArms.gameObject);
                if (existingAnimator.avatar == null)
                {
                    Debug.LogError("Could not build a Generic Avatar for ArmsVR; the squeeze clips will not play.", existingArms);
                    return;
                }
                EditorUtility.SetDirty(existingAnimator);
                EditorSceneManager.MarkSceneDirty(scene);
                if (sceneWasDirty)
                    Debug.Log($"Assigned the ArmsVR Avatar. Save {scene.name} to keep the animation setup.", existingArms);
                else
                    EditorSceneManager.SaveScene(scene);
            }
            else if (existingAnimator != null && existingAnimator.avatar == null)
            {
                Debug.LogError("The ArmsVR Animator has no Avatar; the squeeze clips cannot play.", existingArms);
            }
            return;
        }

        var leftController = FindNamedChild(origin.transform, "Left Controller");
        var rightController = FindNamedChild(origin.transform, "Right Controller");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        var leftClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(LeftClipPath);
        var rightClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RightClipPath);
        if (leftController == null || rightController == null || prefab == null || actions == null ||
            leftClip == null || rightClip == null ||
            actions.FindAction("XRI Left Interaction/Select Value") == null ||
            actions.FindAction("XRI Right Interaction/Select Value") == null ||
            actions.FindAction("XRI Left/Is Tracked") == null || actions.FindAction("XRI Right/Is Tracked") == null)
        {
            Debug.LogError("ArmsVR setup could not find the prefab, controllers, input actions, or grip clips; existing hands were left unchanged.");
            return;
        }

        var wasDirty = scene.isDirty;
        var arms = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (arms == null)
        {
            Debug.LogError("ArmsVR setup could not instantiate the prefab; existing hands were left unchanged.");
            return;
        }

        Undo.RegisterCreatedObjectUndo(arms, "Set up ArmsVR");
        try
        {
            Undo.SetTransformParent(arms.transform, origin.transform, "Set up ArmsVR");
            arms.transform.localPosition = Vector3.zero;
            arms.transform.localRotation = Quaternion.identity;
            var leftShoulder = arms.transform.Find("Armature/UpperArm.L");
            var leftElbow = arms.transform.Find("Armature/UpperArm.L/LowerArm.L");
            var leftHand = arms.transform.Find("Armature/UpperArm.L/LowerArm.L/Hand.L");
            var rightShoulder = arms.transform.Find("Armature/UpperArm.R");
            var rightElbow = arms.transform.Find("Armature/UpperArm.R/LowerArm.R");
            var rightHand = arms.transform.Find("Armature/UpperArm.R/LowerArm.R/Hand.R");
            if (leftShoulder == null || leftElbow == null || leftHand == null || rightShoulder == null ||
                rightElbow == null || rightHand == null || arms.GetComponentInChildren<SkinnedMeshRenderer>(true) == null ||
                leftHand.childCount == 0 || rightHand.childCount == 0)
                throw new InvalidOperationException("The prefab is missing a skinned arm, finger hierarchy, or one of its shoulder/elbow/hand bones.");

            var animator = arms.GetComponent<Animator>();
            if (animator == null)
                animator = Undo.AddComponent<Animator>(arms);
            animator.avatar = ResolveAvatar(arms);
            if (animator.avatar == null)
                throw new InvalidOperationException("Could not build a Generic Avatar for ArmsVR.");
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var rigBuilder = arms.GetComponent<RigBuilder>();
            if (rigBuilder == null)
                rigBuilder = Undo.AddComponent<RigBuilder>(arms);
            rigBuilder.enabled = false;

            var rigObject = new GameObject("Arm IK Rig");
            rigObject.transform.SetParent(arms.transform, false);
            var rig = rigObject.AddComponent<Rig>();
            var leftTarget = CreateTarget("Left Wrist Target", arms.transform, leftHand);
            var rightTarget = CreateTarget("Right Wrist Target", arms.transform, rightHand);
            var leftHint = CreateTarget("Left Elbow Hint", arms.transform, leftElbow);
            var rightHint = CreateTarget("Right Elbow Hint", arms.transform, rightElbow);
            var leftConstraint = CreateConstraint("Left Arm IK", rigObject.transform, leftShoulder, leftElbow, leftHand, leftTarget, leftHint);
            var rightConstraint = CreateConstraint("Right Arm IK", rigObject.transform, rightShoulder, rightElbow, rightHand, rightTarget, rightHint);
            if (!leftConstraint.IsValid() || !rightConstraint.IsValid())
                throw new InvalidOperationException("One of the two-bone IK constraints is invalid.");

            rigBuilder.layers.Clear();
            rigBuilder.layers.Add(new RigLayer(rig));
            var driver = Undo.AddComponent<ArmsVRController>(arms);
            var settings = new SerializedObject(driver);
            SetReference(settings, "head", origin.Camera.transform);
            SetReference(settings, "leftController", leftController);
            SetReference(settings, "rightController", rightController);
            SetReference(settings, "leftShoulder", leftShoulder);
            SetReference(settings, "rightShoulder", rightShoulder);
            SetReference(settings, "leftHand", leftHand);
            SetReference(settings, "rightHand", rightHand);
            SetReference(settings, "leftTarget", leftTarget);
            SetReference(settings, "rightTarget", rightTarget);
            SetReference(settings, "leftHint", leftHint);
            SetReference(settings, "rightHint", rightHint);
            SetReference(settings, "leftConstraint", leftConstraint);
            SetReference(settings, "rightConstraint", rightConstraint);
            SetReference(settings, "actions", actions);
            SetReference(settings, "leftGripClip", leftClip);
            SetReference(settings, "rightGripClip", rightClip);
            settings.ApplyModifiedProperties();

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "VR_Hand_Blue_Left" && root.name != "VR_Hand_Blue_Right")
                    continue;
                Undo.RecordObject(root, "Replace Blue Hand Visual");
                root.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (wasDirty)
                Debug.Log($"ArmsVR was added to {scene.name}. Save the scene to keep it.", arms);
            else if (!EditorSceneManager.SaveScene(scene))
                Debug.LogWarning("ArmsVR was added, but the scene could not be saved.", arms);
        }
        catch (Exception exception)
        {
            Undo.DestroyObjectImmediate(arms);
            Debug.LogError("ArmsVR setup failed; existing hands were left unchanged: " + exception.Message);
        }
    }

    static Transform FindNamedChild(Transform parent, string name)
    {
        foreach (var child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == name)
                return child;
        return null;
    }

    static Avatar ResolveAvatar(GameObject armsRoot)
    {
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(GeneratedAvatarPath);
        if (avatar != null && avatar.isValid)
            return avatar;

        avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault(candidate => candidate.isValid);
        if (avatar != null)
            return avatar;

        var generated = AvatarBuilder.BuildGenericAvatar(armsRoot, string.Empty);
        if (generated == null || !generated.isValid)
        {
            if (generated != null)
                UnityEngine.Object.DestroyImmediate(generated);
            return null;
        }

        generated.name = "ArmsVR Generic Avatar";
        AssetDatabase.CreateAsset(generated, GeneratedAvatarPath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Avatar>(GeneratedAvatarPath);
    }

    static Transform CreateTarget(string name, Transform parent, Transform poseSource)
    {
        var target = new GameObject(name).transform;
        target.SetParent(parent, false);
        target.SetPositionAndRotation(poseSource.position, poseSource.rotation);
        return target;
    }

    static TwoBoneIKConstraint CreateConstraint(string name, Transform parent, Transform root, Transform mid, Transform tip, Transform target, Transform hint)
    {
        var constraintObject = new GameObject(name);
        constraintObject.transform.SetParent(parent, false);
        var constraint = constraintObject.AddComponent<TwoBoneIKConstraint>();
        constraint.data.root = root;
        constraint.data.mid = mid;
        constraint.data.tip = tip;
        constraint.data.target = target;
        constraint.data.hint = hint;
        constraint.data.targetPositionWeight = 1f;
        constraint.data.targetRotationWeight = 1f;
        constraint.data.hintWeight = 1f;
        return constraint;
    }

    static void SetReference(SerializedObject settings, string propertyName, UnityEngine.Object value)
    {
        var property = settings.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException("Missing ArmsVR driver field: " + propertyName);
        property.objectReferenceValue = value;
    }
}
#endif