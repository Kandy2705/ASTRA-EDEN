#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Bake NavMesh gồm terrain + sàn nhà (Outpost) để enemy đi được trong nhà.
/// Menu: ASTRA EDEN → Navigation → Bake NavMesh (Terrain + Buildings)
/// </summary>
public static class NavMeshBuildingBake
{
    const string WorldScenePath = "Assets/Scenes/World_Eden7.unity";
    const string NavMeshAssetPath = "Assets/Scenes/World_Eden7/NavMesh-Terrain.asset";
    const string WalkFloorName = "_NavMesh_WalkFloor";

    [MenuItem("ASTRA EDEN/Navigation/Bake NavMesh (Terrain + Buildings)")]
    public static void BakeFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        BakeWorldEden7();
    }

    /// <summary>Batchmode: -executeMethod NavMeshBuildingBake.BakeBatch</summary>
    public static void BakeBatch()
    {
        try
        {
            BakeWorldEden7();
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    public static void BakeWorldEden7()
    {
        Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);

        // 1) Thêm collider sàn walkable cho Outpost (nếu thiếu phủ sàn).
        int floorsAdded = EnsureBuildingWalkFloors();
        Debug.Log($"[NavBake] Walk-floor colliders ensured/updated: {floorsAdded}");

        // 2) Cấu hình NavMeshSurface trên scene.
        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>(FindObjectsInactive.Include);
        if (surface == null)
        {
            var go = new GameObject("NavMesh Surface");
            surface = go.AddComponent<NavMeshSurface>();
            Undo.RegisterCreatedObjectUndo(go, "NavMesh Surface");
        }

        surface.agentTypeID = 0; // Humanoid default
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.defaultArea = 0; // Walkable
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.minRegionArea = 0.5f; // cho sàn nhỏ / mảnh

        // 3) Bake
        surface.BuildNavMesh();

        // 4) Gắn/save NavMeshData asset
        if (surface.navMeshData != null)
        {
            string folder = Path.GetDirectoryName(NavMeshAssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            }
            else
            {
                // Ghi đè data cũ bằng bản bake mới
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.navMeshData = existing;
                EditorUtility.SetDirty(existing);
            }
        }

        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 5) Log sanity
        bool hasPath = NavMesh.SamplePosition(
            FindOutpostSamplePoint(),
            out NavMeshHit hit,
            3f,
            NavMesh.AllAreas);

        Debug.Log(
            $"[NavBake] DONE. Surface='{surface.name}' geometry=PhysicsColliders collect=All. " +
            $"Outpost NavMesh sample: {(hasPath ? $"OK @ {hit.position}" : "FAIL — check colliders/floors")}");
    }

    static int EnsureBuildingWalkFloors()
    {
        int count = 0;
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var outposts = new List<Transform>();

        foreach (GameObject root in roots)
        {
            CollectOutposts(root.transform, outposts);
        }

        foreach (Transform outpost in outposts)
        {
            if (EnsureWalkFloorUnder(outpost))
            {
                count++;
            }
        }

        return count;
    }

    static void CollectOutposts(Transform t, List<Transform> results)
    {
        if (t.name.IndexOf("Eden Warden Outpost", System.StringComparison.OrdinalIgnoreCase) >= 0
            || t.name.IndexOf("Outpost", System.StringComparison.OrdinalIgnoreCase) >= 0
               && t.name.IndexOf("Warden", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            results.Add(t);
        }

        for (int i = 0; i < t.childCount; i++)
        {
            CollectOutposts(t.GetChild(i), results);
        }
    }

    static bool EnsureWalkFloorUnder(Transform building)
    {
        if (building == null)
        {
            return false;
        }

        // Bounds từ collider + renderer
        Bounds bounds;
        if (!TryGetWorldBounds(building.gameObject, out bounds))
        {
            Debug.LogWarning($"[NavBake] Không lấy được bounds cho '{building.name}'.");
            return false;
        }

        Transform floorT = building.Find(WalkFloorName);
        GameObject floorGo;
        if (floorT == null)
        {
            floorGo = new GameObject(WalkFloorName);
            Undo.RegisterCreatedObjectUndo(floorGo, "NavMesh Walk Floor");
            floorGo.transform.SetParent(building, false);
        }
        else
        {
            floorGo = floorT.gameObject;
        }

        // Sàn mỏng phủ footprint, hơi cao hơn đáy bounds (tránh lún dưới đất).
        float floorY = bounds.min.y + 0.15f;
        // Nếu building pivot đã trên sàn, dùng center.y thấp
        if (bounds.size.y > 1f)
        {
            floorY = bounds.min.y + Mathf.Min(0.5f, bounds.size.y * 0.05f);
        }

        floorGo.transform.position = new Vector3(bounds.center.x, floorY, bounds.center.z);
        floorGo.transform.rotation = Quaternion.identity;
        floorGo.layer = 0;

        BoxCollider box = floorGo.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider>(floorGo);
        }

        // Local size: parent scale
        Vector3 lossy = floorGo.transform.lossyScale;
        float sx = Mathf.Max(bounds.size.x / Mathf.Max(Mathf.Abs(lossy.x), 0.001f), 1f);
        float sz = Mathf.Max(bounds.size.z / Mathf.Max(Mathf.Abs(lossy.z), 0.001f), 1f);
        // Phủ gần full footprint, hơi thụt mép
        box.center = Vector3.zero;
        box.size = new Vector3(sx * 0.92f, 0.12f, sz * 0.92f);
        box.isTrigger = false;

        // NavMeshModifier: Walkable (optional package component)
        var modifier = floorGo.GetComponent<NavMeshModifier>();
        if (modifier == null)
        {
            modifier = Undo.AddComponent<NavMeshModifier>(floorGo);
        }

        modifier.overrideArea = true;
        modifier.area = 0; // Walkable

        EditorUtility.SetDirty(floorGo);
        EditorUtility.SetDirty(building.gameObject);
        return true;
    }

    static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(root.transform.position, Vector3.one);
        bool any = false;

        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || !cols[i].enabled)
            {
                continue;
            }

            if (!any)
            {
                bounds = cols[i].bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(cols[i].bounds);
            }
        }

        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null)
            {
                continue;
            }

            if (!any)
            {
                bounds = rends[i].bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(rends[i].bounds);
            }
        }

        if (!any)
        {
            // fallback footprint
            bounds = new Bounds(root.transform.position, new Vector3(40f, 10f, 40f));
            return true;
        }

        return true;
    }

    static Vector3 FindOutpostSamplePoint()
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.IndexOf("Eden Warden Outpost", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return all[i].position + Vector3.up * 0.5f;
                }
            }
        }

        return Vector3.zero;
    }
}
#endif
