#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One-click setup cho vertical slice demo.
/// Menu:
/// - ASTRA EDEN / Demo / 1. Create Demo Data Assets
/// - ASTRA EDEN / Demo / 2. Setup World_Eden7
/// - ASTRA EDEN / Demo / 3. Setup Beacon_Camp Shop
/// - ASTRA EDEN / Demo / 4. Wire Player Prefab
/// </summary>
public static class VerticalSliceDemoSetup
{
    const string GoldPath = "Assets/_Project/ScriptableObjects/Items/SO_Item_Gold.asset";
    const string PotionPath = "Assets/_Project/ScriptableObjects/Items/Loot/SO_Item_ItemHealthPotionSmall.asset";
    const string CrystalOrePath = "Assets/_Project/ScriptableObjects/Items/Loot/SO_Item_ItemCrystalOre.asset";
    const string CoreDustPath = "Assets/_Project/ScriptableObjects/Items/Loot/SO_Item_ItemCoreDust.asset";
    const string EnemyPrefabPath = "Assets/_Project/Prefab/Enemy.prefab";
    const string VelociraptorSourcePath = "Assets/Packages/PBRVelociraptor/Prefabs/PBR/15K/PBR_Velociraptor_Orange.prefab";
    const string MiniBossPrefabPath = "Assets/_Project/Prefab/Enemy_MiniBoss_Velociraptor.prefab";
    const string PackLeaderDataPath = "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyRaptorPackLeader.asset";
    const string CompyPrefabPath = "Assets/Prefabs/Enemy/compsognathus-compy-dinosaurs (3).prefab";
    const string CompanionPrefabPath = "Assets/_Project/Prefab/Companion_Compy.prefab";
    const string ShopDataPath = "Assets/_Project/ScriptableObjects/Shop/SO_Shop_BeaconCamp.asset";
    const string ResourceCrystalPath = "Assets/_Project/ScriptableObjects/Gathering/SO_ResourceNode_Crystal.asset";
    const string ResourceCorePath = "Assets/_Project/ScriptableObjects/Gathering/SO_ResourceNode_CoreDust.asset";
    const string PlayerPrefabPath = "Assets/Prefabs/Vroids/Seeker Prototype/Seeker Prototype Nu.prefab";
    const string InventoryUIBootstrapScriptPath = "Assets/Scripts/Inventory/InventoryUIBootstrap.cs";

    [MenuItem("ASTRA EDEN/Demo/0. Run ALL Demo Setup (1→5)")]
    public static void RunAllSetup()
    {
        CreateDemoDataAssets();
        CreateMiniBossVelociraptorPrefab();
        WirePlayerPrefab();
        SetupWorldEden7();
        SetupBeaconCampShop();
        SetupCampZonePortals();
        Debug.Log("[DemoSetup] ALL steps finished. Save scenes + test theo huong dan.");
    }

    [MenuItem("ASTRA EDEN/Demo/1. Create Demo Data Assets")]
    public static void CreateDemoDataAssets()
    {
        EnsureFolder("Assets/_Project/ScriptableObjects/Shop");
        EnsureFolder("Assets/_Project/ScriptableObjects/Gathering");

        CreateResourceNodeAsset(ResourceCrystalPath, "node_crystal", "Crystal Node", CrystalOrePath, 1, 2, 2f, 45f);
        CreateResourceNodeAsset(ResourceCorePath, "node_core_dust", "Core Dust Vein", CoreDustPath, 1, 3, 2.5f, 60f);
        CreateShopAsset();
        CreateCompanionPrefab();
        PopulateItemRegistryLists();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DemoSetup] Demo data assets created.");
    }

    [MenuItem("ASTRA EDEN/Demo/2. Setup World_Eden7")]
    public static void SetupWorldEden7()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene("Assets/Scenes/World_Eden7.unity");
        Vector3 pivot = GetScenePivot();

        SetupManagers(pivot);
        SetupEnemySpawnZone(pivot);
        WireMiniBossSpawn();
        SetupResourceNodes(pivot);
        SetupZoneSystems();
        SetupHudPanels();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[DemoSetup] World_Eden7 setup complete. Dời spawn/resource points lên NavMesh nếu cần.");
    }

    [MenuItem("ASTRA EDEN/Demo/5. Setup Camp <-> Zone Portals")]
    public static void SetupCampZonePortals()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        SetupPortalInScene(
            "Assets/Scenes/Beacon_Camp.unity",
            "Portal_ToZone",
            "World_Eden7",
            new Vector3(0f, 0.114f, -9f),
            true);

        SetupPortalInScene(
            "Assets/Scenes/World_Eden7.unity",
            "Portal_ToCamp",
            "Beacon_Camp",
            new Vector3(2261.09f, 86.47f, 880f),
            true);

        Debug.Log("[DemoSetup] Camp <-> Zone portals ready.");
    }

    [MenuItem("ASTRA EDEN/Demo/Create Mini-Boss Velociraptor Prefab")]
    public static void CreateMiniBossVelociraptorPrefab()
    {
        GameObject sourceRoot = PrefabUtility.LoadPrefabContents(VelociraptorSourcePath);
        GameObject templateRoot = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            sourceRoot.name = "Enemy_MiniBoss_Velociraptor";
            sourceRoot.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            EnemyData packLeader = LoadEnemyData(PackLeaderDataPath);
            AddEnemyGameplayComponents(sourceRoot, templateRoot, packLeader);

            EnsureFolder(Path.GetDirectoryName(MiniBossPrefabPath)?.Replace('\\', '/'));
            PrefabUtility.SaveAsPrefabAsset(sourceRoot, MiniBossPrefabPath);

            if (packLeader != null)
            {
                SerializedObject dataSo = new SerializedObject(packLeader);
                dataSo.FindProperty("enemyPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(MiniBossPrefabPath);
                dataSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(packLeader);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[DemoSetup] Created mini-boss prefab at {MiniBossPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(sourceRoot);
            PrefabUtility.UnloadPrefabContents(templateRoot);
        }
    }

    [MenuItem("ASTRA EDEN/Demo/3. Setup Beacon_Camp Shop")]
    public static void SetupBeaconCampShop()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene("Assets/Scenes/Beacon_Camp.unity");
        EnsureEventSystem();
        SetupShopInHub();
        EnsureItemRegistryOnManagers();
        GameplayUISetup.InstallGameplayUIInCurrentScene();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[DemoSetup] Beacon_Camp shop + GameplayUI setup complete.");
    }

    [MenuItem("ASTRA EDEN/Demo/4. Wire Player Prefab")]
    public static void WirePlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            AddComponentIfMissing<PlayerInventoryService>(root);
            AddComponentIfMissing<PlayerInteractController>(root);
            AddComponentIfMissing<PlayerKnockbackReceiver>(root);
            AddComponentIfMissing<CompanionSummonController>(root);

            CompanionSummonController summon = root.GetComponent<CompanionSummonController>();
            SerializedObject summonSo = new SerializedObject(summon);
            summonSo.FindProperty("companionPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(CompanionPrefabPath);
            summonSo.FindProperty("summonOnStart").boolValue = true;
            summonSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[DemoSetup] Player prefab wired with inventory/interact/companion.");
    }

    public static void FixInventorySceneWiringPublic()
    {
        GameObject managers = GameObject.Find("Managers");
        if (managers != null)
        {
            WireInventoryUI(managers);
        }

        EnsureMenuCanvasRenders();
    }

    static void SetupManagers(Vector3 pivot)
    {
        GameObject managers = GameObject.Find("Managers") ?? new GameObject("Managers");
        Undo.RegisterCreatedObjectUndo(managers, "Managers");

        AddComponentIfMissing<GameDataManager>(managers);
        ItemRegistryInstaller installer = AddComponentIfMissing<ItemRegistryInstaller>(managers);
        PopulateInstaller(installer);

        WireInventoryUI(managers);
        EnsureMenuCanvasRenders();
    }

    static void EnsureMenuCanvasRenders()
    {
        GameObject menuCanvasObject = GameObject.Find("Menu_Canvas");
        if (menuCanvasObject == null)
        {
            return;
        }

        EnsureInventoryUIBootstrap(menuCanvasObject);

        Canvas canvas = menuCanvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                canvas.worldCamera = mainCamera;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        if (canvas.sortingOrder < 10)
        {
            canvas.sortingOrder = 10;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void EnsureInventoryUIBootstrap(GameObject menuCanvasObject)
    {
        if (menuCanvasObject == null)
        {
            return;
        }

        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(InventoryUIBootstrapScriptPath);
        if (script == null)
        {
            Debug.LogWarning("[DemoSetup] Không tìm thấy InventoryUIBootstrap.cs.");
            return;
        }

        System.Type bootstrapType = script.GetClass();
        if (bootstrapType == null)
        {
            Debug.LogWarning("[DemoSetup] InventoryUIBootstrap chưa compile — bỏ qua add component.");
            return;
        }

        if (menuCanvasObject.GetComponent(bootstrapType) == null)
        {
            Undo.AddComponent(menuCanvasObject, bootstrapType);
        }
    }

    static void WireInventoryUI(GameObject managers)
    {
        InventoryToggleController toggle = AddComponentIfMissing<InventoryToggleController>(managers);
        InventoryScreenController screen = FindSceneComponent<InventoryScreenController>();

        GameObject inventoryRoot = FindChildByName("Panels", "Inventory") ?? GameObject.Find("Ingame_Inventory");
        GameObject gameplayHud = GameObject.Find("HUD_Canvas") ?? GameObject.Find("GameplayHUD");

        SerializedObject toggleSo = new SerializedObject(toggle);
        toggleSo.FindProperty("toggleKey").intValue = (int)Key.B;

        if (inventoryRoot != null)
        {
            toggleSo.FindProperty("inventoryRoot").objectReferenceValue = inventoryRoot;
        }

        if (gameplayHud != null)
        {
            toggleSo.FindProperty("gameplayHudCanvas").objectReferenceValue = gameplayHud;
        }

        if (screen != null)
        {
            toggleSo.FindProperty("inventoryScreenController").objectReferenceValue = screen;

            SerializedObject screenSo = new SerializedObject(screen);
            screenSo.FindProperty("inventoryToggleController").objectReferenceValue = toggle;
            screenSo.ApplyModifiedPropertiesWithoutUndo();
        }

        toggleSo.ApplyModifiedPropertiesWithoutUndo();

        Transform uiRuntime = managers.transform.Find("UIRuntimeController");
        if (uiRuntime != null)
        {
            InventoryToggleController duplicate = uiRuntime.GetComponent<InventoryToggleController>();
            if (duplicate != null && duplicate != toggle)
            {
                duplicate.enabled = false;
            }
        }
    }

    static void SetupEnemySpawnZone(Vector3 pivot)
    {
        if (GameObject.Find("EnemySpawnZone") != null)
        {
            return;
        }

        var root = new GameObject("EnemySpawnZone");
        Undo.RegisterCreatedObjectUndo(root, "EnemySpawnZone");

        var spawner = root.AddComponent<EnemySpawner>();
        var spawnedParent = new GameObject("SpawnedEnemies");
        spawnedParent.transform.SetParent(root.transform, false);

        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("defaultEnemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        so.FindProperty("defaultEnemyData").objectReferenceValue =
            LoadEnemyData("Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyScavengerLizard.asset");
        so.FindProperty("spawnedEnemiesParent").objectReferenceValue = spawnedParent.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        CreateEnemySpawnPoint(root.transform, "Spawn_Melee_01", pivot + new Vector3(10f, 0f, 8f),
            LoadEnemyData("Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyScavengerLizard.asset"), false);
        CreateEnemySpawnPoint(root.transform, "Spawn_Melee_02", pivot + new Vector3(-12f, 0f, 6f),
            LoadEnemyData("Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyFangRaptor.asset"), false);
        CreateEnemySpawnPoint(root.transform, "Spawn_Ranged_01", pivot + new Vector3(16f, 0f, -6f),
            LoadEnemyData("Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyYoungSpitter.asset"), false);
        CreateBossSpawnPoint(root.transform, "Spawn_MiniBoss", pivot + new Vector3(0f, 0f, 22f),
            LoadEnemyData("Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyRaptorPackLeader.asset"));
    }

    static void CreateBossSpawnPoint(Transform parent, string name, Vector3 pos, EnemyData data)
    {
        CreateEnemySpawnPoint(parent, name, pos, data, true);
        WireMiniBossSpawnOnPoint(parent.Find(name));
    }

    static void WireMiniBossSpawn()
    {
        GameObject spawn = GameObject.Find("Spawn_MiniBoss");
        if (spawn == null)
        {
            Debug.LogWarning("[DemoSetup] Không tìm thấy Spawn_MiniBoss trong scene.");
            return;
        }

        WireMiniBossSpawnOnPoint(spawn.transform);
    }

    static void WireMiniBossSpawnOnPoint(Transform spawnTransform)
    {
        if (spawnTransform == null)
        {
            return;
        }

        GameObject miniBossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MiniBossPrefabPath);
        if (miniBossPrefab == null)
        {
            Debug.LogWarning("[DemoSetup] Chưa có Enemy_MiniBoss_Velociraptor — chạy Create Mini-Boss Velociraptor Prefab trước.");
            return;
        }

        EnemySpawnPoint point = spawnTransform.GetComponent<EnemySpawnPoint>();
        if (point == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(point);
        so.FindProperty("prefabOverride").objectReferenceValue = miniBossPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetupPortalInScene(string scenePath, string portalName, string targetScene, Vector3 position, bool restorePosition)
    {
        EditorSceneManager.OpenScene(scenePath);
        EnsureScenePortal(portalName, targetScene, position, restorePosition);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    static void EnsureScenePortal(string portalName, string targetScene, Vector3 position, bool restorePosition)
    {
        GameObject portalObject = GameObject.Find(portalName);
        if (portalObject == null)
        {
            portalObject = new GameObject(portalName);
            Undo.RegisterCreatedObjectUndo(portalObject, portalName);
        }

        portalObject.transform.position = position;

        BoxCollider collider = EnsureComponentOnRoot<BoxCollider>(portalObject);
        if (collider != null)
        {
            collider.isTrigger = true;
            collider.size = new Vector3(4f, 3f, 4f);
            collider.center = new Vector3(0f, 1.5f, 0f);
        }

        ScenePortalFade portal = EnsureComponentOnRoot<ScenePortalFade>(portalObject);
        if (portal == null)
        {
            Debug.LogWarning($"[DemoSetup] Không thể gắn ScenePortalFade lên '{portalName}'.");
            return;
        }

        SerializedObject portalSo = new SerializedObject(portal);
        portalSo.FindProperty("targetSceneName").stringValue = targetScene;
        portalSo.FindProperty("restoreSavedPositionOnLoad").boolValue = restorePosition;
        portalSo.FindProperty("showDebugLog").boolValue = true;
        portalSo.ApplyModifiedPropertiesWithoutUndo();

        Transform visual = portalObject.transform.Find("PortalVisual");
        if (visual == null)
        {
            GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualObject.name = "PortalVisual";
            visualObject.transform.SetParent(portalObject.transform, false);
            visualObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            visualObject.transform.localScale = new Vector3(2.2f, 0.15f, 2.2f);
            Object.DestroyImmediate(visualObject.GetComponent<Collider>());

            Renderer renderer = visualObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial.color = new Color(0.25f, 0.65f, 1f, 0.85f);
            }
        }
    }

    static void AddEnemyGameplayComponents(GameObject root, GameObject template, EnemyData enemyData)
    {
        // Body blocking: match Enemy template (BoxCollider + kinematic Rigidbody).
        // Older mini-boss builds incorrectly used a default CapsuleCollider with no Rigidbody.
        DestroyComponent<CapsuleCollider>(root);

        BoxCollider templateBox = template != null ? template.GetComponent<BoxCollider>() : null;
        BoxCollider bodyCollider = EnsureComponentOnRoot<BoxCollider>(root);
        if (bodyCollider != null)
        {
            if (templateBox != null)
            {
                bodyCollider.center = templateBox.center;
                bodyCollider.size = templateBox.size;
            }
            else
            {
                // Fallback bounds fitted to PBR Velociraptor local mesh (same as Enemy.prefab).
                bodyCollider.center = new Vector3(0.052734375f, 0.8257828f, 0.37524414f);
                bodyCollider.size = new Vector3(1.546875f, 1.9324493f, 3.6594238f);
            }

            bodyCollider.isTrigger = false;
            bodyCollider.enabled = true;
        }

        Rigidbody templateBody = template != null ? template.GetComponent<Rigidbody>() : null;
        Rigidbody body = EnsureComponentOnRoot<Rigidbody>(root);
        if (body != null)
        {
            body.mass = templateBody != null ? templateBody.mass : 100f;
            body.linearDamping = templateBody != null ? templateBody.linearDamping : 0f;
            body.angularDamping = templateBody != null ? templateBody.angularDamping : 0.05f;
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        NavMeshAgent templateAgent = template.GetComponent<NavMeshAgent>();
        NavMeshAgent agent = EnsureComponentOnRoot<NavMeshAgent>(root);
        if (templateAgent != null && agent != null)
        {
            agent.radius = templateAgent.radius;
            agent.height = templateAgent.height;
            agent.speed = templateAgent.speed;
            agent.angularSpeed = templateAgent.angularSpeed;
            agent.stoppingDistance = templateAgent.stoppingDistance;
            agent.baseOffset = templateAgent.baseOffset;
        }
        else if (agent != null)
        {
            agent.radius = 0.27f;
            agent.height = 0.52f;
            agent.speed = 3.5f;
            agent.angularSpeed = 120f;
            agent.stoppingDistance = 0.1f;
        }

        CharacterHealth health = AddComponentIfMissing<CharacterHealth>(root);
        EnemySensor sensor = AddComponentIfMissing<EnemySensor>(root);
        EnemyAIController ai = AddComponentIfMissing<EnemyAIController>(root);
        CharacterKnockback knockback = AddComponentIfMissing<CharacterKnockback>(root);
        AddComponentIfMissing<LootDropSpawner>(root);
        DissolveOnDeath dissolve = AddComponentIfMissing<DissolveOnDeath>(root);
        EnemyAnimationEventRelay relay = AddComponentIfMissing<EnemyAnimationEventRelay>(root);

        Transform templateHitbox = template.transform.Find("AttackHitbox");
        if (templateHitbox != null && root.transform.Find("AttackHitbox") == null)
        {
            GameObject hitbox = Object.Instantiate(templateHitbox.gameObject, root.transform);
            hitbox.name = "AttackHitbox";
        }

        Transform eye = root.transform.Find("EyeSensor");
        if (eye == null)
        {
            var eyeObject = new GameObject("EyeSensor");
            eyeObject.transform.SetParent(root.transform, false);
            eyeObject.transform.localPosition = new Vector3(0f, 1.6f, 0.5f);
            eye = eyeObject.transform;
        }

        Animator animator = root.GetComponent<Animator>();
        Animator templateAnimator = template.GetComponent<Animator>();
        if (animator != null && templateAnimator != null)
        {
            animator.runtimeAnimatorController = templateAnimator.runtimeAnimatorController;
            animator.applyRootMotion = false;
        }

        EnemyAttackHitbox attackHitbox = root.GetComponentInChildren<EnemyAttackHitbox>(true);

        SerializedObject aiSo = new SerializedObject(ai);
        aiSo.FindProperty("enemyData").objectReferenceValue = enemyData;
        aiSo.FindProperty("sensor").objectReferenceValue = sensor;
        aiSo.FindProperty("health").objectReferenceValue = health;
        aiSo.FindProperty("knockback").objectReferenceValue = knockback;
        aiSo.FindProperty("animator").objectReferenceValue = animator;
        if (attackHitbox != null)
        {
            aiSo.FindProperty("attackHitbox").objectReferenceValue = attackHitbox;
        }

        aiSo.FindProperty("flipForward180").boolValue = true;
        aiSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject sensorSo = new SerializedObject(sensor);
        sensorSo.FindProperty("enemyData").objectReferenceValue = enemyData;
        sensorSo.FindProperty("eyeSensor").objectReferenceValue = eye;
        sensorSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject relaySo = new SerializedObject(relay);
        relaySo.FindProperty("aiOwner").objectReferenceValue = ai;
        relaySo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject dissolveSo = new SerializedObject(dissolve);
        dissolveSo.FindProperty("characterHealth").objectReferenceValue = health;
        dissolveSo.ApplyModifiedPropertiesWithoutUndo();

        EnemyTackleSetup.EnsureTacklePushHitboxPublic(root);
        EnemyTackleSetup.WireAnimationRelayPublic(root);
    }

    static void CreateEnemySpawnPoint(Transform parent, string name, Vector3 pos, EnemyData data, bool isBoss)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var point = go.AddComponent<EnemySpawnPoint>();
        SerializedObject so = new SerializedObject(point);
        so.FindProperty("enemyData").objectReferenceValue = data;
        so.FindProperty("isMiniBoss").boolValue = isBoss;
        so.FindProperty("patrolRadius").floatValue = 5f;
        so.FindProperty("autoPatrolCount").intValue = 4;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetupResourceNodes(Vector3 pivot)
    {
        if (GameObject.Find("ResourceNodes") != null) return;

        var root = new GameObject("ResourceNodes");
        Undo.RegisterCreatedObjectUndo(root, "ResourceNodes");

        CreateResourceNode(root.transform, "Resource_Crystal_01", pivot + new Vector3(6f, 0f, -10f), ResourceCrystalPath);
        CreateResourceNode(root.transform, "Resource_Crystal_02", pivot + new Vector3(-8f, 0f, -12f), ResourceCrystalPath);
        CreateResourceNode(root.transform, "Resource_CoreDust_01", pivot + new Vector3(2f, 0f, 14f), ResourceCorePath);
    }

    static void CreateResourceNode(Transform parent, string name, Vector3 pos, string dataPath)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        var col = go.AddComponent<CapsuleCollider>();
        col.height = 2f;
        col.radius = 0.8f;
        col.isTrigger = true;

        var node = go.AddComponent<ResourceNode>();
        SerializedObject so = new SerializedObject(node);
        so.FindProperty("nodeData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ResourceNodeData>(dataPath);
        so.FindProperty("activeVisual").objectReferenceValue = visual;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetupZoneSystems()
    {
        if (FindSceneComponent<ZoneObjectiveManager>() != null) return;

        var go = new GameObject("ZoneSystems");
        Undo.RegisterCreatedObjectUndo(go, "ZoneSystems");

        var objective = go.AddComponent<ZoneObjectiveManager>();
        SerializedObject so = new SerializedObject(objective);
        so.FindProperty("zoneId").stringValue = "zone_eden7_forest";
        so.FindProperty("requiredEnemyKills").intValue = 3;
        so.FindProperty("requiredResourceGathers").intValue = 2;
        so.FindProperty("requireMiniBossDefeat").boolValue = true;
        so.FindProperty("bonusGoldItem").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemData>(GoldPath);
        so.FindProperty("bonusGoldAmount").intValue = 50;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetupHudPanels()
    {
        EnsureBossHud();
        EnsureResultScreen();
    }

    static void EnsureBossHud()
    {
        if (FindSceneComponent<BossHUDController>() != null) return;

        GameObject menuCanvas = GameObject.Find("Menu_Canvas") ?? GameObject.Find("HUD_Canvas");
        if (menuCanvas == null)
        {
            Debug.LogWarning("[DemoSetup] Không tìm thấy Menu_Canvas/HUD_Canvas để tạo Boss HUD.");
            return;
        }

        var panel = new GameObject("BossHUDPanel");
        Undo.RegisterCreatedObjectUndo(panel, "BossHUDPanel");
        panel.transform.SetParent(menuCanvas.transform, false);

        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.2f, 0.88f);
        rect.anchorMax = new Vector2(0.8f, 0.96f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var nameGo = new GameObject("BossName");
        nameGo.transform.SetParent(panel.transform, false);
        var nameText = nameGo.AddComponent<TextMeshProUGUI>();
        nameText.text = "Boss";
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.fontSize = 24;

        var barBg = new GameObject("HealthBarBg");
        barBg.transform.SetParent(panel.transform, false);
        var bgRect = barBg.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(1f, 0.45f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = barBg.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        var fillGo = new GameObject("HealthFill");
        fillGo.transform.SetParent(barBg.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillGo.AddComponent<Image>();
        fillImage.color = new Color(0.85f, 0.2f, 0.2f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;

        var hpGo = new GameObject("HealthText");
        hpGo.transform.SetParent(panel.transform, false);
        var hpText = hpGo.AddComponent<TextMeshProUGUI>();
        hpText.text = "0 / 0";
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.fontSize = 16;

        var hud = panel.AddComponent<BossHUDController>();
        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("root").objectReferenceValue = panel;
        so.FindProperty("bossNameText").objectReferenceValue = nameText;
        so.FindProperty("healthFill").objectReferenceValue = fillImage;
        so.FindProperty("healthText").objectReferenceValue = hpText;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
    }

    static void EnsureResultScreen()
    {
        if (FindSceneComponent<ZoneResultScreenController>() != null) return;

        GameObject menuCanvas = GameObject.Find("Menu_Canvas") ?? GameObject.Find("HUD_Canvas");
        if (menuCanvas == null) return;

        var panel = new GameObject("ZoneResultPanel");
        Undo.RegisterCreatedObjectUndo(panel, "ZoneResultPanel");
        panel.transform.SetParent(menuCanvas.transform, false);
        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panel.transform, false);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "Zone Cleared!";
        title.fontSize = 42;
        title.alignment = TextAlignmentOptions.Center;
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.2f, 0.65f);
        titleRect.anchorMax = new Vector2(0.8f, 0.8f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        var summaryGo = new GameObject("Summary");
        summaryGo.transform.SetParent(panel.transform, false);
        var summary = summaryGo.AddComponent<TextMeshProUGUI>();
        summary.text = "Summary";
        summary.fontSize = 22;
        summary.alignment = TextAlignmentOptions.TopLeft;
        var summaryRect = summaryGo.GetComponent<RectTransform>();
        summaryRect.anchorMin = new Vector2(0.25f, 0.35f);
        summaryRect.anchorMax = new Vector2(0.75f, 0.6f);
        summaryRect.offsetMin = Vector2.zero;
        summaryRect.offsetMax = Vector2.zero;

        var returnBtn = CreateUIButton(panel.transform, "ReturnToCampButton", "Return to Camp", new Vector2(0.3f, 0.15f), new Vector2(0.45f, 0.25f));
        var continueBtn = CreateUIButton(panel.transform, "ContinueButton", "Continue", new Vector2(0.55f, 0.15f), new Vector2(0.7f, 0.25f));

        var result = panel.AddComponent<ZoneResultScreenController>();
        SerializedObject so = new SerializedObject(result);
        so.FindProperty("root").objectReferenceValue = panel;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("summaryText").objectReferenceValue = summary;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(returnBtn.GetComponent<Button>().onClick, result.OnClickReturnToCamp);
        UnityEventTools.AddPersistentListener(continueBtn.GetComponent<Button>().onClick, result.OnClickContinueExplore);

        panel.SetActive(false);
    }

    static GameObject CreateUIButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.45f, 0.8f, 1f);
        var button = go.AddComponent<Button>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return go;
    }

    static void SetupShopInHub()
    {
        if (FindSceneComponent<ShopController>() != null) return;

        var shopRoot = new GameObject("ShopSystem");
        Undo.RegisterCreatedObjectUndo(shopRoot, "ShopSystem");

        var controller = shopRoot.AddComponent<ShopController>();
        var ui = shopRoot.AddComponent<ShopUIController>();

        var uiPanel = new GameObject("ShopPanel");
        uiPanel.transform.SetParent(shopRoot.transform, false);
        var canvas = uiPanel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        uiPanel.AddComponent<CanvasScaler>();
        uiPanel.AddComponent<GraphicRaycaster>();

        var panelRect = uiPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.25f, 0.2f);
        panelRect.anchorMax = new Vector2(0.75f, 0.8f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelBg = uiPanel.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(uiPanel.transform, false);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "Beacon Supply";
        title.fontSize = 30;
        title.alignment = TextAlignmentOptions.Center;
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.82f);
        titleRect.anchorMax = new Vector2(0.95f, 0.95f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        var statusGo = new GameObject("Status");
        statusGo.transform.SetParent(uiPanel.transform, false);
        var status = statusGo.AddComponent<TextMeshProUGUI>();
        status.text = "Gold: 0";
        status.fontSize = 20;
        var statusRect = statusGo.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.05f, 0.72f);
        statusRect.anchorMax = new Vector2(0.95f, 0.8f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;

        var buttons = new ShopEntryButton[2];
        buttons[0] = CreateShopEntryButton(uiPanel.transform, "Entry0", new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.62f));
        buttons[1] = CreateShopEntryButton(uiPanel.transform, "Entry1", new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.48f));

        var closeBtn = CreateUIButton(uiPanel.transform, "CloseButton", "Close", new Vector2(0.35f, 0.08f), new Vector2(0.65f, 0.16f));
        UnityEventTools.AddPersistentListener(closeBtn.GetComponent<Button>().onClick, ui.OnClickClose);

        SerializedObject controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("shopData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ShopData>(ShopDataPath);
        controllerSo.FindProperty("shopUI").objectReferenceValue = ui;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject uiSo = new SerializedObject(ui);
        uiSo.FindProperty("root").objectReferenceValue = uiPanel;
        uiSo.FindProperty("titleText").objectReferenceValue = title;
        uiSo.FindProperty("statusText").objectReferenceValue = status;
        uiSo.FindProperty("entryButtons").arraySize = 2;
        uiSo.FindProperty("entryButtons").GetArrayElementAtIndex(0).objectReferenceValue = buttons[0];
        uiSo.FindProperty("entryButtons").GetArrayElementAtIndex(1).objectReferenceValue = buttons[1];
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        uiPanel.SetActive(false);

        GameObject shopCounter = GameObject.Find("Shop_Counter") ?? GameObject.Find("Shop");
        if (shopCounter == null)
        {
            shopCounter = new GameObject("Shop_InteractPoint");
            Undo.RegisterCreatedObjectUndo(shopCounter, "Shop Interact");
            shopCounter.transform.position = GetScenePivot() + new Vector3(3f, 0f, 2f);
        }

        var col = shopCounter.GetComponent<Collider>();
        if (col == null)
        {
            col = shopCounter.AddComponent<BoxCollider>();
        }
        col.isTrigger = true;
        if (col is BoxCollider box) box.size = new Vector3(3f, 2f, 3f);

        ShopInteractable interact = EnsureComponentOnRoot<ShopInteractable>(shopCounter);
        SerializedObject interactSo = new SerializedObject(interact);
        interactSo.FindProperty("shopController").objectReferenceValue = controller;
        interactSo.ApplyModifiedPropertiesWithoutUndo();
    }

    static ShopEntryButton CreateShopEntryButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = CreateUIButton(parent, name, "Item — 0 Gold", anchorMin, anchorMax);
        var entry = go.AddComponent<ShopEntryButton>();
        SerializedObject so = new SerializedObject(entry);
        so.FindProperty("labelText").objectReferenceValue = go.GetComponentInChildren<TextMeshProUGUI>();
        so.FindProperty("buyButton").objectReferenceValue = go.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();
        return entry;
    }

    static void CreateShopAsset()
    {
        var shop = AssetDatabase.LoadAssetAtPath<ShopData>(ShopDataPath);
        if (shop == null)
        {
            shop = ScriptableObject.CreateInstance<ShopData>();
            AssetDatabase.CreateAsset(shop, ShopDataPath);
        }

        shop.shopId = "shop_beacon_camp";
        shop.shopName = "Beacon Supply";
        shop.currencyItem = AssetDatabase.LoadAssetAtPath<ItemData>(GoldPath);
        shop.entries = new List<ShopEntry>
        {
            new ShopEntry
            {
                item = AssetDatabase.LoadAssetAtPath<ItemData>(PotionPath),
                price = 25,
                quantity = 1
            },
            new ShopEntry
            {
                item = AssetDatabase.LoadAssetAtPath<ItemData>(CoreDustPath),
                price = 40,
                quantity = 2
            }
        };
        EditorUtility.SetDirty(shop);
    }

    static void CreateResourceNodeAsset(string path, string id, string displayName, string itemPath, int min, int max, float duration, float respawn)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ResourceNodeData>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<ResourceNodeData>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.nodeId = id;
        asset.displayName = displayName;
        asset.outputItem = AssetDatabase.LoadAssetAtPath<ItemData>(itemPath);
        asset.minAmount = min;
        asset.maxAmount = max;
        asset.gatherDuration = duration;
        asset.respawnTime = respawn;
        EditorUtility.SetDirty(asset);
    }

    static void CreateCompanionPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(CompyPrefabPath);
        if (source == null)
        {
            Debug.LogWarning("[DemoSetup] Không tìm thấy compy prefab, bỏ qua companion prefab.");
            return;
        }

        GameObject root = PrefabUtility.InstantiatePrefab(source) as GameObject;
        try
        {
            DestroyComponent<EnemyPatrol>(root);
            DestroyComponent<EnemyAIController>(root);
            DestroyComponent<EnemySensor>(root);
            DestroyComponent<LootDropSpawner>(root);
            DestroyComponent<EnemyKillTracker>(root);
            DestroyComponent<MiniBossMarker>(root);

            NavMeshAgent agent = EnsureComponentOnRoot<NavMeshAgent>(root);
            if (agent != null)
            {
                agent.speed = 4.5f;
                agent.stoppingDistance = 2f;
            }
            root.AddComponent<CompanionController>();

            EnsureFolder(Path.GetDirectoryName(CompanionPrefabPath)?.Replace('\\', '/'));
            PrefabUtility.SaveAsPrefabAsset(root, CompanionPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void PopulateItemRegistryLists()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/_Project/ScriptableObjects" });
        var items = new List<ItemData>();
        foreach (string guid in guids)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) items.Add(item);
        }

        Debug.Log($"[DemoSetup] Found {items.Count} ItemData assets for registry.");
    }

    static void PopulateInstaller(ItemRegistryInstaller installer)
    {
        if (installer == null) return;
        string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/_Project/ScriptableObjects" });
        var items = new List<ItemData>();
        foreach (string guid in guids)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) items.Add(item);
        }

        SerializedObject so = new SerializedObject(installer);
        SerializedProperty list = so.FindProperty("allItems");
        list.ClearArray();
        for (int i = 0; i < items.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        GameDataManager gdm = installer.GetComponent<GameDataManager>();
        if (gdm != null)
        {
            SerializedObject gdmSo = new SerializedObject(gdm);
            SerializedProperty db = gdmSo.FindProperty("itemDatabase");
            db.ClearArray();
            for (int i = 0; i < items.Count; i++)
            {
                db.InsertArrayElementAtIndex(i);
                db.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            gdmSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void EnsureItemRegistryOnManagers()
    {
        GameObject managers = GameObject.Find("Managers") ?? GameObject.Find("GameDataManager");
        if (managers == null)
        {
            managers = new GameObject("Managers");
            Undo.RegisterCreatedObjectUndo(managers, "Managers");
            managers.AddComponent<GameDataManager>();
        }

        ItemRegistryInstaller installer = AddComponentIfMissing<ItemRegistryInstaller>(managers);
        PopulateInstaller(installer);
    }

    static EnemyData LoadEnemyData(string path) => AssetDatabase.LoadAssetAtPath<EnemyData>(path);

    static Vector3 GetScenePivot()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) return player.transform.position;
        if (SceneView.lastActiveSceneView?.camera != null)
            return SceneView.lastActiveSceneView.camera.transform.position;
        return Vector3.zero;
    }

    static T FindSceneComponent<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

    static GameObject FindChildByName(string parentName, string childName)
    {
        GameObject parent = GameObject.Find(parentName);
        if (parent == null)
        {
            return null;
        }

        foreach (Transform child in parent.transform)
        {
            if (child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(es, "EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, folderName);
    }

    static T AddComponentIfMissing<T>(GameObject go) where T : Component
    {
        return EnsureComponentOnRoot<T>(go);
    }

    static T EnsureComponentOnRoot<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null)
        {
            comp = Undo.AddComponent<T>(go);
        }

        return comp;
    }

    static void DestroyComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp != null) Object.DestroyImmediate(comp);
    }
}
#endif