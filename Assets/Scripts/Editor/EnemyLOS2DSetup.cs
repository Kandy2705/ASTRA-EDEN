#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wire Aillieo UnityLOS2D (com.aillieo.los-2d) cho enemy + player.
/// Menu: ASTRA EDEN → Enemies → Setup Aillieo LOS2D …
/// </summary>
public static class EnemyLOS2DSetup
{
    static readonly string[] EnemyPrefabPaths =
    {
        "Assets/_Project/Prefab/Enemy.prefab",
        "Assets/_Project/Prefab/Enemy_FangRaptor.prefab",
        "Assets/_Project/Prefab/Enemy_WildClawRaptor.prefab",
        "Assets/_Project/Prefab/Enemy_MiniBoss_Velociraptor.prefab",
        "Assets/_Project/Prefab/Enemy_Boss_BeachTyran.prefab",
    };

    const string PlayerPrefabPath =
        "Assets/Prefabs/Vroids/Seeker Prototype/Seeker Prototype Nu.prefab";

    [MenuItem("ASTRA EDEN/Enemies/Setup Aillieo LOS2D On Selected Enemy")]
    public static void SetupSelectedEnemy()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogError("[LOS2D] Chọn enemy root trong Hierarchy / Prefab mode.");
            return;
        }

        GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
        if (root == null)
        {
            root = go;
        }

        SetupEnemyRoot(root, enableMesh: false);
        EditorUtility.SetDirty(root);
        Debug.Log($"[LOS2D] Wired on '{root.name}'. Player cần PlayerLOSTarget.");
    }

    [MenuItem("ASTRA EDEN/Enemies/Setup Aillieo LOS2D On ALL Enemy Prefabs")]
    public static void SetupAllEnemyPrefabs()
    {
        int ok = 0;
        foreach (string path in EnemyPrefabPaths)
        {
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[LOS2D] Missing prefab: {path}");
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool mesh = path.Contains("Boss") || path.Contains("Tyran");
                SetupEnemyRoot(root, enableMesh: mesh);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                ok++;
                Debug.Log($"[LOS2D] OK → {path} (mesh={mesh})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[LOS2D] Enemy prefabs: {ok}/{EnemyPrefabPaths.Length}");
    }

    [MenuItem("ASTRA EDEN/Enemies/Setup Aillieo LOS2D On Player Prefab")]
    public static void SetupPlayerPrefab()
    {
        if (!System.IO.File.Exists(PlayerPrefabPath))
        {
            Debug.LogError($"[LOS2D] Player prefab not found: {PlayerPrefabPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            if (root.GetComponent<PlayerLOSTarget>() == null)
            {
                root.AddComponent<PlayerLOSTarget>();
            }

            // Ensure a collidable volume for LOSTarget ray hits.
            if (root.GetComponent<Collider>() == null)
            {
                Transform proxy = root.transform.Find("LOS2D_TargetProxy");
                if (proxy == null)
                {
                    var proxyGo = new GameObject("LOS2D_TargetProxy");
                    proxyGo.transform.SetParent(root.transform, false);
                    proxyGo.transform.localPosition = new Vector3(0f, 1f, 0f);
                    var box = proxyGo.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.6f, 1.6f, 0.6f);
                    box.isTrigger = true;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log($"[LOS2D] PlayerLOSTarget on {PlayerPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem("ASTRA EDEN/Enemies/Setup Aillieo LOS2D (Player + ALL Enemies)")]
    public static void SetupAll()
    {
        SetupPlayerPrefab();
        SetupAllEnemyPrefabs();
        Debug.Log("[LOS2D] Full setup done. Play test: enemy detects player in FOV + LOS.");
    }

    public static void SetupEnemyRoot(GameObject root, bool enableMesh)
    {
        EnemySensor sensor = root.GetComponent<EnemySensor>();
        if (sensor == null)
        {
            sensor = root.AddComponent<EnemySensor>();
        }

        EnemyLOS2DBridge bridge = root.GetComponent<EnemyLOS2DBridge>();
        if (bridge == null)
        {
            bridge = root.AddComponent<EnemyLOS2DBridge>();
        }

        EnemyAIController ai = root.GetComponent<EnemyAIController>();
        bool flip = false;
        if (ai != null)
        {
            SerializedObject aiSo = new SerializedObject(ai);
            SerializedProperty flipProp = aiSo.FindProperty("flipForward180");
            if (flipProp != null)
            {
                flip = flipProp.boolValue;
            }
        }

        SerializedObject sensorSo = new SerializedObject(sensor);
        sensorSo.FindProperty("useAillieoLos2D").boolValue = true;
        sensorSo.FindProperty("useMultiRayFov").boolValue = false;
        sensorSo.FindProperty("aillieoBridge").objectReferenceValue = bridge;
        sensorSo.FindProperty("aillieoDrawMesh").boolValue = enableMesh;
        sensorSo.FindProperty("flipForward180").boolValue = flip;
        sensorSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject bridgeSo = new SerializedObject(bridge);
        bridgeSo.FindProperty("flipForward180").boolValue = flip;
        bridgeSo.FindProperty("createMeshVisual").boolValue = enableMesh;
        bridgeSo.FindProperty("drawSightMesh").boolValue = enableMesh;
        // Mask: Default (0) + Player if exists
        int playerLayer = LayerMask.GetMask("Player");
        int mask = playerLayer != 0 ? (playerLayer | 1) : ~0;
        bridgeSo.FindProperty("maskForEvent").intValue = mask;
        bridgeSo.FindProperty("maskForRender").intValue = mask;
        bridgeSo.FindProperty("eyeHeight").floatValue = 1.2f;
        bridgeSo.ApplyModifiedPropertiesWithoutUndo();

        // Runtime ensure runs in Awake — force once for prefab serialization of children.
        bridge.ConfigureFromSensor(sensor, flip);
    }
}
#endif
