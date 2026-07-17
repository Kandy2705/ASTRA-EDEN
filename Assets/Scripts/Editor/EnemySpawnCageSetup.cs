#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tạo nhanh Enemy Spawn Cage (lồng sinh Minecraft-style) trong scene.
/// Menu: ASTRA EDEN → Spawn → Create Enemy Spawn Cage
/// </summary>
public static class EnemySpawnCageSetup
{
    const string DefaultMeleeData =
        "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyWildClawRaptor.asset";
    const string DefaultFangData =
        "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyFangRaptor.asset";

    [MenuItem("ASTRA EDEN/Spawn/Create Enemy Spawn Cage")]
    public static void CreateCage()
    {
        Vector3 pivot = GetPivot();

        var root = new GameObject("EnemySpawnCage");
        Undo.RegisterCreatedObjectUndo(root, "Create Enemy Spawn Cage");
        root.transform.position = pivot;

        var cage = root.AddComponent<EnemySpawnCage>();

        // 4 patrol points around cage
        var patrols = new Transform[4];
        float r = 10f;
        for (int i = 0; i < 4; i++)
        {
            float ang = (90f * i) * Mathf.Deg2Rad;
            var p = new GameObject($"Patrol_{i + 1}");
            Undo.RegisterCreatedObjectUndo(p, "Patrol Point");
            p.transform.SetParent(root.transform, false);
            p.transform.position = pivot + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
            patrols[i] = p.transform;
        }

        EnemyData wild = AssetDatabase.LoadAssetAtPath<EnemyData>(DefaultMeleeData);
        EnemyData fang = AssetDatabase.LoadAssetAtPath<EnemyData>(DefaultFangData);

        var so = new SerializedObject(cage);
        so.FindProperty("spawnRadius").floatValue = 8f;
        so.FindProperty("autoPatrolRadius").floatValue = 10f;
        so.FindProperty("spawnWhen").enumValueIndex = (int)EnemySpawnCage.SpawnWhen.OnZoneStart;
        so.FindProperty("respawnMode").enumValueIndex = (int)EnemySpawnCage.RespawnMode.None;
        so.FindProperty("spawnPlacement").enumValueIndex = (int)EnemySpawnCage.SpawnPlacement.RandomInRadius;
        so.FindProperty("respawnDelay").floatValue = 30f;

        SerializedProperty patrolProp = so.FindProperty("patrolPoints");
        patrolProp.arraySize = patrols.Length;
        for (int i = 0; i < patrols.Length; i++)
        {
            patrolProp.GetArrayElementAtIndex(i).objectReferenceValue = patrols[i];
        }

        SerializedProperty entries = so.FindProperty("entries");
        entries.arraySize = 0;

        if (wild != null)
        {
            entries.InsertArrayElementAtIndex(0);
            SerializedProperty e0 = entries.GetArrayElementAtIndex(0);
            e0.FindPropertyRelative("enemyData").objectReferenceValue = wild;
            e0.FindPropertyRelative("count").intValue = 3;
            e0.FindPropertyRelative("isMiniBoss").boolValue = false;
        }

        if (fang != null)
        {
            int i = entries.arraySize;
            entries.InsertArrayElementAtIndex(i);
            SerializedProperty e1 = entries.GetArrayElementAtIndex(i);
            e1.FindPropertyRelative("enemyData").objectReferenceValue = fang;
            e1.FindPropertyRelative("count").intValue = 1;
            e1.FindPropertyRelative("isMiniBoss").boolValue = false;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log(
            "[ZoneSpawn] Đã tạo EnemySpawnCage (spawn zone).\n" +
            "1) Entries = loại enemy + số lượng\n" +
            "2) Patrol Points = lộ trình đi tuần (đã tạo 4 điểm)\n" +
            "3) Spawn When = On Zone Start (vào màn là sinh)\n" +
            "4) Respawn Mode = None (chỉ 1 lần, bật nếu muốn farm)\n" +
            "Đặt lên NavMesh → Play.");
    }

    static Vector3 GetPivot()
    {
        if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
        {
            return SceneView.lastActiveSceneView.camera.transform.position
                   + SceneView.lastActiveSceneView.camera.transform.forward * 8f;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            return player.transform.position + player.transform.forward * 6f;
        }

        return Vector3.zero;
    }
}
#endif
