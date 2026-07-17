#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tool sửa EnemySpawnCage: gán lại Patrol Points + ép At Patrol Points
/// để enemy sinh đúng vị trí các điểm Patrol_1, Patrol_2...
/// Menu: ASTRA EDEN → Spawn → Fix All Spawn Cages (At Patrol Points)
/// </summary>
public static class EnemySpawnCageFixTool
{
    [MenuItem("ASTRA EDEN/Spawn/Fix All Spawn Cages (At Patrol Points)")]
    public static void FixAllInOpenScenes()
    {
        int fixedCount = 0;
        var report = new StringBuilder();
        report.AppendLine("[SpawnCageFix] === Fix All Spawn Cages ===");

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded)
            {
                continue;
            }

            EnemySpawnCage[] cages = Object.FindObjectsByType<EnemySpawnCage>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (EnemySpawnCage cage in cages)
            {
                if (cage == null)
                {
                    continue;
                }

                // Chỉ fix cage thuộc scene hiện tại (tránh double nếu multi-scene)
                if (cage.gameObject.scene != scene)
                {
                    continue;
                }

                string line = FixOneCage(cage);
                report.AppendLine(line);
                fixedCount++;
                EditorUtility.SetDirty(cage);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        AssetDatabase.SaveAssets();
        report.AppendLine($"[SpawnCageFix] Done. Fixed {fixedCount} cage(s). Save scene (Ctrl/Cmd+S).");
        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog(
            "Spawn Cage Fix",
            $"Đã fix {fixedCount} EnemySpawnCage.\n\n" +
            "• Spawn Placement = At Patrol Points\n" +
            "• Patrol Points = tự lấy child tên Patrol_*\n" +
            "• Tắt auto-ring (tránh sinh ngoài rìa)\n\n" +
            "Save scene rồi Play. Xem Console log chi tiết.",
            "OK");
    }

    [MenuItem("ASTRA EDEN/Spawn/Fix Selected Spawn Cage")]
    public static void FixSelected()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Spawn Cage Fix", "Chọn một EnemySpawnCage trong Hierarchy.", "OK");
            return;
        }

        EnemySpawnCage cage = go.GetComponent<EnemySpawnCage>()
                             ?? go.GetComponentInParent<EnemySpawnCage>();
        if (cage == null)
        {
            EditorUtility.DisplayDialog("Spawn Cage Fix", "Object chọn không có EnemySpawnCage.", "OK");
            return;
        }

        string line = FixOneCage(cage);
        EditorUtility.SetDirty(cage);
        EditorSceneManager.MarkSceneDirty(cage.gameObject.scene);
        Debug.Log(line);
        EditorUtility.DisplayDialog("Spawn Cage Fix", line, "OK");
    }

    [MenuItem("ASTRA EDEN/Spawn/Validate Spawn Cages (Report)")]
    public static void ValidateAll()
    {
        EnemySpawnCage[] cages = Object.FindObjectsByType<EnemySpawnCage>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        var sb = new StringBuilder();
        sb.AppendLine($"[SpawnCageFix] Validate {cages.Length} cage(s):");

        foreach (EnemySpawnCage cage in cages)
        {
            if (cage == null)
            {
                continue;
            }

            SerializedObject so = new SerializedObject(cage);
            var placement = (EnemySpawnCage.SpawnPlacement)so.FindProperty("spawnPlacement").enumValueIndex;
            SerializedProperty patrolProp = so.FindProperty("patrolPoints");
            int assigned = 0;
            for (int i = 0; i < patrolProp.arraySize; i++)
            {
                if (patrolProp.GetArrayElementAtIndex(i).objectReferenceValue != null)
                {
                    assigned++;
                }
            }

            List<Transform> children = CollectPatrolChildren(cage.transform);
            sb.AppendLine(
                $"• '{cage.name}' placement={placement} assignedPatrols={assigned} " +
                $"childPatrols={children.Count} pos={cage.transform.position}");

            for (int i = 0; i < children.Count; i++)
            {
                sb.AppendLine($"    - {children[i].name} @ {children[i].position}");
            }

            if (placement != EnemySpawnCage.SpawnPlacement.AtPatrolPoints)
            {
                sb.AppendLine("    ⚠ Không phải AtPatrolPoints → sẽ random radius.");
            }

            if (assigned == 0 && children.Count == 0)
            {
                sb.AppendLine("    ⚠ Không có patrol points.");
            }
        }

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Spawn Cage Validate", $"Đã log {cages.Length} cage(s) ra Console.", "OK");
    }

    static string FixOneCage(EnemySpawnCage cage)
    {
        Undo.RecordObject(cage, "Fix Enemy Spawn Cage");

        SerializedObject so = new SerializedObject(cage);

        // 1) Ép At Patrol Points
        so.FindProperty("spawnPlacement").enumValueIndex =
            (int)EnemySpawnCage.SpawnPlacement.AtPatrolPoints;

        // 2) Tắt auto ring (hay tạo điểm ngoài rìa, lệch ý user)
        SerializedProperty autoCreate = so.FindProperty("autoCreatePatrolIfEmpty");
        if (autoCreate != null)
        {
            autoCreate.boolValue = false;
        }

        // 3) Thu thập Patrol_* từ hierarchy
        List<Transform> patrols = CollectPatrolChildren(cage.transform);

        // Nếu không có child tên Patrol_*, giữ mảng cũ (nếu còn ref)
        SerializedProperty patrolProp = so.FindProperty("patrolPoints");
        if (patrols.Count > 0)
        {
            patrolProp.arraySize = patrols.Count;
            for (int i = 0; i < patrols.Count; i++)
            {
                patrolProp.GetArrayElementAtIndex(i).objectReferenceValue = patrols[i];
            }
        }
        else
        {
            // Dọn null
            int write = 0;
            for (int i = 0; i < patrolProp.arraySize; i++)
            {
                Object o = patrolProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o != null)
                {
                    patrolProp.GetArrayElementAtIndex(write).objectReferenceValue = o;
                    write++;
                }
            }

            patrolProp.arraySize = write;
        }

        // 4) Log spawns bật để debug
        SerializedProperty log = so.FindProperty("logSpawns");
        if (log != null)
        {
            log.boolValue = true;
        }

        // 5) allow off navmesh — giữ đúng tọa độ patrol
        SerializedProperty allowOff = so.FindProperty("allowSpawnOffNavMesh");
        if (allowOff != null)
        {
            allowOff.boolValue = true;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        int count = so.FindProperty("patrolPoints").arraySize;
        return $"[SpawnCageFix] '{cage.name}' → AtPatrolPoints, patrolSlots={count}, " +
               $"childrenFound={patrols.Count}, autoRing=OFF @ {cage.transform.position}";
    }

    /// <summary>
    /// Lấy transform con tên chứa "Patrol" (Patrol_1, Patrol_2...), sort theo tên.
    /// Bỏ SpawnedEnemies / _AutoPatrol.
    /// </summary>
    static List<Transform> CollectPatrolChildren(Transform root)
    {
        var list = new List<Transform>();
        CollectRecursive(root, list);
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    static void CollectRecursive(Transform t, List<Transform> list)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            Transform c = t.GetChild(i);
            string n = c.name;

            if (n.StartsWith("Spawned", System.StringComparison.OrdinalIgnoreCase)
                || n == "_AutoPatrol"
                || n.IndexOf("SpawnedEnemies", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (n.IndexOf("Patrol", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                list.Add(c);
            }

            // Không recurse vào patrol point (thường là leaf)
            if (n.IndexOf("Patrol", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                CollectRecursive(c, list);
            }
        }
    }
}
#endif
