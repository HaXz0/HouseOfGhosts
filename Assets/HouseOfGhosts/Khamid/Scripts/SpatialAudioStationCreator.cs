#if UNITY_EDITOR
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Editor-only menu tool that builds the Spatial Audio test station in the open scene.
/// It only adds new objects; it never changes or removes existing ones.
/// </summary>
public static class SpatialAudioStationCreator
{
    private const string MenuPath = "Tools/House of Ghosts/Create Spatial Audio Station";
    private const string StationName = "Spatial Audio Station";
    private const string StationsParentName = "Interaction Test Stations";
    private const string ClipPath = "Assets/_Course Library/Audio/FX/SFX_Bird_1.ogg";
    private const string SphereMaterialPath = "Assets/_Course Library/Materials/Solid Colors/Blue.mat";

    private static readonly Vector3 StationPosition = new Vector3(4.8f, 0f, 1.65f);
    private const float SphereHeight = 1.1f;
    private const float SphereSize = 0.2f;
    private const float SphereSpacing = 0.35f;

    [MenuItem(MenuPath)]
    private static void CreateStation()
    {
        var scene = SceneManager.GetActiveScene();
        var existing = FindInScene(scene, StationName);
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            Debug.LogWarning($"'{StationName}' already exists in this scene. Nothing was created.", existing);
            return;
        }

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (clip == null)
            Debug.LogWarning($"Could not find the clip at {ClipPath}. Assign it on the Spatial Audio Player manually.");

        var head = FindPlayerHead(scene);
        if (head == null)
            Debug.LogWarning("Could not find the XR Origin camera. Assign Player Head on the Spatial Audio Player manually.");

        var parentObject = FindInScene(scene, StationsParentName);
        var station = new GameObject(StationName).transform;
        Undo.RegisterCreatedObjectUndo(station.gameObject, "Create Spatial Audio Station");
        if (parentObject != null)
            station.SetParent(parentObject.transform, false);
        station.localPosition = StationPosition;

        var player = CreateAudioPlayer(station, head, clip);

        var directions = new[] { SpatialDirection.Front, SpatialDirection.Back, SpatialDirection.Left, SpatialDirection.Right };
        var firstX = -SphereSpacing * (directions.Length - 1) * 0.5f;
        for (var i = 0; i < directions.Length; i++)
            CreateSphere(station, player, directions[i], new Vector3(firstX + i * SphereSpacing, SphereHeight, 0f));

        CreateLabel(station, "SPATIAL AUDIO\nSelect a sphere to hear a sound", new Vector3(0f, 1.65f, 0f));

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = station.gameObject;
        Debug.Log("Spatial Audio Station created. Save the scene to keep it.", station.gameObject);
    }

    private static SpatialAudioDirectionPlayer CreateAudioPlayer(Transform station, Transform head, AudioClip clip)
    {
        var playerObject = new GameObject("Spatial Audio Player");
        playerObject.transform.SetParent(station, false);

        var audioSource = playerObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        var player = playerObject.AddComponent<SpatialAudioDirectionPlayer>();
        var serializedPlayer = new SerializedObject(player);
        serializedPlayer.FindProperty("playerHead").objectReferenceValue = head;
        serializedPlayer.FindProperty("clip").objectReferenceValue = clip;
        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
        return player;
    }

    private static void CreateSphere(Transform station, SpatialAudioDirectionPlayer player, SpatialDirection direction, Vector3 position)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = $"Sphere {direction}";
        sphere.transform.SetParent(station, false);
        sphere.transform.localPosition = position;
        sphere.transform.localScale = Vector3.one * SphereSize;

        var material = AssetDatabase.LoadAssetAtPath<Material>(SphereMaterialPath);
        if (material != null)
            sphere.GetComponent<Renderer>().sharedMaterial = material;

        // Inactive while adding components so Awake/OnEnable run after configuration.
        sphere.SetActive(false);
        sphere.AddComponent<XRSimpleInteractable>();
        var testSphere = sphere.AddComponent<SpatialAudioTestSphere>();
        var serializedSphere = new SerializedObject(testSphere);
        serializedSphere.FindProperty("audioPlayer").objectReferenceValue = player;
        serializedSphere.FindProperty("direction").enumValueIndex = (int)direction;
        serializedSphere.FindProperty("sphereRenderer").objectReferenceValue = sphere.GetComponent<Renderer>();
        serializedSphere.ApplyModifiedPropertiesWithoutUndo();
        sphere.SetActive(true);

        CreateLabel(station, direction.ToString().ToUpperInvariant(), position + Vector3.up * 0.2f);
    }

    private static void CreateLabel(Transform parent, string content, Vector3 position)
    {
        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = position;

        var text = labelObject.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 48;
        text.characterSize = 0.004f;
        text.color = Color.white;
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
    }

    private static Transform FindPlayerHead(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var origin = root.GetComponentInChildren<XROrigin>(true);
            if (origin != null && origin.Camera != null)
                return origin.Camera.transform;
        }

        return Camera.main != null ? Camera.main.transform : null;
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                    return child.gameObject;
            }
        }

        return null;
    }
}
#endif
