#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Safely creates a close-combat Beach Tyran boss variant by duplicating the
/// existing prefab and EnemyData through Unity APIs instead of hand-written YAML.
/// Menu: ASTRA EDEN → Enemies → Create Beach Tyran Close Combat Variant
/// </summary>
public static class EnemyBossBeachTyranCloseCombatBuilder
{
    private const string SourcePrefabPath = "Assets/_Project/Prefab/Enemy_Boss_BeachTyran.prefab";
    private const string SourceEnemyDataPath =
        "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyBeachApexTyran.asset";
    private const string VariantPrefabName = "Enemy_Boss_BeachTyran_CloseCombat";
    private const string VariantPrefabPath = "Assets/_Project/Prefab/Enemy_Boss_BeachTyran_CloseCombat.prefab";
    private const string VariantEnemyDataPath =
        "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyBeachApexTyran_CloseCombat.asset";
    private const string BiteAttackPatternPath =
        "Assets/_Project/ScriptableObjects/Enemies/AttackPatterns/SO_AttackPattern_AtkRaptorBite.asset";
    private const string VariantBiteAttackPatternPath =
        "Assets/_Project/ScriptableObjects/Enemies/AttackPatterns/SO_AttackPattern_AtkRaptorBite_BeachTyranCloseCombat.asset";
    private const string SourceBiteAnimationPath =
        "Assets/Animations/Motion/Boss/attack.anim";
    private const string VariantBiteAnimationPath =
        "Assets/Animations/Enemy_Boss_BeachTyran_CloseCombat_BiteSynced.anim";
    private const string VariantAnimatorOverridePath =
        "Assets/Animations/Enemy_Boss_BeachTyran_CloseCombat_AnimatorOverride.overrideController";
    private const float BossChaseStoppingDistance = 0.15f;
    private const float BossChaseDestinationInterval = 0.05f;
    private const float BossBiteStartTime = 0.45f;
    private const float BossBiteHitTime = 1.1f;
    private const float BossBiteActiveTime = 0.3f;
    private const float BossBiteEndPadding = 0.2f;

    [MenuItem("ASTRA EDEN/Enemies/Create Beach Tyran Close Combat Variant")]
    public static void CreateVariant()
    {
        if (!ValidateSourceAssets())
        {
            return;
        }

        EnsureFolderForAsset(VariantEnemyDataPath);
        EnsureFolderForAsset(VariantPrefabPath);
        EnsureFolderForAsset(VariantBiteAttackPatternPath);
        EnsureFolderForAsset(VariantBiteAnimationPath);
        EnsureFolderForAsset(VariantAnimatorOverridePath);

        AnimationClip variantBiteAnimation = CreateOrReplaceBossBiteAnimation();
        if (variantBiteAnimation == null)
        {
            return;
        }

        AttackPatternData variantBitePattern = CreateOrReplaceBossBiteAttackPattern(variantBiteAnimation);
        if (variantBitePattern == null)
        {
            return;
        }

        EnemyData variantData = CreateOrReplaceEnemyDataVariant(variantBitePattern);
        if (variantData == null)
        {
            return;
        }

        GameObject variantPrefab = CreateOrReplacePrefabVariant(variantData, variantBiteAnimation);
        if (variantPrefab == null)
        {
            return;
        }

        SerializedObject dataSo = new SerializedObject(variantData);
        SetObjectReference(dataSo, "enemyPrefab", variantPrefab);
        dataSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(variantData);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BeachTyranCloseCombatBuilder] Generated EnemyData: {VariantEnemyDataPath}");
        Debug.Log($"[BeachTyranCloseCombatBuilder] Generated prefab: {VariantPrefabPath}");
        Debug.Log($"[BeachTyranCloseCombatBuilder] Generated bite attack pattern: {VariantBiteAttackPatternPath}");
        Debug.Log($"[BeachTyranCloseCombatBuilder] Generated bite animation with events: {VariantBiteAnimationPath}");
        Debug.Log($"[BeachTyranCloseCombatBuilder] Generated animator override: {VariantAnimatorOverridePath}");
    }

    /// <summary>Batchmode entry point: -executeMethod EnemyBossBeachTyranCloseCombatBuilder.CreateVariantBatch</summary>
    public static void CreateVariantBatch()
    {
        CreateVariant();
        EditorApplication.Exit(0);
    }

    private static bool ValidateSourceAssets()
    {
        bool valid = true;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath) == null)
        {
            Debug.LogError($"[BeachTyranCloseCombatBuilder] Missing source prefab: {SourcePrefabPath}");
            valid = false;
        }

        if (AssetDatabase.LoadAssetAtPath<EnemyData>(SourceEnemyDataPath) == null)
        {
            Debug.LogError($"[BeachTyranCloseCombatBuilder] Missing source EnemyData: {SourceEnemyDataPath}");
            valid = false;
        }

        if (AssetDatabase.LoadAssetAtPath<AttackPatternData>(BiteAttackPatternPath) == null)
        {
            Debug.LogError($"[BeachTyranCloseCombatBuilder] Missing bite attack pattern: {BiteAttackPatternPath}");
            valid = false;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceBiteAnimationPath) == null)
        {
            Debug.LogError($"[BeachTyranCloseCombatBuilder] Missing source bite animation: {SourceBiteAnimationPath}");
            valid = false;
        }

        return valid;
    }

    private static AnimationClip CreateOrReplaceBossBiteAnimation()
    {
        DeleteGeneratedAssetIfPresent(VariantBiteAnimationPath);

        AnimationClip sourceBiteAnimation = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceBiteAnimationPath);
        if (sourceBiteAnimation == null)
        {
            return null;
        }

        AnimationClip clip = Object.Instantiate(sourceBiteAnimation);
        clip.name = "Enemy_Boss_BeachTyran_CloseCombat_BiteSynced";

        GetBossBiteTiming(clip, out float attackStart, out float attackHit, out float attackEnd);
        AnimationUtility.SetAnimationEvents(
            clip,
            new[]
            {
                new AnimationEvent { time = attackStart, functionName = "OnAttackStart" },
                new AnimationEvent { time = attackHit, functionName = "OnAttackHit" },
                new AnimationEvent { time = attackEnd, functionName = "Anim_OnAttackEnd" },
            });

        AssetDatabase.CreateAsset(clip, VariantBiteAnimationPath);
        EditorUtility.SetDirty(clip);
        AssetDatabase.ImportAsset(VariantBiteAnimationPath);

        Debug.Log(
            $"[BeachTyranCloseCombatBuilder] Bite events synced: start={attackStart:F2}s hit={attackHit:F2}s end={attackEnd:F2}s");
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(VariantBiteAnimationPath);
    }

    private static AttackPatternData CreateOrReplaceBossBiteAttackPattern(AnimationClip variantBiteAnimation)
    {
        AttackPatternData sourceBite = AssetDatabase.LoadAssetAtPath<AttackPatternData>(BiteAttackPatternPath);
        AttackPatternData pattern = AssetDatabase.LoadAssetAtPath<AttackPatternData>(VariantBiteAttackPatternPath);
        if (pattern == null)
        {
            if (!AssetDatabase.CopyAsset(BiteAttackPatternPath, VariantBiteAttackPatternPath))
            {
                Debug.LogError(
                    $"[BeachTyranCloseCombatBuilder] Failed to copy AttackPattern {BiteAttackPatternPath} → {VariantBiteAttackPatternPath}");
                return null;
            }

            AssetDatabase.ImportAsset(VariantBiteAttackPatternPath);
            pattern = AssetDatabase.LoadAssetAtPath<AttackPatternData>(VariantBiteAttackPatternPath);
        }
        else
        {
            Undo.RegisterCompleteObjectUndo(pattern, "Refresh Beach Tyran Close Combat bite pattern from source");
            EditorUtility.CopySerialized(sourceBite, pattern);
        }

        if (pattern == null)
        {
            Debug.LogError(
                $"[BeachTyranCloseCombatBuilder] Failed to load copied AttackPattern: {VariantBiteAttackPatternPath}");
            return null;
        }

        GetBossBiteTiming(variantBiteAnimation, out _, out float attackHit, out float attackEnd);
        float recovery = Mathf.Max(0.5f, attackEnd - attackHit - BossBiteActiveTime);

        SerializedObject so = new SerializedObject(pattern);
        SetString(so, "attackId", "atk_beach_tyran_close_combat_bite");
        SetString(so, "displayName", "Beach Tyran Bite");
        SetFloat(so, "minRange", 0f);
        SetFloat(so, "maxRange", 3.5f);
        SetFloat(so, "cooldown", 2.2f);
        SetFloat(so, "windup", attackHit);
        SetFloat(so, "activeTime", BossBiteActiveTime);
        SetFloat(so, "recovery", recovery);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pattern);

        return pattern;
    }

    private static EnemyData CreateOrReplaceEnemyDataVariant(AttackPatternData variantBitePattern)
    {
        EnemyData sourceData = AssetDatabase.LoadAssetAtPath<EnemyData>(SourceEnemyDataPath);
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(VariantEnemyDataPath);
        if (data == null)
        {
            if (!AssetDatabase.CopyAsset(SourceEnemyDataPath, VariantEnemyDataPath))
            {
                Debug.LogError(
                    $"[BeachTyranCloseCombatBuilder] Failed to copy EnemyData {SourceEnemyDataPath} → {VariantEnemyDataPath}");
                return null;
            }

            AssetDatabase.ImportAsset(VariantEnemyDataPath);
            data = AssetDatabase.LoadAssetAtPath<EnemyData>(VariantEnemyDataPath);
        }
        else
        {
            Undo.RegisterCompleteObjectUndo(data, "Refresh Beach Tyran Close Combat EnemyData from source");
            EditorUtility.CopySerialized(sourceData, data);
        }

        if (data == null)
        {
            Debug.LogError($"[BeachTyranCloseCombatBuilder] Failed to load copied EnemyData: {VariantEnemyDataPath}");
            return null;
        }

        SerializedObject so = new SerializedObject(data);
        SetString(so, "enemyId", "enemy_beach_apex_tyran_close_combat");
        SetString(so, "displayName", "Beach Apex Tyran - Close Combat");
        SetFloat(so, "sightRange", 12f);
        SetFloat(so, "sightAngle", 129f);
        SetFloat(so, "hearingRange", 10f);
        SetFloat(so, "aggroKeepRange", 20f);
        SetFloat(so, "attackRange", 3.5f);
        SetFloat(so, "attackCooldown", 2.2f);

        SerializedProperty patterns = so.FindProperty("attackPatterns");
        if (patterns == null || !patterns.isArray)
        {
            Debug.LogError("[BeachTyranCloseCombatBuilder] EnemyData.attackPatterns property was not found.");
            return null;
        }

        patterns.ClearArray();
        patterns.InsertArrayElementAtIndex(0);
        patterns.GetArrayElementAtIndex(0).objectReferenceValue = variantBitePattern;

        // enemyPrefab is assigned after the duplicated prefab is saved.
        SetObjectReference(so, "enemyPrefab", null);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);

        return data;
    }

    private static GameObject CreateOrReplacePrefabVariant(EnemyData variantData, AnimationClip variantBiteAnimation)
    {
        DeleteGeneratedAssetIfPresent(VariantPrefabPath);

        if (!AssetDatabase.CopyAsset(SourcePrefabPath, VariantPrefabPath))
        {
            Debug.LogError(
                $"[BeachTyranCloseCombatBuilder] Failed to copy prefab {SourcePrefabPath} → {VariantPrefabPath}");
            return null;
        }

        AssetDatabase.ImportAsset(VariantPrefabPath);

        GameObject root = PrefabUtility.LoadPrefabContents(VariantPrefabPath);
        try
        {
            root.name = VariantPrefabName;
            Undo.RegisterFullObjectHierarchyUndo(root, "Configure Beach Tyran Close Combat prefab");

            EnemyAIController ai = EnsureComponent<EnemyAIController>(root);
            EnemySensor sensor = EnsureComponent<EnemySensor>(root);
            NavMeshAgent agent = EnsureComponent<NavMeshAgent>(root);
            Animator animator = root.GetComponentInChildren<Animator>(true);
            EnemyAnimationEventRelay relay = EnsureComponent<EnemyAnimationEventRelay>(root);
            EnemyAttackHitbox attackHitbox = root.GetComponentInChildren<EnemyAttackHitbox>(true);
            EnemyPushHitbox tacklePushHitbox = root.GetComponentInChildren<EnemyPushHitbox>(true);
            CharacterHealth health = EnsureComponent<CharacterHealth>(root);
            CharacterKnockback knockback = EnsureComponent<CharacterKnockback>(root);
            EnsureComponent<LootDropSpawner>(root);
            DissolveOnDeath dissolve = EnsureComponent<DissolveOnDeath>(root);

            SerializedObject aiSo = new SerializedObject(ai);
            SetObjectReference(aiSo, "enemyData", variantData);
            SetBool(aiSo, "initializeFromEnemyData", true);
            SetObjectReference(aiSo, "sensor", sensor);
            SetObjectReference(aiSo, "health", health);
            SetObjectReference(aiSo, "knockback", knockback);
            if (animator != null)
            {
                SetObjectReference(aiSo, "animator", animator);
            }

            if (attackHitbox != null)
            {
                SetObjectReference(aiSo, "attackHitbox", attackHitbox);
            }

            SetBool(aiSo, "useTackle", true);
            SetInt(aiSo, "attacksBeforeTackle", 2);
            SetFloat(aiSo, "tackleRange", 5.5f);
            SetFloat(aiSo, "tackleCooldown", 8f);
            // Boss-only chase tuning: keep the agent pursuing the player until the
            // close-combat EnemyData.attackRange is reached, then let the AI attack.
            SetFloat(aiSo, "chaseDestinationInterval", BossChaseDestinationInterval);
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            ConfigureBossChaseAgent(agent, variantData);
            ConfigureBossAnimator(animator, variantBiteAnimation);

            SerializedObject sensorSo = new SerializedObject(sensor);
            SetObjectReference(sensorSo, "enemyData", variantData);
            sensorSo.ApplyModifiedPropertiesWithoutUndo();

            if (relay != null)
            {
                SerializedObject relaySo = new SerializedObject(relay);
                SetObjectReference(relaySo, "aiOwner", ai);
                SerializedProperty tackleHitboxProp = relaySo.FindProperty("tacklePushHitbox");
                if (tackleHitboxProp != null && tacklePushHitbox != null)
                {
                    tackleHitboxProp.objectReferenceValue = tacklePushHitbox;
                }

                relaySo.ApplyModifiedPropertiesWithoutUndo();
            }

            SerializedObject dissolveSo = new SerializedObject(dissolve);
            SetObjectReference(dissolveSo, "characterHealth", health);
            dissolveSo.ApplyModifiedPropertiesWithoutUndo();

            if (attackHitbox == null)
            {
                Debug.LogWarning(
                    $"[BeachTyranCloseCombatBuilder] No EnemyAttackHitbox found on duplicated prefab: {VariantPrefabPath}");
            }

            PrefabUtility.SaveAsPrefabAsset(root, VariantPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(VariantPrefabPath);
    }

    private static void ConfigureBossAnimator(Animator animator, AnimationClip variantBiteAnimation)
    {
        if (animator == null)
        {
            Debug.LogWarning(
                $"[BeachTyranCloseCombatBuilder] No Animator found on duplicated prefab: {VariantPrefabPath}");
            return;
        }

        Undo.RecordObject(animator, "Configure Beach Tyran Close Combat animator");
        // Boss uses the same shared enemy gameplay components/triggers as normal
        // enemies. Keep the copied boss avatar and swap only the copied controller
        // through an override that adds bite Animation Events for this variant.
        AnimatorOverrideController overrideController =
            CreateOrReplaceAnimatorOverride(animator.runtimeAnimatorController, variantBiteAnimation);
        if (overrideController != null)
        {
            animator.runtimeAnimatorController = overrideController;
        }

        animator.applyRootMotion = false;
        EditorUtility.SetDirty(animator);
    }

    private static AnimatorOverrideController CreateOrReplaceAnimatorOverride(
        RuntimeAnimatorController baseController,
        AnimationClip variantBiteAnimation)
    {
        if (baseController == null || variantBiteAnimation == null)
        {
            return null;
        }

        DeleteGeneratedAssetIfPresent(VariantAnimatorOverridePath);

        var overrideController = new AnimatorOverrideController(baseController)
        {
            name = "Enemy_Boss_BeachTyran_CloseCombat_AnimatorOverride",
        };

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(overrides);
        bool replacedAttackClip = false;
        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip originalClip = overrides[i].Key;
            if (originalClip == null)
            {
                continue;
            }

            if (originalClip.name == "attack" || originalClip.name == "Basic Attack")
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(originalClip, variantBiteAnimation);
                replacedAttackClip = true;
            }
        }

        if (!replacedAttackClip)
        {
            Debug.LogWarning(
                "[BeachTyranCloseCombatBuilder] No 'attack' or 'Basic Attack' clip entry was found in the boss Animator controller override list.");
        }

        overrideController.ApplyOverrides(overrides);
        AssetDatabase.CreateAsset(overrideController, VariantAnimatorOverridePath);
        EditorUtility.SetDirty(overrideController);
        AssetDatabase.ImportAsset(VariantAnimatorOverridePath);
        return AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(VariantAnimatorOverridePath);
    }

    private static void GetBossBiteTiming(
        AnimationClip clip,
        out float attackStart,
        out float attackHit,
        out float attackEnd)
    {
        float clipLength = clip != null ? clip.length : 2.4f;
        attackStart = Mathf.Clamp(BossBiteStartTime, 0f, Mathf.Max(0f, clipLength - 0.1f));
        attackHit = Mathf.Clamp(BossBiteHitTime, attackStart + 0.05f, Mathf.Max(attackStart + 0.05f, clipLength - 0.1f));
        attackEnd = Mathf.Max(attackHit + BossBiteActiveTime + 0.1f, clipLength - BossBiteEndPadding);
        attackEnd = Mathf.Clamp(attackEnd, attackHit + BossBiteActiveTime + 0.05f, clipLength);
    }

    private static void ConfigureBossChaseAgent(NavMeshAgent agent, EnemyData variantData)
    {
        if (agent == null)
        {
            Debug.LogWarning(
                $"[BeachTyranCloseCombatBuilder] No NavMeshAgent found on duplicated prefab: {VariantPrefabPath}");
            return;
        }

        Undo.RecordObject(agent, "Configure Beach Tyran Close Combat chase agent");
        if (variantData != null && variantData.baseStats != null)
        {
            agent.speed = variantData.baseStats.moveSpeed;
            agent.angularSpeed = variantData.baseStats.turnSpeed;
        }

        agent.stoppingDistance = BossChaseStoppingDistance;
        agent.autoBraking = false;
        agent.updateRotation = false;
        EditorUtility.SetDirty(agent);
    }

    private static T EnsureComponent<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        component = Undo.AddComponent<T>(root);
        EditorUtility.SetDirty(root);
        return component;
    }

    private static void EnsureFolderForAsset(string assetPath)
    {
        string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
    }

    private static void DeleteGeneratedAssetIfPresent(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) == null)
        {
            return;
        }

        Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        Undo.RegisterCompleteObjectUndo(asset, $"Replace generated asset {assetPath}");
        if (!AssetDatabase.DeleteAsset(assetPath))
        {
            FileUtil.DeleteFileOrDirectory(assetPath);
            FileUtil.DeleteFileOrDirectory(assetPath + ".meta");
            AssetDatabase.Refresh();
        }
    }

    private static void SetString(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty property = FindRequiredProperty(so, propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty property = FindRequiredProperty(so, propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetInt(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty property = FindRequiredProperty(so, propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty property = FindRequiredProperty(so, propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetObjectReference(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = FindRequiredProperty(so, propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static SerializedProperty FindRequiredProperty(SerializedObject so, string propertyName)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError(
                $"[BeachTyranCloseCombatBuilder] Serialized property '{propertyName}' was not found on {so.targetObject}.");
        }

        return property;
    }
}
#endif
