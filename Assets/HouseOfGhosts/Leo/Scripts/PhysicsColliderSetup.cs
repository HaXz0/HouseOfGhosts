#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Finds visible meshes that have no collider, so the physics hands and body would pass straight through them.
/// Works on the current selection, or on the whole active scene when nothing is selected.
/// Nothing is saved automatically and every change can be undone with Ctrl+Z.
/// </summary>
static class PhysicsColliderSetup
{
    const string k_MenuRoot = "Tools/House of Ghosts/Physics/";
    // Meshes smaller than this (in meters) are decoration that hands do not need to touch.
    const float k_MinimumSize = 0.02f;
    // A mesh collider with more triangles than this is worth replacing by hand with a few box colliders.
    const int k_HeavyTriangleCount = 5000;

    [MenuItem(k_MenuRoot + "Report Meshes Without Colliders")]
    static void Report()
    {
        var missing = FindMeshesWithoutColliders(out var scope);
        if (missing.Count == 0)
        {
            Debug.Log($"Physics check ({scope}): every visible mesh has a collider.");
            return;
        }

        var report = new StringBuilder($"Physics check ({scope}): {missing.Count} visible meshes have no collider:\n");
        foreach (var filter in missing)
            report.AppendLine($"  {GetPath(filter.transform)}  ({CountTriangles(filter.sharedMesh)} triangles)");
        Debug.LogWarning(report.ToString());
    }

    [MenuItem(k_MenuRoot + "Add Missing Colliders")]
    static void AddMissing()
    {
        var missing = FindMeshesWithoutColliders(out var scope);
        if (missing.Count == 0)
        {
            Debug.Log($"Physics check ({scope}): every visible mesh already has a collider.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Add Missing Colliders",
            $"Add a collider to {missing.Count} meshes in {scope}?\n\nNothing is saved until you save the scene, and Ctrl+Z undoes it.",
            "Add Colliders", "Cancel"))
            return;

        var heavy = new StringBuilder();
        Undo.SetCurrentGroupName("Add Missing Colliders");
        foreach (var filter in missing)
        {
            var mesh = filter.sharedMesh;
            if (IsBuiltInCube(mesh))
            {
                Undo.AddComponent<BoxCollider>(filter.gameObject);
                continue;
            }

            var meshCollider = Undo.AddComponent<MeshCollider>(filter.gameObject);
            meshCollider.sharedMesh = mesh;
            // Only convex mesh colliders are allowed on objects that physics moves around.
            var body = filter.GetComponentInParent<Rigidbody>();
            meshCollider.convex = body != null && !body.isKinematic;
            var triangles = CountTriangles(mesh);
            if (triangles > k_HeavyTriangleCount)
                heavy.AppendLine($"  {GetPath(filter.transform)}  ({triangles} triangles)");
        }
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

        Debug.Log($"Added colliders to {missing.Count} meshes in {scope}. Save the scene to keep them.");
        if (heavy.Length > 0)
            Debug.LogWarning("These mesh colliders are detailed and may cost performance on Quest 3. Consider replacing them with a few box colliders:\n" + heavy);
    }

    static List<MeshFilter> FindMeshesWithoutColliders(out string scope)
    {
        var candidates = new List<MeshFilter>();
        var selection = Selection.gameObjects;
        var activeScene = SceneManager.GetActiveScene();
        if (selection.Length > 0)
        {
            scope = "the selection";
            foreach (var selected in selection)
            {
                // Skip prefab assets selected in the Project window.
                if (selected.scene.IsValid())
                    candidates.AddRange(selected.GetComponentsInChildren<MeshFilter>(false));
            }
        }
        else
        {
            scope = "scene '" + activeScene.name + "'";
            foreach (var root in activeScene.GetRootGameObjects())
                candidates.AddRange(root.GetComponentsInChildren<MeshFilter>(false));
        }

        var solidColliders = new List<Collider>();
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            if (!scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var collider in root.GetComponentsInChildren<Collider>(false))
                {
                    if (collider.enabled && !collider.isTrigger)
                        solidColliders.Add(collider);
                }
            }
        }

        var missing = new List<MeshFilter>();
        var seen = new HashSet<MeshFilter>();
        foreach (var filter in candidates)
        {
            if (seen.Add(filter) && NeedsCollider(filter, solidColliders))
                missing.Add(filter);
        }
        return missing;
    }

    static bool NeedsCollider(MeshFilter filter, List<Collider> solidColliders)
    {
        var renderer = filter.GetComponent<MeshRenderer>();
        if (filter.sharedMesh == null || renderer == null || !renderer.enabled)
            return false;

        // Text, UI and the player rig are not part of the physical world.
        if (filter.GetComponent("TextMeshPro") != null || filter.GetComponentInParent<Canvas>() != null ||
            filter.GetComponentInParent<XROrigin>() != null)
            return false;

        if (filter.GetComponent<Collider>() != null)
            return false;

        var bounds = renderer.bounds;
        if (Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) < k_MinimumSize)
            return false;

        // A collider placed on a parent or sibling (e.g. one box around a whole lamp) already covers this mesh.
        foreach (var collider in solidColliders)
        {
            if (collider.bounds.Contains(bounds.center))
                return false;
        }
        return true;
    }

    static bool IsBuiltInCube(Mesh mesh)
    {
        return mesh.name == "Cube" && AssetDatabase.GetAssetPath(mesh).StartsWith("Library/");
    }

    static int CountTriangles(Mesh mesh)
    {
        var indexCount = 0L;
        for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            indexCount += mesh.GetIndexCount(subMesh);
        return (int)(indexCount / 3);
    }

    static string GetPath(Transform transform)
    {
        var path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
#endif
