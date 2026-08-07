#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Build full gameplay boss prefab from a_tyran_01 / Enemy_Boss_BeachTyran.
/// Menu: ASTRA EDEN → Enemies → Build Beach Boss Tyran (Full Gameplay)
/// </summary>
public static class EnemyBossBeachTyranBuilder
{
    const string SourceModelPath =
        "Assets/Prefabs/Enemy/dino-hunter-deadly-shores-vicious/source/a_tyran_01.fbx";
    const string BossPrefabPath = "Assets/_Project/Prefab/Enemy_Boss_BeachTyran.prefab";
    const string BossAnimatorPath = "Assets/Animations/Enemy_Boss_1_Animator.controller";
    const string BossMusicPath = "Assets/Sounds/25 Rpg Game Tracks/Action 2 (Loop).wav";
    const string EnemyTemplatePath = "Assets/_Project/Prefab/Enemy.prefab";
    const string EnemyDataPath =
        "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyBeachApexTyran.asset";
    const string LootTablePath =
        "Assets/_Project/ScriptableObjects/Enemies/LootTables/SO_LootTable_LootPackLeader.asset";
    const string AttackBitePath =
        "Assets/_Project/ScriptableObjects/Enemies/AttackPatterns/SO_AttackPattern_AtkRaptorBite.asset";
    const string AttackLeapPath =
        "Assets/_Project/ScriptableObjects/Enemies/AttackPatterns/SO_AttackPattern_AtkEliteCharge.asset";

    [MenuItem("ASTRA EDEN/Enemies/Build Beach Boss Tyran (Full Gameplay)")]
    public static void Build()
    {
        EnsureEnemyData();
        BuildBossPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "[BeachBossTyran] Prefab gameplay ready: " + BossPrefabPath +
            " | Data: " + EnemyDataPath);
    }

    /// <summary>Batchmode entry: -executeMethod EnemyBossBeachTyranBuilder.BuildBatch</summary>
    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    static void EnsureEnemyData()
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyDataPath);
        bool created = false;
        if (data == null)
        {
            string folder = Path.GetDirectoryName(EnemyDataPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            data = ScriptableObject.CreateInstance<EnemyData>();
            AssetDatabase.CreateAsset(data, EnemyDataPath);
            created = true;
        }

        SerializedObject so = new SerializedObject(data);
        so.FindProperty("enemyId").stringValue = "enemy_beach_apex_tyran";
        so.FindProperty("displayName").stringValue = "Beach Apex Tyran";
        so.FindProperty("archetype").enumValueIndex = (int)EnemyArchetype.Boss;
        so.FindProperty("rank").enumValueIndex = (int)EnemyRank.AlphaBoss;
        so.FindProperty("zone").enumValueIndex = (int)EnemyZone.BeachCrash;

        SerializedProperty stats = so.FindProperty("baseStats");
        stats.FindPropertyRelative("maxHP").floatValue = 1200f;
        stats.FindPropertyRelative("attack").floatValue = 32f;
        stats.FindPropertyRelative("defense").floatValue = 45f;
        stats.FindPropertyRelative("poise").floatValue = 100f;
        stats.FindPropertyRelative("moveSpeed").floatValue = 3.8f;
        stats.FindPropertyRelative("turnSpeed").floatValue = 400f;

        so.FindProperty("sightRange").floatValue = 22f;
        so.FindProperty("sightAngle").floatValue = 130f;
        so.FindProperty("hearingRange").floatValue = 12f;
        so.FindProperty("aggroKeepRange").floatValue = 36f;
        so.FindProperty("attackRange").floatValue = 4.5f;
        so.FindProperty("attackCooldown").floatValue = 2.2f;
        so.FindProperty("expReward").intValue = 120;
        so.FindProperty("goldMin").intValue = 40;
        so.FindProperty("goldMax").intValue = 70;
        so.FindProperty("description").stringValue =
            "Stage 1 Beach apex boss (a_tyran_01). Slow, heavy melee pressure.";

        SerializedProperty patterns = so.FindProperty("attackPatterns");
        patterns.ClearArray();
        AttackPatternData bite = AssetDatabase.LoadAssetAtPath<AttackPatternData>(AttackBitePath);
        AttackPatternData charge = AssetDatabase.LoadAssetAtPath<AttackPatternData>(AttackLeapPath);
        if (bite != null)
        {
            patterns.InsertArrayElementAtIndex(0);
            patterns.GetArrayElementAtIndex(0).objectReferenceValue = bite;
        }

        if (charge != null)
        {
            int i = patterns.arraySize;
            patterns.InsertArrayElementAtIndex(i);
            patterns.GetArrayElementAtIndex(i).objectReferenceValue = charge;
        }

        LootTableData loot = AssetDatabase.LoadAssetAtPath<LootTableData>(LootTablePath);
        if (loot != null)
        {
            so.FindProperty("mainLootTable").objectReferenceValue = loot;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        Debug.Log(created
            ? $"[BeachBossTyran] Created EnemyData at {EnemyDataPath}"
            : $"[BeachBossTyran] Updated EnemyData at {EnemyDataPath}");
    }

    static void BuildBossPrefab()
    {
        GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
        if (sourceAsset == null)
        {
            Debug.LogError($"[BeachBossTyran] Missing model: {SourceModelPath}");
            return;
        }

        // Prefer LoadPrefabContents so FBX hierarchy/materials stay intact.
        GameObject root = PrefabUtility.LoadPrefabContents(SourceModelPath);
        root.name = "Enemy_Boss_BeachTyran";
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        // Keep artist scale from previous prefab (model unit → playable size).
        root.transform.localScale = Vector3.one * 50f;

        try
        {
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyDataPath);
            GameObject template = PrefabUtility.LoadPrefabContents(EnemyTemplatePath);
            try
            {
                ApplyGameplay(root, template, data);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(template);
            }

            string folder = Path.GetDirectoryName(BossPrefabPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);

            // Wire EnemyData.enemyPrefab → saved root.
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (data != null && saved != null)
            {
                SerializedObject dataSo = new SerializedObject(data);
                dataSo.FindProperty("enemyPrefab").objectReferenceValue = saved;
                dataSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ApplyGameplay(GameObject root, GameObject template, EnemyData enemyData)
    {
        // --- Physics body (fit mesh, not raptor defaults) ---
        DestroyIfPresent<CapsuleCollider>(root);

        BoxCollider body = Ensure<BoxCollider>(root);
        FitBoxColliderToRenderers(root, body);
        body.isTrigger = false;
        body.enabled = true;

        Rigidbody rb = Ensure<Rigidbody>(root);
        rb.mass = 250f;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        // --- NavMesh (world units for large boss) ---
        NavMeshAgent agent = Ensure<NavMeshAgent>(root);
        agent.radius = 2.4f;
        agent.height = 5f;
        agent.speed = enemyData != null && enemyData.baseStats != null
            ? enemyData.baseStats.moveSpeed
            : 3.8f;
        agent.angularSpeed = enemyData != null && enemyData.baseStats != null
            ? enemyData.baseStats.turnSpeed
            : 400f;
        agent.acceleration = 10f;
        agent.stoppingDistance = 2.2f;
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.baseOffset = 0f;

        // --- Animator: keep T-Rex controller, clear wrong raptor avatar ---
        Animator animator = Ensure<Animator>(root);
        RuntimeAnimatorController bossController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BossAnimatorPath);
        if (bossController != null)
        {
            animator.runtimeAnimatorController = bossController;
        }

        Avatar modelAvatar = null;
        Object[] fbxAssets = AssetDatabase.LoadAllAssetsAtPath(SourceModelPath);
        for (int i = 0; i < fbxAssets.Length; i++)
        {
            if (fbxAssets[i] is Avatar av)
            {
                modelAvatar = av;
                break;
            }
        }

        animator.avatar = modelAvatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        // --- Core scripts ---
        CharacterHealth health = Ensure<CharacterHealth>(root);
        EnemySensor sensor = Ensure<EnemySensor>(root);
        EnemyAIController ai = Ensure<EnemyAIController>(root);
        CharacterKnockback knockback = Ensure<CharacterKnockback>(root);
        Ensure<LootDropSpawner>(root);
        DissolveOnDeath dissolve = Ensure<DissolveOnDeath>(root);
        EnemyAnimationEventRelay relay = Ensure<EnemyAnimationEventRelay>(root);
        MiniBossMarker marker = Ensure<MiniBossMarker>(root);

        // Health defaults from data
        if (enemyData != null && enemyData.baseStats != null)
        {
            health.ApplyEnemyStats(enemyData.baseStats);
        }

        // --- Attack hitbox (local space; root scale 50) ---
        Transform hitboxT = root.transform.Find("AttackHitbox");
        GameObject hitboxGo;
        if (hitboxT == null)
        {
            hitboxGo = new GameObject("AttackHitbox");
            hitboxGo.transform.SetParent(root.transform, false);
        }
        else
        {
            hitboxGo = hitboxT.gameObject;
        }

        // Mouth / head region roughly forward-up in model local space.
        hitboxGo.transform.localPosition = new Vector3(0f, 0.08f, 0.12f);
        hitboxGo.transform.localRotation = Quaternion.identity;
        hitboxGo.transform.localScale = Vector3.one;

        BoxCollider hitCol = Ensure<BoxCollider>(hitboxGo);
        hitCol.isTrigger = true;
        hitCol.enabled = false;
        hitCol.center = Vector3.zero;
        hitCol.size = new Vector3(0.08f, 0.08f, 0.12f);

        EnemyAttackHitbox attackHitbox = Ensure<EnemyAttackHitbox>(hitboxGo);

        // --- Eye sensor ---
        Transform eye = root.transform.Find("EyeSensor");
        if (eye == null)
        {
            var eyeGo = new GameObject("EyeSensor");
            eyeGo.transform.SetParent(root.transform, false);
            eye = eyeGo.transform;
        }

        eye.localPosition = new Vector3(0f, 0.12f, 0.1f);

        // --- Tackle hitbox (charge / shove window) ---
        EnemyTackleSetup.EnsureTacklePushHitboxPublic(root);
        Transform tackle = root.transform.Find("TacklePushHitbox");
        if (tackle != null)
        {
            tackle.localPosition = new Vector3(0f, 0.06f, 0.1f);
            BoxCollider tCol = tackle.GetComponent<BoxCollider>();
            if (tCol != null)
            {
                tCol.isTrigger = true;
                tCol.enabled = false;
                tCol.size = new Vector3(0.12f, 0.1f, 0.16f);
                tCol.center = new Vector3(0f, 0.02f, 0.04f);
            }

            EnemyPushHitbox push = tackle.GetComponent<EnemyPushHitbox>();
            if (push != null)
            {
                SerializedObject pushSo = new SerializedObject(push);
                pushSo.FindProperty("pushDistance").floatValue = 6f;
                pushSo.FindProperty("pushDuration").floatValue = 0.22f;
                pushSo.FindProperty("verticalLift").floatValue = 0.25f;
                pushSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // --- Wire AI ---
        SerializedObject aiSo = new SerializedObject(ai);
        aiSo.FindProperty("enemyData").objectReferenceValue = enemyData;
        aiSo.FindProperty("initializeFromEnemyData").boolValue = true;
        aiSo.FindProperty("sensor").objectReferenceValue = sensor;
        aiSo.FindProperty("health").objectReferenceValue = health;
        aiSo.FindProperty("knockback").objectReferenceValue = knockback;
        aiSo.FindProperty("animator").objectReferenceValue = animator;
        aiSo.FindProperty("attackHitbox").objectReferenceValue = attackHitbox;
        // T-Rex forward usually +Z; start false (toggle in Inspector if chase faces wrong).
        aiSo.FindProperty("flipForward180").boolValue = false;
        aiSo.FindProperty("useDeathAnimation").boolValue = true;
        aiSo.FindProperty("deathAnimationDuration").floatValue = 3f;
        aiSo.FindProperty("useHitAnimation").boolValue = true;
        aiSo.FindProperty("useTackle").boolValue = true;
        aiSo.FindProperty("attacksBeforeTackle").intValue = 2;
        aiSo.FindProperty("tackleRange").floatValue = 5.5f;
        aiSo.FindProperty("tackleCooldown").floatValue = 8f;
        aiSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject sensorSo = new SerializedObject(sensor);
        sensorSo.FindProperty("enemyData").objectReferenceValue = enemyData;
        sensorSo.FindProperty("eyeSensor").objectReferenceValue = eye;
        sensorSo.FindProperty("flipForward180").boolValue = false;
        // Multi-ray FOV (eoger-style). Mesh off by default — bật Generate Vision Mesh trên Inspector để debug.
        sensorSo.FindProperty("useMultiRayFov").boolValue = true;
        sensorSo.FindProperty("generateVisionMesh").boolValue = false;
        // Obstacle: only real blockers + Player target; avoid boss/VFX/UI/helper layers blocking passive detection.
        sensorSo.FindProperty("obstacleMask").intValue = LayerMask.GetMask("Default", "Ground", "Player");
        // Boss should still aggro by sound if tiny decorative colliders obscure the sight ray.
        sensorSo.FindProperty("hearingRequiresLineOfSight").boolValue = false;
        sensorSo.ApplyModifiedPropertiesWithoutUndo();

        // Ensure LoS component exists when multi-ray is on.
        EnemyLineOfSight los = root.GetComponent<EnemyLineOfSight>();
        if (los == null)
        {
            los = root.AddComponent<EnemyLineOfSight>();
        }

        SerializedObject losSo = new SerializedObject(los);
        losSo.FindProperty("maxRange").floatValue = enemyData != null ? enemyData.sightRange : 22f;
        losSo.FindProperty("fovAngle").floatValue = enemyData != null ? enemyData.sightAngle : 130f;
        losSo.FindProperty("eye").objectReferenceValue = eye;
        losSo.FindProperty("eyeHeight").floatValue = 0.12f;
        losSo.FindProperty("subdivisions").intValue = 16;
        losSo.FindProperty("maxIterations").intValue = 2;
        losSo.FindProperty("generateMesh").boolValue = false;
        losSo.FindProperty("flipForward180").boolValue = false;
        losSo.ApplyModifiedPropertiesWithoutUndo();

        sensorSo = new SerializedObject(sensor);
        sensorSo.FindProperty("lineOfSight").objectReferenceValue = los;
        sensorSo.ApplyModifiedPropertiesWithoutUndo();

        // World-space HP bar (giống Enemy.prefab). Boss root scale lớn → localY/scale nhỏ hơn.
        EnemyHUDBuilder.EnsureHudOnRoot(root, canvasLocalY: 0.22f, canvasScale: 0.004f, showDistance: 30f);

        SerializedObject relaySo = new SerializedObject(relay);
        relaySo.FindProperty("aiOwner").objectReferenceValue = ai;
        relaySo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject dissolveSo = new SerializedObject(dissolve);
        dissolveSo.FindProperty("characterHealth").objectReferenceValue = health;
        dissolveSo.FindProperty("startDelay").floatValue = 2.5f;
        dissolveSo.FindProperty("dissolveDuration").floatValue = 2f;
        dissolveSo.ApplyModifiedPropertiesWithoutUndo();

        EnemyTackleSetup.WireAnimationRelayPublic(root);

        // Boss HUD marker (also added at spawn if isMiniBoss; keep on prefab for drag-drop).
        string displayName = enemyData != null && !string.IsNullOrEmpty(enemyData.displayName)
            ? enemyData.displayName
            : "Beach Apex Tyran";
        marker.Configure(displayName, health);
        marker.ConfigureLockedArena(true);
        marker.ConfigureBossMusic(AssetDatabase.LoadAssetAtPath<AudioClip>(BossMusicPath));

        // Kill tracker: true = do not count as normal trash kill (boss has own objective).
        EnemyKillTracker tracker = Ensure<EnemyKillTracker>(root);
        tracker.Configure(true);
    }

    static void FitBoxColliderToRenderers(GameObject root, BoxCollider box)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            // Fallback local bounds for scaled T-Rex (scale ~50).
            box.center = new Vector3(0f, 0.06f, 0.02f);
            box.size = new Vector3(0.1f, 0.12f, 0.22f);
            return;
        }

        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                world.Encapsulate(renderers[i].bounds);
            }
        }

        Transform t = root.transform;
        Vector3 localCenter = t.InverseTransformPoint(world.center);
        Vector3 lossy = t.lossyScale;
        box.center = localCenter;
        box.size = new Vector3(
            Mathf.Max(world.size.x / Mathf.Max(Mathf.Abs(lossy.x), 0.0001f), 0.01f),
            Mathf.Max(world.size.y / Mathf.Max(Mathf.Abs(lossy.y), 0.0001f), 0.01f),
            Mathf.Max(world.size.z / Mathf.Max(Mathf.Abs(lossy.z), 0.0001f), 0.01f));
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c == null)
        {
            c = go.AddComponent<T>();
        }

        return c;
    }

    static void DestroyIfPresent<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c != null)
        {
            Object.DestroyImmediate(c);
        }
    }
}
#endif
