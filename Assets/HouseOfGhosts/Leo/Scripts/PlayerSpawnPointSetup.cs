#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds a "Player Spawn Point" to PlayerTestScene and MainMenu when they are opened and do not have one yet.
/// Move the spawn point object afterwards to change where the player starts; it is never moved back.
/// </summary>
[InitializeOnLoad]
static class PlayerSpawnPointSetup
{
    const string SpawnName = "Player Spawn Point";

    struct SpawnLocation
    {
        public string scenePath;
        public Vector3 position;
        public float yaw;
    }

    // Middle of each scene's floor, facing the main content (the interaction stations and the menu door).
    static readonly SpawnLocation[] k_Locations =
    {
        new SpawnLocation { scenePath = "Assets/HouseOfGhosts/Scenes/LeoScenes/PlayerTestScene.unity", position = new Vector3(1f, 0f, -1f), yaw = 0f },
        new SpawnLocation { scenePath = "Assets/HouseOfGhosts/Scenes/LeoScenes/MainMenu.unity", position = new Vector3(-0.05f, 0.5f, 2.6f), yaw = 0f },
    };

    static PlayerSpawnPointSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += () => SetUpOpenScenes();
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += () => SetUpOpenScenes();
    }

    [MenuItem("Tools/House of Ghosts/Add Player Spawn Points")]
    static void SetUpFromMenu()
    {
        if (!SetUpOpenScenes())
            Debug.LogWarning("Open PlayerTestScene or MainMenu to add a player spawn point.");
    }

    static bool SetUpOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return false;

        var foundScene = false;
        foreach (var location in k_Locations)
        {
            var scene = SceneManager.GetSceneByPath(location.scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            foundScene = true;
            SetUpScene(scene, location);
        }
        return foundScene;
    }

    static void SetUpScene(Scene scene, SpawnLocation location)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<PlayerSpawnPoint>(true) != null)
                return;
        }

        var wasDirty = scene.isDirty;
        var spawn = new GameObject(SpawnName);
        SceneManager.MoveGameObjectToScene(spawn, scene);
        spawn.transform.SetPositionAndRotation(location.position, Quaternion.Euler(0f, location.yaw, 0f));
        Undo.RegisterCreatedObjectUndo(spawn, "Add Player Spawn Point");
        try
        {
            spawn.AddComponent<PlayerSpawnPoint>();
        }
        catch (Exception exception)
        {
            Undo.DestroyObjectImmediate(spawn);
            Debug.LogError($"Could not add a player spawn point to {scene.name}: {exception.Message}");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (wasDirty)
            Debug.Log($"A player spawn point was added to {scene.name}. Save the scene to keep it.", spawn);
        else if (EditorSceneManager.SaveScene(scene))
            Debug.Log($"A player spawn point was added to {scene.name} and the scene was saved.", spawn);
        else
            Debug.LogWarning($"A player spawn point was added to {scene.name}, but the scene could not be saved.", spawn);
    }
}
#endif
