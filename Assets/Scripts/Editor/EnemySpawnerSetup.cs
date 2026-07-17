#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tạo EnemySpawnZone trong scene hiện tại và gán prefab/data mặc định.
/// Menu: ASTRA EDEN → Spawn → Create Enemy Spawn Zone In Scene
/// </summary>
public static class EnemySpawnerSetup
{
    const string DefaultEnemyPrefabPath = "Assets/_Project/Prefab/Enemy_WildClawRaptor.prefab";
    const string DefaultMeleeDataPath = "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyWildClawRaptor.asset";
    const string DefaultRangedDataPath = "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyFangRaptor.asset";
    const string DefaultBruteDataPath = "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyArmoredHerbivoreJuvenile.asset";

    [MenuItem("ASTRA EDEN/Spawn/Create Enemy Spawn Zone In Scene")]
    public static void CreateSpawnZoneInScene()
    {
        Vector3 pivot = GetSpawnPivot();

        var root = new GameObject("EnemySpawnZone");
        Undo.RegisterCreatedObjectUndo(root, "Create Enemy Spawn Zone");

        var spawner = root.AddComponent<EnemySpawner>();
        var spawnedParent = new GameObject("SpawnedEnemies");
        spawnedParent.transform.SetParent(root.transform, false);
        Undo.RegisterCreatedObjectUndo(spawnedParent, "Create Spawned Enemies Parent");

        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("defaultEnemyPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(DefaultEnemyPrefabPath);
        so.FindProperty("defaultEnemyData").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<EnemyData>(DefaultMeleeDataPath);
        so.FindProperty("spawnedEnemiesParent").objectReferenceValue = spawnedParent.transform;
        so.FindProperty("spawnOnStart").boolValue = true;
        so.FindProperty("logSpawns").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        CreateSpawnPoint(root.transform, "Spawn_WildClaw_01", pivot + new Vector3(8f, 0f, 6f),
            AssetDatabase.LoadAssetAtPath<EnemyData>(DefaultMeleeDataPath));
        CreateSpawnPoint(root.transform, "Spawn_WildClaw_02", pivot + new Vector3(-10f, 0f, 4f),
            AssetDatabase.LoadAssetAtPath<EnemyData>(DefaultMeleeDataPath));
        CreateSpawnPoint(root.transform, "Spawn_Fang_01", pivot + new Vector3(14f, 0f, -8f),
            AssetDatabase.LoadAssetAtPath<EnemyData>(DefaultRangedDataPath));
        CreateSpawnPoint(root.transform, "Spawn_Tank_01", pivot + new Vector3(-14f, 0f, -10f),
            AssetDatabase.LoadAssetAtPath<EnemyData>(DefaultBruteDataPath));

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log(
            "[EnemySpawn] Đã tạo EnemySpawnZone với 4 spawn point (2 WildClaw green, 1 Fang sand, 1 tank data). " +
            "Dời các điểm spawn lên NavMesh trong World_Eden7 rồi Play để test.");
    }

    static void CreateSpawnPoint(Transform parent, string name, Vector3 worldPos, EnemyData data)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Spawn Point");
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;

        var point = go.AddComponent<EnemySpawnPoint>();
        SerializedObject so = new SerializedObject(point);
        so.FindProperty("enemyData").objectReferenceValue = data;
        so.FindProperty("patrolRadius").floatValue = 5f;
        so.FindProperty("autoPatrolCount").intValue = 4;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Vector3 GetSpawnPivot()
    {
        if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
        {
            return SceneView.lastActiveSceneView.camera.transform.position;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            return player.transform.position;
        }

        return Vector3.zero;
    }
}
#endif