#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds the Ancient Forest Tyranno from its existing artist prefab.
/// The builder is intentionally idempotent: generated assets are updated and
/// the model hierarchy/material/avatar inside the source prefab are preserved.
/// </summary>
public static class EnemyBossAncientForestBuilder
{
    const string SourceModelPath = "Assets/Prefabs/Enemy/tyranno/source/RaptorALL.fbx";
    const string BossPrefabPath = "Assets/_Project/Prefab/Enemy_Boss_AncientForest.prefab";
    const string EnemyDataPath =
        "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyAncientForestTyranno.asset";
    const string PatternFolder =
        "Assets/_Project/ScriptableObjects/Enemies/AttackPatterns/AncientForest";
    const string ClipFolder = "Assets/Animations/Enemy_Boss_AncientForest";
    const string AnimatorPath = "Assets/Animations/Enemy_Boss_AncientForest_Animator.controller";
    const string SourceProjectilePath = "Assets/_Project/Prefab/PoisonOrb.prefab";
    const string ProjectilePath =
        "Assets/_Project/Prefab/EnemyProjectile_AncientForestPoisonOrb.prefab";
    const string LootTablePath =
        "Assets/_Project/ScriptableObjects/Enemies/LootTables/SO_LootTable_LootPackLeader.asset";
    const string BossMusicPath = "Assets/Sounds/25 Rpg Game Tracks/Action 2 (Loop).wav";

    sealed class ClipSet
    {
        public AnimationClip Idle;
        public AnimationClip Walk;
        public AnimationClip Run;
        public AnimationClip Bite;
        public AnimationClip HeavyBite;
        public AnimationClip Headbutt;
        public AnimationClip TailWhip;
        public AnimationClip Roar;
        public AnimationClip Hit;
        public AnimationClip Death;
    }

    [MenuItem("ASTRA EDEN/Enemies/Build Ancient Forest Boss")]
    public static void Build()
    {
        if (!ValidateInputs()) return;

        EnsureAssetFolder(EnemyDataPath);
        EnsureFolder(PatternFolder);
        EnsureFolder(ClipFolder);
        EnsureAssetFolder(AnimatorPath);
        EnsureAssetFolder(ProjectilePath);

        ClipSet clips = BuildAnimationClips();
        if (clips == null) return;

        AnimatorController controller = BuildAnimatorController(clips);
        GameObject projectile = BuildProjectileVariant();
        List<AttackPatternData> patterns = BuildAttackPatterns(clips);
        EnemyData data = BuildEnemyData(patterns);

        if (controller == null || projectile == null || data == null || patterns.Count != 5)
        {
            Debug.LogError("[AncientForestBoss] Build stopped because a generated dependency is missing.");
            return;
        }

        GameObject savedPrefab = BuildBossPrefab(data, controller, projectile);
        if (savedPrefab == null) return;

        SerializedObject dataSo = new SerializedObject(data);
        dataSo.FindProperty("enemyPrefab").objectReferenceValue = savedPrefab;
        dataSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!ValidateGeneratedAssets(data, controller)) return;
        Debug.Log(
            "[AncientForestBoss] Build complete. " +
            $"Prefab={BossPrefabPath} | Data={EnemyDataPath} | Animator={AnimatorPath} | Projectile={ProjectilePath}");
    }

    static bool ValidateGeneratedAssets(EnemyData data, AnimatorController controller)
    {
        bool valid = true;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[AncientForestBoss] Validation: boss prefab is missing.");
            return false;
        }

        valid &= ValidateCount<EnemyAIController>(prefab, 1);
        valid &= ValidateCount<EnemyProjectileShooter>(prefab, 1);
        valid &= ValidateCount<EnemyAttackHitbox>(prefab, 1);
        valid &= ValidateCount<CharacterHealth>(prefab, 1);
        valid &= ValidateCount<MiniBossMarker>(prefab, 1);
        valid &= prefab.transform.Find("ProjectileSpawnPoint") != null;

        Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
        valid &= animators.Length == 1;
        if (animators.Length != 1)
        {
            Debug.LogError($"[AncientForestBoss] Validation: expected exactly 1 Animator, found {animators.Length}.");
        }
        else
        {
            Transform modelRoot = FindModelAnimationRoot(prefab.transform);
            valid &= modelRoot != null && animators[0].transform == modelRoot;
            valid &= animators[0].runtimeAnimatorController == controller;
            valid &= animators[0].GetComponent<EnemyAnimationEventRelay>() != null;

            if (modelRoot == null || animators[0].transform != modelRoot)
            {
                Debug.LogError(
                    "[AncientForestBoss] Validation: Animator is not on the common parent of Hips and U3DMesh.");
            }
        }

        valid &= data != null && data.attackPatterns != null && data.attackPatterns.Count == 5;
        valid &= data != null && data.enemyPrefab == prefab;

        string[] requiredTriggers = { "Attack", "Attack2", "HeadButt", "TailWhip", "Roar", "Hit", "Die" };
        HashSet<string> parameters = controller != null
            ? new HashSet<string>(controller.parameters.Select(parameter => parameter.name))
            : new HashSet<string>();
        valid &= requiredTriggers.All(parameters.Contains);

        foreach (string clipPath in AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipFolder })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            string file = Path.GetFileNameWithoutExtension(clipPath);
            if (file.IndexOf("Bite", StringComparison.OrdinalIgnoreCase) < 0 &&
                file.IndexOf("Headbutt", StringComparison.OrdinalIgnoreCase) < 0 &&
                file.IndexOf("TailWhip", StringComparison.OrdinalIgnoreCase) < 0 &&
                file.IndexOf("PoisonRoar", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            HashSet<string> events = new HashSet<string>(
                AnimationUtility.GetAnimationEvents(clip).Select(animationEvent => animationEvent.functionName));
            valid &= events.Contains("OnAttackStart") &&
                     events.Contains("OnAttackHit") &&
                     events.Contains("OnAttackEnd");
        }

        if (!valid)
        {
            Debug.LogError("[AncientForestBoss] Validation failed. Check generated prefab references/components.");
            return false;
        }

        Debug.Log("[AncientForestBoss] Validation passed: components, 5 patterns, triggers, events and prefab references are valid.");
        return true;
    }

    static bool ValidateCount<T>(GameObject root, int expected) where T : Component
    {
        int count = root.GetComponentsInChildren<T>(true).Length;
        if (count == expected) return true;
        Debug.LogError($"[AncientForestBoss] Validation: expected {expected} {typeof(T).Name}, found {count}.");
        return false;
    }

    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    static bool ValidateInputs()
    {
        bool valid = true;
        valid &= RequireAsset<GameObject>(BossPrefabPath, "existing Ancient Forest boss prefab");
        valid &= RequireAsset<GameObject>(SourceModelPath, "RaptorALL source model");
        valid &= RequireAsset<GameObject>(SourceProjectilePath, "Poison Orb projectile");
        return valid;
    }

    static bool RequireAsset<T>(string path, string label) where T : UnityEngine.Object
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return true;
        Debug.LogError($"[AncientForestBoss] Missing {label}: {path}");
        return false;
    }

    static ClipSet BuildAnimationClips()
    {
        Dictionary<string, AnimationClip> embedded = AssetDatabase
            .LoadAllAssetsAtPath(SourceModelPath)
            .OfType<AnimationClip>()
            .Where(clip => clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            .GroupBy(clip => clip.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        string[] required = { "Idle", "Walk", "Run", "Attack", "Attack2", "HeadButt", "TailWhip", "Roar", "Fall" };
        foreach (string name in required)
        {
            if (!embedded.ContainsKey(name))
            {
                Debug.LogError(
                    $"[AncientForestBoss] Animation take '{name}' is missing from {SourceModelPath}. " +
                    $"Available: {string.Join(", ", embedded.Keys)}");
                return null;
            }
        }

        AnimationClip idle2 = embedded.TryGetValue("Idle2", out AnimationClip alternateIdle)
            ? alternateIdle
            : embedded["Idle"];

        return new ClipSet
        {
            Idle = CopyClip(embedded["Idle"], "AncientForest_Idle", true),
            Walk = CopyClip(embedded["Walk"], "AncientForest_Walk", true),
            Run = CopyClip(embedded["Run"], "AncientForest_Run", true),
            Bite = CopyAttackClip(embedded["Attack"], "AncientForest_Bite", 0.08f, 0.43f, 0.9f),
            HeavyBite = CopyAttackClip(embedded["Attack2"], "AncientForest_HeavyBite", 0.08f, 0.5f, 0.92f),
            Headbutt = CopyAttackClip(embedded["HeadButt"], "AncientForest_Headbutt", 0.08f, 0.48f, 0.92f),
            TailWhip = CopyAttackClip(embedded["TailWhip"], "AncientForest_TailWhip", 0.08f, 0.54f, 0.93f),
            Roar = CopyAttackClip(embedded["Roar"], "AncientForest_PoisonRoar", 0.08f, 0.58f, 0.92f),
            Hit = CopyClip(idle2, "AncientForest_Hit", false),
            Death = CopyClip(embedded["Fall"], "AncientForest_Death", false),
        };
    }

    static AnimationClip CopyAttackClip(
        AnimationClip source,
        string assetName,
        float startNormalized,
        float hitNormalized,
        float endNormalized)
    {
        AnimationClip clip = CopyClip(source, assetName, false);
        if (clip == null) return null;

        float length = Mathf.Max(0.1f, clip.length);
        AnimationUtility.SetAnimationEvents(
            clip,
            new[]
            {
                new AnimationEvent
                {
                    time = Mathf.Clamp(startNormalized * length, 0f, length - 0.03f),
                    functionName = "OnAttackStart",
                },
                new AnimationEvent
                {
                    time = Mathf.Clamp(hitNormalized * length, 0.02f, length - 0.02f),
                    functionName = "OnAttackHit",
                },
                new AnimationEvent
                {
                    time = Mathf.Clamp(endNormalized * length, 0.04f, length - 0.01f),
                    functionName = "OnAttackEnd",
                },
            });
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static AnimationClip CopyClip(AnimationClip source, string assetName, bool loop)
    {
        string path = $"{ClipFolder}/{assetName}.anim";
        AnimationClip copy = UnityEngine.Object.Instantiate(source);
        copy.name = assetName;

        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(copy, path);
            existing = copy;
        }
        else
        {
            EditorUtility.CopySerialized(copy, existing);
            UnityEngine.Object.DestroyImmediate(copy);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(existing);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionXZ = true;
        settings.keepOriginalPositionY = true;
        AnimationUtility.SetAnimationClipSettings(existing, settings);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static AnimatorController BuildAnimatorController(ClipSet clips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
        }
        else
        {
            // Preserve the controller GUID so external references remain valid,
            // but rebuild generated sub-assets/states without accumulating copies.
            controller.layers = Array.Empty<AnimatorControllerLayer>();
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(AnimatorPath);
            foreach (UnityEngine.Object subAsset in subAssets)
            {
                if (subAsset != null && subAsset != controller)
                {
                    UnityEngine.Object.DestroyImmediate(subAsset, true);
                }
            }

            controller.AddLayer("Base Layer");
        }

        controller.AddParameter("Blend", AnimatorControllerParameterType.Float);
        controller.AddParameter("Horizontal", AnimatorControllerParameterType.Float);
        controller.AddParameter("Vertical", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("HeadButt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("TailWhip", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Roar", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Stagger", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Tackle", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        sm.name = "Ancient Forest Boss";

        BlendTree locomotionTree = new BlendTree
        {
            name = "AncientForest_Locomotion",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Blend",
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(locomotionTree, controller);
        locomotionTree.AddChild(clips.Idle, 0f);
        locomotionTree.AddChild(clips.Walk, 0.45f);
        locomotionTree.AddChild(clips.Run, 1f);

        AnimatorState locomotion = sm.AddState("Locomotion", new Vector3(260f, 40f));
        locomotion.motion = locomotionTree;
        sm.defaultState = locomotion;

        AddAttackState(sm, locomotion, "Bite Attack", clips.Bite, "Attack", new Vector3(560f, -160f));
        AddAttackState(sm, locomotion, "Heavy Bite Attack2", clips.HeavyBite, "Attack2", new Vector3(560f, -80f));
        AddAttackState(sm, locomotion, "Headbutt", clips.Headbutt, "HeadButt", new Vector3(560f, 0f));
        AddAttackState(sm, locomotion, "TailWhip", clips.TailWhip, "TailWhip", new Vector3(560f, 80f));
        AddAttackState(sm, locomotion, "Poison Roar", clips.Roar, "Roar", new Vector3(560f, 160f));

        AnimatorState hit = sm.AddState("Hit", new Vector3(260f, 200f));
        hit.motion = clips.Hit;
        AddAnyTrigger(sm, hit, "Hit", 0.04f);
        AddAnyTrigger(sm, hit, "Stagger", 0.04f);
        AnimatorStateTransition hitBack = hit.AddTransition(locomotion);
        hitBack.hasExitTime = true;
        hitBack.exitTime = 0.8f;
        hitBack.duration = 0.08f;

        AnimatorState death = sm.AddState("Death", new Vector3(260f, 300f));
        death.motion = clips.Death;
        death.speed = 1f;
        AddAnyTrigger(sm, death, "Die", 0.05f);
        AnimatorStateTransition deadBool = sm.AddAnyStateTransition(death);
        deadBool.hasExitTime = false;
        deadBool.duration = 0.05f;
        deadBool.canTransitionToSelf = false;
        deadBool.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static void AddAttackState(
        AnimatorStateMachine sm,
        AnimatorState locomotion,
        string stateName,
        AnimationClip clip,
        string trigger,
        Vector3 position)
    {
        AnimatorState state = sm.AddState(stateName, position);
        state.motion = clip;
        AddAnyTrigger(sm, state, trigger, 0.06f);

        AnimatorStateTransition back = state.AddTransition(locomotion);
        back.hasExitTime = true;
        back.exitTime = 0.92f;
        back.duration = 0.08f;
    }

    static void AddAnyTrigger(
        AnimatorStateMachine sm,
        AnimatorState destination,
        string trigger,
        float duration)
    {
        AnimatorStateTransition transition = sm.AddAnyStateTransition(destination);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    static GameObject BuildProjectileVariant()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourceProjectilePath, ProjectilePath))
            {
                Debug.LogError($"[AncientForestBoss] Could not copy projectile to {ProjectilePath}");
                return null;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(ProjectilePath);
        try
        {
            root.name = "EnemyProjectile_AncientForestPoisonOrb";
            Ensure<PoisonOrbProjectile>(root);
            PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
    }

    static List<AttackPatternData> BuildAttackPatterns(ClipSet clips)
    {
        var patterns = new List<AttackPatternData>(5)
        {
            UpsertPattern(
                "Bite", "atk_ancient_forest_bite", "Bite Attack", "Attack",
                EnemyAttackRangeType.Melee, 0f, 3.8f, 1.9f,
                clips.Bite, 0.43f, 0.2f, 1.05f, 28f,
                EnemyAttackHitbox.HitShape.Box, new Vector3(1.35f, 1.15f, 1.8f),
                new Vector3(0f, 2.05f, 2.5f), 0f, 0f),
            UpsertPattern(
                "HeavyBite", "atk_ancient_forest_heavy_bite", "Heavy Bite Attack2", "Attack2",
                EnemyAttackRangeType.Melee, 0f, 4.2f, 2.15f,
                clips.HeavyBite, 0.5f, 0.24f, 1.35f, 42f,
                EnemyAttackHitbox.HitShape.Box, new Vector3(1.55f, 1.3f, 2.05f),
                new Vector3(0f, 2.05f, 2.65f), 2f, 0.12f),
            UpsertPattern(
                "Headbutt", "atk_ancient_forest_headbutt", "Headbutt", "HeadButt",
                EnemyAttackRangeType.MeleeAOE, 0.5f, 5f, 2f,
                clips.Headbutt, 0.48f, 0.24f, 1.2f, 48f,
                EnemyAttackHitbox.HitShape.Box, new Vector3(1.8f, 1.35f, 2.2f),
                new Vector3(0f, 1.8f, 2.25f), 4.5f, 0.2f),
            UpsertPattern(
                "TailWhip", "atk_ancient_forest_tail_whip", "TailWhip", "TailWhip",
                EnemyAttackRangeType.MeleeAOE, 0f, 5.5f, 2.2f,
                clips.TailWhip, 0.54f, 0.28f, 1.18f, 55f,
                EnemyAttackHitbox.HitShape.Sphere, Vector3.one * 1.2f,
                Vector3.zero, 6f, 0.28f),
            UpsertPattern(
                "PoisonRoar", "atk_ancient_forest_poison_roar", "Poison Roar", "Roar",
                EnemyAttackRangeType.ProjectileAOE, 6f, 10f, 2.05f,
                clips.Roar, 0.58f, 0.16f, 1.2f, 34f,
                EnemyAttackHitbox.HitShape.Sphere, Vector3.one,
                Vector3.zero, 3f, 0.2f),
        };
        return patterns;
    }

    static AttackPatternData UpsertPattern(
        string fileSuffix,
        string id,
        string displayName,
        string trigger,
        EnemyAttackRangeType rangeType,
        float minRange,
        float maxRange,
        float cooldown,
        AnimationClip timingClip,
        float hitNormalized,
        float activeTime,
        float damageMultiplier,
        float poiseDamage,
        EnemyAttackHitbox.HitShape hitShape,
        Vector3 halfExtents,
        Vector3 localOffset,
        float knockbackDistance,
        float knockbackDuration)
    {
        string path = $"{PatternFolder}/SO_AttackPattern_AncientForest{fileSuffix}.asset";
        AttackPatternData pattern = AssetDatabase.LoadAssetAtPath<AttackPatternData>(path);
        if (pattern == null)
        {
            pattern = ScriptableObject.CreateInstance<AttackPatternData>();
            AssetDatabase.CreateAsset(pattern, path);
        }

        float clipLength = timingClip != null ? Mathf.Max(0.1f, timingClip.length) : 1f;
        float windup = Mathf.Max(0.08f, clipLength * hitNormalized);
        float recovery = Mathf.Max(0.22f, clipLength * 0.92f - windup - activeTime);

        SerializedObject so = new SerializedObject(pattern);
        so.FindProperty("attackId").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("archetype").enumValueIndex = (int)EnemyArchetype.Boss;
        so.FindProperty("rangeType").enumValueIndex = (int)rangeType;
        so.FindProperty("minRange").floatValue = minRange;
        so.FindProperty("maxRange").floatValue = maxRange;
        so.FindProperty("cooldown").floatValue = Mathf.Clamp(cooldown, 1.8f, 2.2f);
        so.FindProperty("windup").floatValue = windup;
        so.FindProperty("activeTime").floatValue = activeTime;
        so.FindProperty("recovery").floatValue = recovery;
        so.FindProperty("animationTrigger").stringValue = trigger;
        so.FindProperty("damageMultiplier").floatValue = damageMultiplier;
        so.FindProperty("poiseDamage").floatValue = poiseDamage;
        so.FindProperty("element").enumValueIndex = (int)(rangeType == EnemyAttackRangeType.ProjectileAOE
            ? DamageElement.Poison
            : DamageElement.Physical);
        so.FindProperty("canBeInterrupted").boolValue = rangeType != EnemyAttackRangeType.MeleeAOE;
        so.FindProperty("overrideHitbox").boolValue = rangeType != EnemyAttackRangeType.ProjectileAOE;
        so.FindProperty("hitboxShape").enumValueIndex = (int)hitShape;
        so.FindProperty("hitboxRadius").floatValue = 1.2f;
        so.FindProperty("hitboxHalfExtents").vector3Value = halfExtents;
        so.FindProperty("hitboxLocalOffset").vector3Value = localOffset;
        so.FindProperty("knockbackDistance").floatValue = knockbackDistance;
        so.FindProperty("knockbackDuration").floatValue = Mathf.Max(0.01f, knockbackDuration);
        so.FindProperty("knockbackVerticalLift").floatValue = knockbackDistance > 0f ? 0.22f : 0f;
        so.FindProperty("telegraph").stringValue =
            rangeType == EnemyAttackRangeType.ProjectileAOE
                ? "The Tyranno raises its head and gathers ancient poison before roaring."
                : $"Ancient Forest Tyranno prepares {displayName}.";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pattern);
        return pattern;
    }

    static EnemyData BuildEnemyData(IReadOnlyList<AttackPatternData> patterns)
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            AssetDatabase.CreateAsset(data, EnemyDataPath);
        }

        SerializedObject so = new SerializedObject(data);
        so.FindProperty("enemyId").stringValue = "enemy_ancient_forest_tyranno";
        so.FindProperty("displayName").stringValue = "Ancient Forest Tyranno";
        so.FindProperty("archetype").enumValueIndex = (int)EnemyArchetype.Boss;
        so.FindProperty("rank").enumValueIndex = (int)EnemyRank.AlphaBoss;
        so.FindProperty("zone").enumValueIndex = (int)EnemyZone.PrimevalForest;

        SerializedProperty stats = so.FindProperty("baseStats");
        stats.FindPropertyRelative("maxHP").floatValue = 2000f;
        stats.FindPropertyRelative("attack").floatValue = 42f;
        stats.FindPropertyRelative("defense").floatValue = 55f;
        stats.FindPropertyRelative("poise").floatValue = 160f;
        stats.FindPropertyRelative("moveSpeed").floatValue = 4.2f;
        stats.FindPropertyRelative("turnSpeed").floatValue = 420f;

        so.FindProperty("sightRange").floatValue = 24f;
        so.FindProperty("sightAngle").floatValue = 140f;
        so.FindProperty("hearingRange").floatValue = 14f;
        so.FindProperty("aggroKeepRange").floatValue = 40f;
        so.FindProperty("attackRange").floatValue = 5.5f;
        so.FindProperty("attackCooldown").floatValue = 2f;
        so.FindProperty("expReward").intValue = 260;
        so.FindProperty("goldMin").intValue = 90;
        so.FindProperty("goldMax").intValue = 140;
        so.FindProperty("description").stringValue =
            "Ancient Forest alpha tyranno. Mixes heavy close-range pressure, a sweeping tail and a toxic ranged roar.";

        SerializedProperty array = so.FindProperty("attackPatterns");
        array.ClearArray();
        for (int i = 0; i < patterns.Count; i++)
        {
            array.InsertArrayElementAtIndex(i);
            array.GetArrayElementAtIndex(i).objectReferenceValue = patterns[i];
        }

        LootTableData loot = AssetDatabase.LoadAssetAtPath<LootTableData>(LootTablePath);
        if (loot != null) so.FindProperty("mainLootTable").objectReferenceValue = loot;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        return data;
    }

    static GameObject BuildBossPrefab(
        EnemyData data,
        RuntimeAnimatorController controller,
        GameObject projectilePrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        try
        {
            root.name = "Enemy_Boss_AncientForest";
            Bounds localBounds = CalculateLocalRendererBounds(root);
            Bounds worldBounds = CalculateWorldRendererBounds(root);

            // The copied clips are bound to paths beginning at Hips/U3DMesh.
            // Therefore the Animator must live on their common parent (the visual/model root),
            // not on the outer AI root. An Animator on the wrong level can receive triggers
            // and still deal timed damage while the dinosaur appears frozen.
            Transform modelRoot = FindModelAnimationRoot(root.transform);
            if (modelRoot == null)
            {
                Debug.LogError(
                    "[AncientForestBoss] Could not find the model root containing both Hips and U3DMesh. " +
                    "The prefab hierarchy must keep those model nodes under one common parent.");
                return null;
            }

            RemoveAnimatorsOutsideModelRoot(root, modelRoot);
            Animator animator = Ensure<Animator>(modelRoot.gameObject);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            // Boss attack events must continue even when the renderer is briefly outside camera view.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Avatar sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(SourceModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            if (sourceAvatar != null)
            {
                animator.avatar = sourceAvatar;
            }
            else
            {
                Debug.LogWarning(
                    $"[AncientForestBoss] No Avatar was found in {SourceModelPath}. " +
                    "Animations may not bind correctly.");
            }

            BoxCollider body = Ensure<BoxCollider>(root);
            body.isTrigger = false;
            body.center = localBounds.center;
            body.size = localBounds.size;

            Rigidbody rb = Ensure<Rigidbody>(root);
            rb.mass = 340f;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            NavMeshAgent agent = Ensure<NavMeshAgent>(root);
            agent.radius = Mathf.Clamp(Mathf.Min(worldBounds.extents.x, worldBounds.extents.z) * 0.55f, 1.25f, 3f);
            agent.height = Mathf.Clamp(worldBounds.size.y * 0.88f, 3.5f, 7f);
            agent.speed = 4.2f;
            agent.angularSpeed = 420f;
            agent.acceleration = 11f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = true;
            agent.autoRepath = true;

            CharacterHealth health = Ensure<CharacterHealth>(root);
            health.ApplyEnemyStats(data.baseStats);
            CharacterKnockback knockback = Ensure<CharacterKnockback>(root);
            EnemySensor sensor = Ensure<EnemySensor>(root);
            EnemyAIController ai = Ensure<EnemyAIController>(root);
            AncientForestBossBehaviour bossBehaviour = Ensure<AncientForestBossBehaviour>(root);

            SerializedObject behaviourSo = new SerializedObject(bossBehaviour);
            SetObjectReferenceIfPresent(behaviourSo, "visualRoot", modelRoot);
            SetFloatIfPresent(behaviourSo, "visualYawOffset", 90f);
            SetFloatIfPresent(behaviourSo, "fallbackMeleeRange", 5.5f);
            behaviourSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bossBehaviour);

            LootDropSpawner loot = Ensure<LootDropSpawner>(root);
            loot.ConfigureFromEnemyData(data);
            DissolveOnDeath dissolve = Ensure<DissolveOnDeath>(root);
            MiniBossMarker marker = Ensure<MiniBossMarker>(root);
            EnemyKillTracker killTracker = Ensure<EnemyKillTracker>(root);
            killTracker.Configure(true);

            Transform head = FindTransformContaining(root.transform, "head");
            Vector3 mouthLocal = head != null
                ? root.transform.InverseTransformPoint(head.position) + Vector3.forward * 0.55f
                : new Vector3(localBounds.center.x, localBounds.max.y * 0.78f, localBounds.max.z * 0.82f);

            Transform hitboxTransform = EnsureChild(root.transform, "AttackHitbox");
            hitboxTransform.localPosition = Vector3.zero;
            hitboxTransform.localRotation = Quaternion.identity;
            hitboxTransform.localScale = Vector3.one;
            EnemyAttackHitbox attackHitbox = Ensure<EnemyAttackHitbox>(hitboxTransform.gameObject);
            SerializedObject hitboxSo = new SerializedObject(attackHitbox);
            hitboxSo.FindProperty("shape").enumValueIndex = (int)EnemyAttackHitbox.HitShape.Box;
            hitboxSo.FindProperty("boxHalfExtents").vector3Value = new Vector3(1.35f, 1.15f, 1.8f);
            hitboxSo.FindProperty("localOffset").vector3Value = mouthLocal;
            hitboxSo.FindProperty("targetLayer").intValue = LayerMask.GetMask("Player");
            hitboxSo.FindProperty("minimumHitInterval").floatValue = 1f;
            hitboxSo.ApplyModifiedPropertiesWithoutUndo();

            Transform eye = EnsureChild(root.transform, "EyeSensor");
            eye.localPosition = mouthLocal + Vector3.up * 0.35f;
            eye.localRotation = Quaternion.identity;

            Transform spawn = EnsureChild(root.transform, "ProjectileSpawnPoint");
            spawn.localPosition = mouthLocal;
            spawn.localRotation = Quaternion.identity;

            EnemyProjectileShooter shooter = Ensure<EnemyProjectileShooter>(root);
            SerializedObject shooterSo = new SerializedObject(shooter);
            shooterSo.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            shooterSo.FindProperty("spawnPoint").objectReferenceValue = spawn;
            shooterSo.FindProperty("spawnForwardOffset").floatValue = 0.35f;
            shooterSo.FindProperty("useNegativeForward").boolValue = false;
            shooterSo.FindProperty("projectileSpeed").floatValue = 12.5f;
            shooterSo.FindProperty("maxTravelDistance").floatValue = 20f;
            shooterSo.FindProperty("projectileRadius").floatValue = 0.34f;
            shooterSo.FindProperty("knockbackDistance").floatValue = 3f;
            shooterSo.FindProperty("knockbackDuration").floatValue = 0.2f;
            shooterSo.FindProperty("verticalLift").floatValue = 0.18f;
            shooterSo.ApplyModifiedPropertiesWithoutUndo();

            EnemyLineOfSight los = Ensure<EnemyLineOfSight>(root);
            SerializedObject losSo = new SerializedObject(los);
            losSo.FindProperty("maxRange").floatValue = 24f;
            losSo.FindProperty("fovAngle").floatValue = 140f;
            losSo.FindProperty("eye").objectReferenceValue = eye;
            losSo.FindProperty("eyeHeight").floatValue = 0f;
            losSo.FindProperty("subdivisions").intValue = 20;
            losSo.FindProperty("maxIterations").intValue = 2;
            losSo.FindProperty("generateMesh").boolValue = false;
            losSo.FindProperty("flipForward180").boolValue = false;
            losSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject sensorSo = new SerializedObject(sensor);
            sensorSo.FindProperty("enemyData").objectReferenceValue = data;
            sensorSo.FindProperty("eyeSensor").objectReferenceValue = eye;
            sensorSo.FindProperty("lineOfSight").objectReferenceValue = los;
            sensorSo.FindProperty("flipForward180").boolValue = false;
            sensorSo.FindProperty("useMultiRayFov").boolValue = true;
            sensorSo.FindProperty("generateVisionMesh").boolValue = false;
            sensorSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject aiSo = new SerializedObject(ai);
            aiSo.FindProperty("enemyData").objectReferenceValue = data;
            aiSo.FindProperty("initializeFromEnemyData").boolValue = true;
            aiSo.FindProperty("sensor").objectReferenceValue = sensor;
            aiSo.FindProperty("health").objectReferenceValue = health;
            aiSo.FindProperty("knockback").objectReferenceValue = knockback;
            aiSo.FindProperty("animator").objectReferenceValue = animator;
            aiSo.FindProperty("attackHitbox").objectReferenceValue = attackHitbox;
            aiSo.FindProperty("projectileShooter").objectReferenceValue = shooter;
            SetObjectReferenceIfPresent(aiSo, "bossBehaviour", bossBehaviour);
            SetFloatIfPresent(aiSo, "modelYawOffset", 0f);
            aiSo.FindProperty("flipForward180").boolValue = false;
            aiSo.FindProperty("useHitAnimation").boolValue = true;
            aiSo.FindProperty("hitStunDuration").floatValue = 0.12f;
            aiSo.FindProperty("hurtCooldown").floatValue = 0.35f;
            aiSo.FindProperty("staggerDuration").floatValue = 0.45f;
            SetFloatIfPresent(aiSo, "hitReactionFallbackDuration", 0.9f);
            SetFloatIfPresent(aiSo, "staggerReactionFallbackDuration", 1.1f);
            aiSo.FindProperty("useDeathAnimation").boolValue = true;
            aiSo.FindProperty("deathAnimationDuration").floatValue = 2.5f;
            aiSo.FindProperty("useTackle").boolValue = false;
            aiSo.FindProperty("maxCombatVerticalDifference").floatValue = 3.5f;
            aiSo.FindProperty("attackStateTimeout").floatValue = 6f;
            aiSo.FindProperty("chaseDestinationInterval").floatValue = 0.08f;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            RemoveRelaysOutsideAnimator(root, animator);
            EnemyAnimationEventRelay relay = Ensure<EnemyAnimationEventRelay>(animator.gameObject);
            SerializedObject relaySo = new SerializedObject(relay);
            relaySo.FindProperty("aiOwner").objectReferenceValue = ai;
            relaySo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(relay);

            SerializedObject dissolveSo = new SerializedObject(dissolve);
            dissolveSo.FindProperty("characterHealth").objectReferenceValue = health;
            dissolveSo.FindProperty("startDelay").floatValue = 3f;
            dissolveSo.FindProperty("dissolveDuration").floatValue = 2.14f;
            dissolveSo.ApplyModifiedPropertiesWithoutUndo();

            float hudY = localBounds.max.y + Mathf.Max(0.35f, localBounds.size.y * 0.12f);
            EnemyHUDBuilder.EnsureHudOnRoot(root, hudY, 0.01f, 35f);

            marker.Configure(data.displayName, health);
            marker.ConfigureLockedArena(true);
            marker.ConfigureBossMusic(AssetDatabase.LoadAssetAtPath<AudioClip>(BossMusicPath));

            // Phần thưởng chắc chắn khi chết: 2 bình máu + đánh dấu boss đã hạ
            // (mở Floating Tree → Note #2). One-time-only.
            BossDeathRewardConfig reward = Ensure<BossDeathRewardConfig>(root);
            SerializedObject rewardSo = new SerializedObject(reward);
            rewardSo.FindProperty("healthPotionCount").intValue = 2;
            rewardSo.FindProperty("markAncientForestBossDefeated").boolValue = true;
            rewardSo.FindProperty("potionSpawnDelay").floatValue = 2.5f;
            SerializedProperty rewardOffsets = rewardSo.FindProperty("potionDropOffsets");
            rewardOffsets.ClearArray();
            rewardOffsets.InsertArrayElementAtIndex(0);
            rewardOffsets.GetArrayElementAtIndex(0).vector3Value = new Vector3(-1.1f, 0.6f, 1.2f);
            rewardOffsets.InsertArrayElementAtIndex(1);
            rewardOffsets.GetArrayElementAtIndex(1).vector3Value = new Vector3(1.1f, 0.6f, 1.2f);
            rewardSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(reward);
            marker.ConfigureBossDeathReward(reward);

            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
    }

    static Bounds CalculateLocalRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(new Vector3(0f, 2f, 0f), new Vector3(3f, 4f, 7f));

        Bounds local = new Bounds(root.transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                        local.Encapsulate(root.transform.InverseTransformPoint(corner));
                    }
        }

        return local;
    }

    static Bounds CalculateWorldRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position + Vector3.up * 2f, new Vector3(3f, 4f, 7f));
        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);
        return world;
    }

    static Transform FindTransformContaining(Transform root, string token)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
        }

        return null;
    }

    static Transform FindModelAnimationRoot(Transform root)
    {
        Transform hips = FindTransformByExactName(root, "Hips");
        Transform mesh = FindTransformByExactName(root, "U3DMesh");

        if (hips == null || mesh == null)
        {
            return null;
        }

        Transform common = FindCommonAncestor(hips, mesh);
        return common != root ? common : null;
    }

    static Transform FindTransformByExactName(Transform root, string exactName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, exactName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    static Transform FindCommonAncestor(Transform a, Transform b)
    {
        if (a == null || b == null)
        {
            return null;
        }

        HashSet<Transform> ancestors = new HashSet<Transform>();
        for (Transform current = a; current != null; current = current.parent)
        {
            ancestors.Add(current);
        }

        for (Transform current = b; current != null; current = current.parent)
        {
            if (ancestors.Contains(current))
            {
                return current;
            }
        }

        return null;
    }

    static void RemoveAnimatorsOutsideModelRoot(GameObject root, Transform modelRoot)
    {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        foreach (Animator existing in animators)
        {
            if (existing != null && existing.transform != modelRoot)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }
    }

    static void RemoveRelaysOutsideAnimator(GameObject root, Animator animator)
    {
        EnemyAnimationEventRelay[] relays =
            root.GetComponentsInChildren<EnemyAnimationEventRelay>(true);

        foreach (EnemyAnimationEventRelay existing in relays)
        {
            if (existing != null && existing.gameObject != animator.gameObject)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }
    }

    static void SetObjectReferenceIfPresent(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    static void SetFloatIfPresent(
        SerializedObject serializedObject,
        string propertyName,
        float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    static void EnsureAssetFolder(string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(directory)) EnsureFolder(directory);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
