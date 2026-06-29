#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tạo prefab UI dùng chung (HUD + Menu/Inventory) và cài vào các scene gameplay/hub.
/// Menu:
/// - ASTRA EDEN / UI / 1. Create GameplayUI_Root Prefab (from World_Eden7)
/// - ASTRA EDEN / UI / 2. Install GameplayUI vào scene hiện tại
/// - ASTRA EDEN / UI / 3. Install GameplayUI vào Beacon_Camp
/// - ASTRA EDEN / UI / Run ALL (Create + Install Beacon_Camp)
/// </summary>
public static class GameplayUISetup
{
    const string PrefabPath = "Assets/_Project/Prefab/UI/GameplayUI_Root.prefab";
    const string WorldScenePath = "Assets/Scenes/World_Eden7.unity";
    const string HubScenePath = "Assets/Scenes/Beacon_Camp.unity";
    const string InventoryBootstrapScriptPath = "Assets/Scripts/Inventory/InventoryUIBootstrap.cs";
    const string GameplayBootstrapScriptPath = "Assets/Scripts/UI/GameplayUISceneBootstrap.cs";

    [MenuItem("ASTRA EDEN/UI/1. Create GameplayUI_Root Prefab (from World_Eden7)")]
    public static void CreateGameplayUIPrefab()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(WorldScenePath);
        CreateOrUpdatePrefabFromScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[GameplayUI] Đã tạo/cập nhật prefab GameplayUI_Root từ World_Eden7. Save scene.");
    }

    [MenuItem("ASTRA EDEN/UI/2. Install GameplayUI vào scene hiện tại")]
    public static void InstallGameplayUIInActiveScene()
    {
        InstallGameplayUIInCurrentScene();
    }

    public static void InstallGameplayUIInCurrentScene()
    {
        string scenePath = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(scenePath))
        {
            Debug.LogError("[GameplayUI] Scene chưa save — save scene trước khi install.");
            return;
        }

        InstallGameplayUICore();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[GameplayUI] Đã cài UI vào scene hiện tại. Save scene rồi Play test.");
    }

    [MenuItem("ASTRA EDEN/UI/3. Install GameplayUI vào Beacon_Camp")]
    public static void InstallGameplayUIInBeaconCamp()
    {
        InstallGameplayUIInScene(HubScenePath);
    }

    [MenuItem("ASTRA EDEN/UI/Run ALL (Create Prefab + Install Beacon_Camp)")]
    public static void RunAll()
    {
        CreateGameplayUIPrefab();
        InstallGameplayUIInBeaconCamp();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[GameplayUI] Hoàn tất: prefab + Beacon_Camp. Mở Beacon_Camp test HUD/Inventory (B).");
    }

    /// <summary>Gọi từ Unity batchmode: -executeMethod GameplayUISetup.RunAllBatch</summary>
    public static void RunAllBatch()
    {
        RunAll();
    }

    public static void InstallGameplayUIInScene(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
        {
            Debug.LogError("[GameplayUI] Scene path không hợp lệ.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(scenePath);
        InstallGameplayUICore();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[GameplayUI] Đã cài UI vào {scenePath}. Save scene rồi Play test.");
    }

    static void InstallGameplayUICore()
    {
        if (!File.Exists(PrefabPath))
        {
            Debug.LogWarning("[GameplayUI] Chưa có prefab — tạo từ World_Eden7 trước.");
            string activePath = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(WorldScenePath);
            CreateOrUpdatePrefabFromScene();
            if (!string.IsNullOrEmpty(activePath))
            {
                EditorSceneManager.OpenScene(activePath);
            }
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[GameplayUI] Không load được prefab tại {PrefabPath}");
            return;
        }

        if (FindSceneRoot("GameplayUI_Root") != null)
        {
            Debug.Log("[GameplayUI] Scene đã có GameplayUI_Root — bỏ qua instantiate.");
        }
        else if (HasLooseGameplayCanvases())
        {
            Debug.LogWarning(
                "[GameplayUI] Scene có HUD_Canvas/Menu_Canvas rời (chưa gom prefab). " +
                "Chạy menu 1 trên World_Eden7 hoặc xóa canvas cũ trước khi install.");
        }
        else
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                Undo.RegisterCreatedObjectUndo(instance, "GameplayUI_Root");
                instance.name = "GameplayUI_Root";
            }
        }

        EnsureEventSystem();
        EnsureGameplayManagers();
        VerticalSliceDemoSetup.FixInventorySceneWiringPublic();
        EnsureMenuCanvasOverlay();
    }

    static void CreateOrUpdatePrefabFromScene()
    {
        GameObject hud = GameObject.Find("HUD_Canvas");
        GameObject menu = GameObject.Find("Menu_Canvas");
        if (hud == null && menu == null)
        {
            Debug.LogError("[GameplayUI] Không tìm thấy HUD_Canvas / Menu_Canvas trong scene.");
            return;
        }

        GameObject root = FindSceneRoot("GameplayUI_Root");
        if (root == null)
        {
            root = new GameObject("GameplayUI_Root");
            Undo.RegisterCreatedObjectUndo(root, "GameplayUI_Root");
        }

        if (hud != null && hud.transform.parent != root.transform)
        {
            Undo.SetTransformParent(hud.transform, root.transform, "Parent HUD_Canvas");
        }

        if (menu != null && menu.transform.parent != root.transform)
        {
            Undo.SetTransformParent(menu.transform, root.transform, "Parent Menu_Canvas");
        }

        EnsureBootstrapOnRoot(root);
        RemoveMissingScripts(root);
        if (hud != null)
        {
            RemoveMissingScripts(hud);
        }

        if (menu != null)
        {
            RemoveMissingScripts(menu);
        }

        EnsureFolder(Path.GetDirectoryName(PrefabPath)?.Replace('\\', '/'));

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
            Debug.Log($"[GameplayUI] Cập nhật prefab: {PrefabPath}");
        }
        else
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
            Debug.Log($"[GameplayUI] Tạo prefab mới: {PrefabPath}");
        }

        AssetDatabase.SaveAssets();
    }

    static void EnsureBootstrapOnRoot(GameObject root)
    {
        if (root.GetComponent<GameplayUISceneBootstrap>() == null)
        {
            Undo.AddComponent<GameplayUISceneBootstrap>(root);
        }

        MonoScript inventoryBootstrap = AssetDatabase.LoadAssetAtPath<MonoScript>(InventoryBootstrapScriptPath);
        if (inventoryBootstrap != null)
        {
            System.Type inventoryType = inventoryBootstrap.GetClass();
            GameObject menu = GameObject.Find("Menu_Canvas");
            if (inventoryType != null && menu != null)
            {
                RemoveMissingScripts(menu);
                if (menu.GetComponent(inventoryType) == null)
                {
                    Undo.AddComponent(menu, inventoryType);
                }
            }
        }
    }

    static void RemoveMissingScripts(GameObject go)
    {
        if (go == null)
        {
            return;
        }

        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        if (removed > 0)
        {
            Debug.LogWarning($"[GameplayUI] Đã xóa {removed} missing script trên '{go.name}'.");
        }
    }

    static void EnsureGameplayManagers()
    {
        GameObject managers = GameObject.Find("Managers");
        if (managers == null)
        {
            managers = new GameObject("Managers");
            Undo.RegisterCreatedObjectUndo(managers, "Managers");
        }

        AddComponentIfMissing<GameDataManager>(managers);
        AddComponentIfMissing<InventoryToggleController>(managers);
        AddComponentIfMissing<ItemRegistryInstaller>(managers);

        Transform screenTransform = managers.transform.Find("InventoryScreenController");
        if (screenTransform == null)
        {
            GameObject screenObject = new GameObject("InventoryScreenController");
            Undo.RegisterCreatedObjectUndo(screenObject, "InventoryScreenController");
            screenObject.transform.SetParent(managers.transform, false);
            Undo.AddComponent<InventoryScreenController>(screenObject);
        }
        else if (screenTransform.GetComponent<InventoryScreenController>() == null)
        {
            Undo.AddComponent<InventoryScreenController>(screenTransform.gameObject);
        }

        DisableDuplicateChild(managers.transform, "GameDataManager");
        DisableDuplicateChild(managers.transform, "UIRuntimeController");
    }

    static void DisableDuplicateChild(Transform managersRoot, string childName)
    {
        Transform child = managersRoot.Find(childName);
        if (child != null && child.gameObject.activeSelf)
        {
            Undo.RecordObject(child.gameObject, "Disable duplicate");
            child.gameObject.SetActive(false);
        }
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(eventSystem, "EventSystem");
        eventSystem.AddComponent<EventSystem>();
        AddUiInputModule(eventSystem);
    }

    static void AddUiInputModule(GameObject eventSystemObject)
    {
        System.Type inputSystemModule = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModule != null && eventSystemObject.GetComponent(inputSystemModule) == null)
        {
            Undo.AddComponent(eventSystemObject, inputSystemModule);
            return;
        }

        if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
        {
            Undo.AddComponent<StandaloneInputModule>(eventSystemObject);
        }
    }

    static void EnsureMenuCanvasOverlay()
    {
        GameObject menuCanvas = GameObject.Find("Menu_Canvas");
        if (menuCanvas == null)
        {
            return;
        }

        Canvas canvas = menuCanvas.GetComponent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        if (canvas.sortingOrder < 10)
        {
            canvas.sortingOrder = 10;
        }
    }

    static bool HasLooseGameplayCanvases()
    {
        GameObject hud = GameObject.Find("HUD_Canvas");
        GameObject menu = GameObject.Find("Menu_Canvas");
        Transform root = FindSceneRoot("GameplayUI_Root")?.transform;

        bool hudLoose = hud != null && (root == null || hud.transform.parent != root);
        bool menuLoose = menu != null && (root == null || menu.transform.parent != root);
        return hudLoose || menuLoose;
    }

    static GameObject FindSceneRoot(string objectName)
    {
        GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            GameObject candidate = all[i];
            if (candidate.name == objectName && candidate.transform.parent == null)
            {
                return candidate;
            }
        }

        return null;
    }

    static T AddComponentIfMissing<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif