#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tool dọn dẹp EnemyPatrol cũ khỏi prefab/scene để tránh conflict với EnemyAIController.
/// </summary>
public static class EnemyPatrolCleanup
{
    [MenuItem("ASTRA EDEN/Migrate/Scan EnemyPatrol Conflicts")]
    public static void ScanConflicts()
    {
        int prefabCount = 0, sceneCount = 0;
        var conflicts = new List<string>();

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var patrols = root.GetComponentsInChildren<EnemyPatrol>(true);
                if (patrols == null || patrols.Length == 0) continue;
                prefabCount++;
                bool hasAI = root.GetComponentInChildren<EnemyAIController>(true) != null;
                conflicts.Add($"[Prefab] {path}  patrols={patrols.Length}  hasAIController={hasAI}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            foreach (var root in activeScene.GetRootGameObjects())
            {
                foreach (var patrol in root.GetComponentsInChildren<EnemyPatrol>(true))
                {
                    sceneCount++;
                    bool hasAI = patrol.GetComponent<EnemyAIController>() != null;
                    conflicts.Add($"[Scene] {patrol.transform.GetHierarchyPath()}  hasAIController={hasAI}");
                }
            }
        }

        Debug.Log($"[PatrolCleanup] Scan done. Prefab containing EnemyPatrol: {prefabCount}. Scene instances: {sceneCount}.");
        foreach (var line in conflicts) Debug.Log(line);
    }

    [MenuItem("ASTRA EDEN/Migrate/Remove EnemyPatrol from Selection (Prefab)")]
    public static void RemoveFromSelection()
    {
        int removed = 0;
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var patrols = root.GetComponentsInChildren<EnemyPatrol>(true);
                if (patrols == null || patrols.Length == 0) continue;
                foreach (var p in patrols)
                {
                    Object.DestroyImmediate(p, true);
                    removed++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[PatrolCleanup] Removed EnemyPatrol from {path}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[PatrolCleanup] Total components removed: {removed}.");
    }

    [MenuItem("ASTRA EDEN/Migrate/Remove EnemyPatrol from Active Scene")]
    public static void RemoveFromActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) { Debug.LogWarning("[PatrolCleanup] No active scene."); return; }

        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var patrol in root.GetComponentsInChildren<EnemyPatrol>(true))
            {
                Undo.DestroyObjectImmediate(patrol);
                removed++;
            }
        }
        if (removed > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[PatrolCleanup] Removed {removed} EnemyPatrol component(s) from scene '{scene.name}'.");
    }

    static string GetHierarchyPath(this Transform t)
    {
        if (t == null) return "<null>";
        var sb = new System.Text.StringBuilder();
        sb.Append(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }
}
#endif
