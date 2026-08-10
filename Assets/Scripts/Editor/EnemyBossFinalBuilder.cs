#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent builder for the humanoid Commander. It reuses Player motion
/// clips, but gives the boss its own controller, data and AI behaviour.
/// </summary>
public static class EnemyBossFinalBuilder
{
    const string BossPrefabPath = "Assets/_Project/Prefab/Enemy_Boss_Final.prefab";
    const string AnimatorPath = "Assets/Animations/Enemy_Boss_Final_Animator.controller";
    const string DataPath = "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_FinalCommander.asset";
    const string PatternFolder = "Assets/_Project/ScriptableObjects/Enemies/AttackPatterns/FinalBoss";
    const string SummonSourcePath = "Assets/_Project/Prefab/Enemy_MiniBoss_Velociraptor.prefab";
    const string SummonPrefabPath = "Assets/_Project/Prefab/Enemy_FinalBoss_RangedSummon.prefab";
    const string SummonDataPath = "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_FinalBossRangedSummon.asset";
    const string PoisonPatternPath = "Assets/_Project/ScriptableObjects/Enemies/AttackPatterns/SO_AttackPattern_AtkVelociraptorPoisonOrb.asset";
    const string SwordPrefabPath = "Assets/Prefabs/Environment/Hovl Studio/Package Magic sword/Prefabs/MagicSword_Iron.prefab";
    const string WorldScenePath = "Assets/Scenes/World_Eden7.unity";
    const string ArenaMarkerName = "Poi swaning";

    const string MotionFolder = "Assets/Animations/Motion/Player/";

    sealed class Clips
    {
        public AnimationClip Idle;
        public AnimationClip Walk;
        public AnimationClip Run;
        public AnimationClip Attack1;
        public AnimationClip Attack2;
        public AnimationClip Attack3;
        public AnimationClip Heavy;
        public AnimationClip Hit;
        public AnimationClip Stagger;
        public AnimationClip PowerUp;
        public AnimationClip Summon;
        public AnimationClip Death;
    }

    [MenuItem("ASTRA EDEN/Enemies/Build Final Boss")]
    public static void Build()
    {
        if (!ValidateInputs()) return;

        EnsureFolder(PatternFolder);
        Clips clips = LoadClips();
        if (clips == null) return;

        List<AttackPatternData> patterns = BuildAttackPatterns(clips);
        EnemyData summonData = BuildSummonData();
        GameObject summonPrefab = BuildSummonPrefab(summonData);
        EnemyData bossData = BuildBossData(patterns);
        AnimatorController controller = BuildAnimator(clips);

        if (patterns.Count != 3 || summonPrefab == null || bossData == null || controller == null)
        {
            Debug.LogError("[FinalBossBuilder] Missing generated dependency; build stopped.");
            return;
        }

        GameObject bossPrefab = BuildBossPrefab(bossData, controller, summonPrefab);
        if (bossPrefab == null) return;

        AssignPrefab(bossData, bossPrefab);
        AssignPrefab(summonData, summonPrefab);
        PlaceBossAtZone3Marker(bossPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateGeneratedAssets(controller, bossData, summonPrefab);
        Debug.Log(
            "[FinalBossBuilder] Build complete | " +
            $"Prefab={BossPrefabPath} | Animator={AnimatorPath} | " +
            $"Summon={SummonPrefabPath} | Arena marker='{ArenaMarkerName}'.");
    }

    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    static bool ValidateInputs()
    {
        bool valid = Require<GameObject>(BossPrefabPath, "existing Final Boss artist prefab") &&
                     Require<GameObject>(SummonSourcePath, "ranged Velociraptor source prefab") &&
                     Require<AttackPatternData>(PoisonPatternPath, "Poison Orb attack pattern") &&
                     Require<GameObject>(SwordPrefabPath, "Player MagicSword_Iron prefab");

        string[] clips =
        {
            "Great Sword Idle.anim", "Walk.anim", "Fast Run.anim",
            "Basic Attack.anim", "Great Sword Slash.anim", "Right Hook.anim",
            "Standing React Small From Front.anim",
            "Receiving An Uppercut.anim", "Great Sword Casting.anim", "Pointing.anim",
            "Two Handed Sword Death.anim",
        };

        foreach (string clip in clips)
        {
            valid &= Require<AnimationClip>(MotionFolder + clip, clip);
        }

        return valid;
    }

    static bool Require<T>(string path, string label) where T : UnityEngine.Object
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return true;
        Debug.LogError($"[FinalBossBuilder] Missing {label}: {path}");
        return false;
    }

    static Clips LoadClips()
    {
        Clips clips = new Clips
        {
            Idle = LoadClip("Great Sword Idle.anim"),
            Walk = LoadClip("Walk.anim"),
            Run = LoadClip("Fast Run.anim"),
            Attack1 = LoadClip("Basic Attack.anim"),
            Attack2 = LoadClip("Great Sword Slash.anim"),
            Attack3 = LoadClip("Right Hook.anim"),
            // Skill R (index 3 của Player). Q/High Spin không còn được Final Boss dùng.
            Heavy = LoadClip("Great Sword Casting.anim"),
            Hit = LoadClip("Standing React Small From Front.anim"),
            Stagger = LoadClip("Receiving An Uppercut.anim"),
            PowerUp = LoadClip("Great Sword Casting.anim"),
            Summon = LoadClip("Pointing.anim"),
            Death = LoadClip("Two Handed Sword Death.anim"),
        };

        return clips.GetType().GetFields()
            .All(field => field.GetValue(clips) != null)
            ? clips
            : null;
    }

    static AnimationClip LoadClip(string fileName) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>(MotionFolder + fileName);

    static List<AttackPatternData> BuildAttackPatterns(Clips clips)
    {
        return new List<AttackPatternData>
        {
            ConfigurePattern(
                "SO_AttackPattern_FinalBossAttack1.asset", "final_basic_attack", "Commander Basic Attack",
                "Attack", clips.Attack1, 2.25f, 1f, 1.1f,
                new Vector3(0.55f, 0.85f, 0.65f), new Vector3(0f, 1f, 1f)),
            ConfigurePattern(
                "SO_AttackPattern_FinalBossAttack2.asset", "final_skill_e", "Commander Skill E",
                "Attack2", clips.Attack2, 2.25f, 1.25f, 1.45f,
                new Vector3(0.75f, 0.9f, 0.75f), new Vector3(0f, 1f, 1.1f)),
            ConfigurePattern(
                "SO_AttackPattern_FinalBossHeavy.asset", "final_skill_r", "Commander Skill R",
                "HeavyAttack", clips.Heavy, 2.25f, 1.8f, 2.35f,
                new Vector3(1.15f, 0.95f, 1.15f), new Vector3(0f, 1f, 0.35f), 3f),
        };
    }

    static AttackPatternData ConfigurePattern(
        string fileName,
        string attackId,
        string displayName,
        string trigger,
        AnimationClip clip,
        float range,
        float damageMultiplier,
        float cooldown,
        Vector3 halfExtents,
        Vector3 offset,
        float knockback = 0f)
    {
        string path = PatternFolder + "/" + fileName;
        AttackPatternData pattern = GetOrCreateAsset<AttackPatternData>(path);
        float length = Mathf.Max(0.6f, clip != null ? clip.length : 1f);

        pattern.attackId = attackId;
        pattern.displayName = displayName;
        pattern.archetype = EnemyArchetype.Boss;
        pattern.rangeType = EnemyAttackRangeType.Melee;
        pattern.minRange = 0f;
        pattern.maxRange = range;
        pattern.cooldown = cooldown;
        pattern.windup = Mathf.Clamp(length * 0.42f, 0.2f, 0.8f);
        pattern.activeTime = Mathf.Clamp(length * 0.16f, 0.12f, 0.32f);
        pattern.recovery = Mathf.Max(0.2f, length - pattern.windup - pattern.activeTime);
        if (attackId == "final_skill_r")
        {
            // Great Sword Casting phát OnAttackHit ở 2.333s. Khớp timed
            // fallback với frame này để R không gây damage khi mới giơ kiếm.
            pattern.windup = 2.2f;
            pattern.activeTime = 0.3f;
            pattern.recovery = Mathf.Max(0.5f, length - 2.5f);
        }
        pattern.animationTrigger = trigger;
        pattern.damageMultiplier = damageMultiplier;
        pattern.poiseDamage = damageMultiplier * 12f;
        pattern.element = DamageElement.Physical;
        pattern.overrideHitbox = true;
        pattern.hitboxShape = EnemyAttackHitbox.HitShape.Box;
        pattern.hitboxHalfExtents = halfExtents;
        pattern.hitboxLocalOffset = offset;
        pattern.knockbackDistance = knockback;
        pattern.knockbackDuration = 0.2f;
        pattern.knockbackVerticalLift = knockback > 0f ? 0.15f : 0f;
        pattern.canBeInterrupted = true;
        pattern.telegraph = $"Humanoid Commander move using Player clip: {clip?.name}.";
        EditorUtility.SetDirty(pattern);
        return pattern;
    }

    static EnemyData BuildBossData(List<AttackPatternData> patterns)
    {
        EnemyData data = GetOrCreateAsset<EnemyData>(DataPath);
        data.enemyId = "enemy_final_commander";
        data.displayName = "COMMANDER";
        data.archetype = EnemyArchetype.Boss;
        data.rank = EnemyRank.FinalBoss;
        data.zone = EnemyZone.CrystalCore;
        data.baseStats.maxHP = 3200f;
        data.baseStats.attack = 48f;
        data.baseStats.defense = 75f;
        data.baseStats.poise = 180f;
        data.baseStats.moveSpeed = 4.4f;
        data.baseStats.turnSpeed = 720f;
        data.sightRange = 26f;
        data.sightAngle = 150f;
        data.hearingRange = 12f;
        data.aggroKeepRange = 38f;
        // Humanoid sword range: force the Commander to actually close the gap
        // instead of beginning a melee swing several metres away.
        data.attackRange = 2.25f;
        data.attackCooldown = 1.45f;
        data.attackPatterns = new List<AttackPatternData>(patterns);
        data.expReward = 650;
        data.goldMin = 350;
        data.goldMax = 500;
        data.description =
            "Final Commander of the invading force. Tactical humanoid melee boss who commands modified ranged dinosaurs.";
        EditorUtility.SetDirty(data);
        return data;
    }

    static EnemyData BuildSummonData()
    {
        EnemyData data = GetOrCreateAsset<EnemyData>(SummonDataPath);
        data.enemyId = "enemy_final_boss_ranged_summon";
        data.displayName = "Modified Ranged Raptor";
        data.archetype = EnemyArchetype.Ranged;
        data.rank = EnemyRank.Normal;
        data.zone = EnemyZone.CrystalCore;
        data.baseStats.maxHP = 320f;
        data.baseStats.attack = 18f;
        data.baseStats.defense = 15f;
        data.baseStats.poise = 30f;
        data.baseStats.moveSpeed = 3.4f;
        data.baseStats.turnSpeed = 650f;
        data.sightRange = 20f;
        data.sightAngle = 150f;
        data.hearingRange = 10f;
        data.aggroKeepRange = 30f;
        data.attackRange = 12f;
        data.attackCooldown = 2.25f;
        data.attackPatterns = new List<AttackPatternData>
        {
            AssetDatabase.LoadAssetAtPath<AttackPatternData>(PoisonPatternPath),
        };
        data.expReward = 35;
        data.goldMin = 0;
        data.goldMax = 0;
        data.mainLootTable = null;
        data.description = "Lightweight ranged support summoned by the Final Commander.";
        EditorUtility.SetDirty(data);
        return data;
    }

    static AnimatorController BuildAnimator(Clips clips)
    {
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);
        if (existing != null)
        {
            UpdateExistingFinalBossAnimator(existing, clips);
            return existing;
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
        AddFloat(controller, "Blend");
        AddFloat(controller, "Horizontal");
        AddFloat(controller, "Vertical");
        AddTrigger(controller, "Attack");
        AddTrigger(controller, "Attack2");
        AddTrigger(controller, "Attack3");
        AddTrigger(controller, "HeavyAttack");
        AddTrigger(controller, "Hit");
        AddTrigger(controller, "Stagger");
        AddTrigger(controller, "PowerUp");
        AddTrigger(controller, "Summon");
        AddTrigger(controller, "Die");
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Phase2", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        BlendTree locomotionTree = new BlendTree
        {
            name = "Final Boss Locomotion Blend",
            blendParameter = "Blend",
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(locomotionTree, controller);
        locomotionTree.AddChild(clips.Idle, 0f);
        locomotionTree.AddChild(clips.Walk, 1f);
        locomotionTree.AddChild(clips.Run, 2f);

        AnimatorState locomotion = machine.AddState("Locomotion");
        locomotion.motion = locomotionTree;
        machine.defaultState = locomotion;

        AnimatorState attack1 = AddState(machine, "Attack 1", clips.Attack1);
        AnimatorState attack2 = AddState(machine, "Skill E", clips.Attack2);
        AnimatorState attack3 = AddState(machine, "Attack 3", clips.Attack3);
        AnimatorState heavy = AddState(machine, "Skill R", clips.Heavy);
        AnimatorState hit = AddState(machine, "Hit", clips.Hit);
        AnimatorState stagger = AddState(machine, "Stagger", clips.Stagger);
        AnimatorState powerUp = AddState(machine, "Power Up", clips.PowerUp);
        AnimatorState summon = AddState(machine, "Summon Dinosaurs", clips.Summon);
        AnimatorState death = AddState(machine, "Death", clips.Death);

        powerUp.AddStateMachineBehaviour<FinalBossSpecialStateEventBehaviour>()
            .Configure(false, 0.5f, 0.94f);
        summon.AddStateMachineBehaviour<FinalBossSpecialStateEventBehaviour>()
            .Configure(true, 0.55f, 0.94f);

        AddAnyTransition(machine, death, "Die", 0.04f);
        AddAnyTransition(machine, hit, "Hit", 0.05f);
        AddAnyTransition(machine, stagger, "Stagger", 0.05f);
        AddAnyTransition(machine, powerUp, "PowerUp", 0.08f);
        AddAnyTransition(machine, summon, "Summon", 0.08f);
        AddAnyTransition(machine, attack1, "Attack", 0.06f);
        AddAnyTransition(machine, attack2, "Attack2", 0.06f);
        AddAnyTransition(machine, attack3, "Attack3", 0.06f);
        AddAnyTransition(machine, heavy, "HeavyAttack", 0.08f);

        foreach (AnimatorState state in new[] { attack1, attack2, attack3, heavy, hit, stagger, powerUp, summon })
        {
            AnimatorStateTransition transition = state.AddTransition(locomotion);
            transition.hasExitTime = true;
            transition.exitTime = 0.96f;
            transition.duration = 0.08f;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static void UpdateExistingFinalBossAnimator(AnimatorController controller, Clips clips)
    {
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states)
        {
            AnimatorState state = child.state;
            if (state.name == "Attack 2" || state.name == "Skill E")
            {
                state.name = "Skill E";
                state.motion = clips.Attack2;
            }
            else if (state.name == "Heavy Attack" || state.name == "Skill R")
            {
                state.name = "Skill R";
                state.motion = clips.Heavy;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion)
    {
        AnimatorState state = machine.AddState(name);
        state.motion = motion;
        return state;
    }

    static void AddAnyTransition(
        AnimatorStateMachine machine,
        AnimatorState destination,
        string trigger,
        float duration)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(destination);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    static void AddFloat(AnimatorController controller, string name) =>
        controller.AddParameter(name, AnimatorControllerParameterType.Float);

    static void AddTrigger(AnimatorController controller, string name) =>
        controller.AddParameter(name, AnimatorControllerParameterType.Trigger);

    static GameObject BuildSummonPrefab(EnemyData data)
    {
        GameObject source = PrefabUtility.LoadPrefabContents(SummonSourcePath);
        try
        {
            source.name = "Enemy_FinalBoss_RangedSummon";

            foreach (MiniBossMarker marker in source.GetComponentsInChildren<MiniBossMarker>(true))
            {
                UnityEngine.Object.DestroyImmediate(marker, true);
            }
            foreach (BossDeathRewardConfig reward in source.GetComponentsInChildren<BossDeathRewardConfig>(true))
            {
                UnityEngine.Object.DestroyImmediate(reward, true);
            }

            CharacterHealth health = Ensure<CharacterHealth>(source);
            health.ApplyEnemyStats(data.baseStats);
            EnemyAIController ai = Ensure<EnemyAIController>(source);
            EnemySensor sensor = source.GetComponentInChildren<EnemySensor>(true);

            SerializedObject aiSo = new SerializedObject(ai);
            SetObject(aiSo, "enemyData", data);
            SetBool(aiSo, "initializeFromEnemyData", true);
            SetBool(aiSo, "enableLowHealthRetreat", false);
            SetBool(aiSo, "enableRandomWalkWhenNoPatrol", false);
            SetFloat(aiSo, "returnDistance", 12f);
            SetBool(aiSo, "useTackle", false);
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            if (sensor != null)
            {
                SerializedObject sensorSo = new SerializedObject(sensor);
                SetObject(sensorSo, "enemyData", data);
                sensorSo.ApplyModifiedPropertiesWithoutUndo();
            }

            LootDropSpawner loot = source.GetComponent<LootDropSpawner>();
            loot?.ConfigureFromEnemyData(data);
            EnemyKillTracker tracker = Ensure<EnemyKillTracker>(source);
            tracker.Configure(false, data);

            PrefabUtility.SaveAsPrefabAsset(source, SummonPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(source);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(SummonPrefabPath);
    }

    static GameObject BuildBossPrefab(
        EnemyData data,
        RuntimeAnimatorController controller,
        GameObject summonPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        try
        {
            root.name = "Enemy_Boss_Final";
            Animator animator = EnsureSingleAnimator(root);
            if (animator == null)
            {
                Debug.LogError("[FinalBossBuilder] Final boss model has no visual root for Animator.");
                return null;
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            if (!EnsureCommanderSword(root, animator))
            {
                return null;
            }

            // Only the humanoid skinned meshes may define the receive collider.
            // MagicSword_Iron contains VFX renderers with very large bounds; the
            // old all-Renderer calculation moved the CapsuleCollider roughly 48m
            // away from the visible boss, making player attacks miss completely.
            Bounds bounds = CalculateHumanoidRendererBounds(root);
            CapsuleCollider body = EnsureSingle<CapsuleCollider>(root);
            body.isTrigger = false;
            body.direction = 1;
            body.height = Mathf.Clamp(bounds.size.y, 1.7f, 2.6f);
            body.radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z), 0.35f, 0.65f);
            body.center = new Vector3(0f, body.height * 0.5f, 0f);

            Rigidbody rigidbody = EnsureSingle<Rigidbody>(root);
            rigidbody.mass = 95f;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;

            NavMeshAgent agent = EnsureSingle<NavMeshAgent>(root);
            agent.radius = body.radius;
            agent.height = body.height;
            agent.speed = data.baseStats.moveSpeed;
            agent.angularSpeed = data.baseStats.turnSpeed;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.15f;
            agent.autoBraking = true;
            agent.autoRepath = true;

            CharacterHealth health = EnsureSingle<CharacterHealth>(root);
            health.ApplyEnemyStats(data.baseStats);
            CharacterKnockback knockback = EnsureSingle<CharacterKnockback>(root);
            FinalBossBehaviour behaviour = EnsureSingle<FinalBossBehaviour>(root);
            EnemySensor sensor = EnsureSingle<EnemySensor>(root);
            EnemyAIController ai = EnsureSingle<EnemyAIController>(root);
            AudioSource audio = EnsureSingle<AudioSource>(root);
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;

            Transform attackNode = EnsureChild(root.transform, "AttackHitbox");
            EnemyAttackHitbox hitbox = EnsureSingle<EnemyAttackHitbox>(attackNode.gameObject);
            SerializedObject hitboxSo = new SerializedObject(hitbox);
            SetEnum(hitboxSo, "shape", (int)EnemyAttackHitbox.HitShape.Box);
            SetVector3(hitboxSo, "boxHalfExtents", new Vector3(0.55f, 0.85f, 0.65f));
            SetVector3(hitboxSo, "localOffset", new Vector3(0f, 1f, 1f));
            SetInt(hitboxSo, "targetLayer", LayerMask.GetMask("Player"));
            SetFloat(hitboxSo, "minimumHitInterval", 1f);
            SetBool(hitboxSo, "ignoreTriggerColliders", true);
            hitboxSo.ApplyModifiedPropertiesWithoutUndo();
            hitbox.CaptureDefaultConfiguration();

            Transform eye = EnsureChild(root.transform, "EyeSensor");
            eye.localPosition = new Vector3(0f, 1.6f, 0.12f);
            EnemyLineOfSight lineOfSight = EnsureSingle<EnemyLineOfSight>(root);
            SerializedObject losSo = new SerializedObject(lineOfSight);
            SetFloat(losSo, "maxRange", 28f);
            SetFloat(losSo, "fovAngle", 150f);
            SetObject(losSo, "eye", eye);
            SetBool(losSo, "generateMesh", false);
            losSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject sensorSo = new SerializedObject(sensor);
            SetObject(sensorSo, "enemyData", data);
            SetObject(sensorSo, "eyeSensor", eye);
            SetObject(sensorSo, "lineOfSight", lineOfSight);
            SetBool(sensorSo, "generateVisionMesh", false);
            sensorSo.ApplyModifiedPropertiesWithoutUndo();

            Transform points = EnsureChild(root.transform, "SummonPoints");
            Transform left = EnsureChild(points, "DinoSpawn_Left");
            Transform right = EnsureChild(points, "DinoSpawn_Right");
            if (left.localPosition == Vector3.zero) left.localPosition = new Vector3(-6f, 0f, 2f);
            if (right.localPosition == Vector3.zero) right.localPosition = new Vector3(6f, 0f, 2f);

            Transform vfxRoot = EnsureChild(root.transform, "VFX");
            Transform powerUpVfx = EnsureChild(vfxRoot, "PowerUpVFX");
            Light powerLight = EnsureSingle<Light>(powerUpVfx.gameObject);
            powerLight.type = LightType.Point;
            powerLight.color = new Color(0.55f, 0.3f, 1f);
            powerLight.intensity = 4f;
            powerLight.range = 6f;
            powerUpVfx.gameObject.SetActive(false);

            SerializedObject behaviourSo = new SerializedObject(behaviour);
            SetObject(behaviourSo, "rangedDinoSummonPrefab", summonPrefab);
            SetObject(behaviourSo, "summonSpawnLeft", left);
            SetObject(behaviourSo, "summonSpawnRight", right);
            SetFloat(behaviourSo, "summonMinPlayerDistance", 6f);
            SetFloat(behaviourSo, "summonCooldownAfterBothDead", 6f);
            SetFloat(behaviourSo, "phase2HealthThreshold", 0.5f);
            SetFloat(behaviourSo, "phase2MovementMultiplier", 1.15f);
            SetFloat(behaviourSo, "phase2DamageMultiplier", 1.2f);
            SetFloat(behaviourSo, "phase2CooldownMultiplier", 0.85f);
            SetFloat(behaviourSo, "lowHealthRThreshold", 0.25f);
            SetFloat(behaviourSo, "meleeEngageDistance", 1.45f);
            SetFloat(behaviourSo, "specialSkillChance", 0.3f);
            SetFloat(behaviourSo, "specialSkillCooldown", 5.5f);
            SetFloat(behaviourSo, "summonArenaRadius", 15.5f);
            SetObject(behaviourSo, "audioSource", audio);
            SetObject(behaviourSo, "powerUpVfx", powerUpVfx.gameObject);
            SetBool(behaviourSo, "cleanupSummonsOnBossDeath", true);
            behaviourSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject aiSo = new SerializedObject(ai);
            SetObject(aiSo, "enemyData", data);
            SetBool(aiSo, "initializeFromEnemyData", true);
            SetObject(aiSo, "bossBehaviour", behaviour);
            SetObject(aiSo, "sensor", sensor);
            SetObject(aiSo, "health", health);
            SetObject(aiSo, "knockback", knockback);
            SetObject(aiSo, "animator", animator);
            SetObject(aiSo, "attackHitbox", hitbox);
            SetBool(aiSo, "forceHitReactionOnAnyDamage", true);
            SetFloat(aiSo, "hitReactionFallbackDuration", Mathf.Max(0.75f, LoadClip("Standing React Small From Front.anim").length));
            SetFloat(aiSo, "staggerReactionFallbackDuration", Mathf.Max(0.9f, LoadClip("Receiving An Uppercut.anim").length));
            SetBool(aiSo, "enableLowHealthRetreat", false);
            // Final Boss waits at the designer-authored castle-front position
            // until the Player enters the arena; it must not wander away first.
            SetBool(aiSo, "enableRandomWalkWhenNoPatrol", false);
            SetBool(aiSo, "forceRunAnimationWhileChasing", true);
            SetBool(aiSo, "useTackle", false);
            SetFloat(aiSo, "returnDistance", 32f);
            SetFloat(aiSo, "maxCombatVerticalDifference", 3f);
            SetFloat(aiSo, "deathAnimationDuration", Mathf.Max(2f, LoadClip("Two Handed Sword Death.anim").length));
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            foreach (EnemyAnimationEventRelay oldRelay in root.GetComponentsInChildren<EnemyAnimationEventRelay>(true))
            {
                if (oldRelay.gameObject != animator.gameObject)
                {
                    UnityEngine.Object.DestroyImmediate(oldRelay, true);
                }
            }
            EnemyAnimationEventRelay relay = EnsureSingle<EnemyAnimationEventRelay>(animator.gameObject);
            SerializedObject relaySo = new SerializedObject(relay);
            SetObject(relaySo, "aiOwner", ai);
            relaySo.ApplyModifiedPropertiesWithoutUndo();

            EnemyKillTracker killTracker = EnsureSingle<EnemyKillTracker>(root);
            killTracker.Configure(true, data);
            MiniBossMarker marker = EnsureSingle<MiniBossMarker>(root);
            marker.Configure(data.displayName, health);
            marker.ConfigureLockedArena(true);
            SerializedObject markerSo = new SerializedObject(marker);
            SetFloat(markerSo, "arenaRadius", 18f);
            SetFloat(markerSo, "arenaEngageDistance", 17f);
            SetFloat(markerSo, "engageDistance", 22f);
            SetFloat(markerSo, "disengageDistance", 30f);
            markerSo.ApplyModifiedPropertiesWithoutUndo();

            EnemyHUDBuilder.EnsureHudOnRoot(root, Mathf.Max(2.2f, bounds.max.y + 0.25f), 0.01f, 35f);
            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
    }

    static void PlaceBossAtZone3Marker(GameObject prefab)
    {
        Scene scene = SceneManager.GetSceneByPath(WorldScenePath);
        bool openedForBuild = !scene.IsValid() || !scene.isLoaded;
        if (openedForBuild)
        {
            scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject marker = FindInScene(scene, ArenaMarkerName);
            GameObject existing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FinalBossBehaviour>(true))
                .Select(component => component.gameObject)
                .FirstOrDefault();

            bool createdNewInstance = existing == null;
            if (createdNewInstance)
            {
                existing = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                existing.name = "Enemy_Boss_Final";
            }

            if (createdNewInstance && marker != null)
            {
                existing.transform.SetPositionAndRotation(marker.transform.position, marker.transform.rotation);
                Debug.Log($"[FinalBossBuilder] Placed Final Boss at '{ArenaMarkerName}' {marker.transform.position} (Zone 3 / castle front).");
            }
            else if (createdNewInstance)
            {
                Debug.LogWarning(
                    $"[FinalBossBuilder] '{ArenaMarkerName}' not found. Boss instance kept at {existing.transform.position}; reposition it before the Zone 3 castle.");
            }
            else
            {
                Debug.Log(
                    $"[FinalBossBuilder] Preserved designer Final Boss transform at {existing.transform.position}; builder did not reposition it.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (openedForBuild && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName) return child.gameObject;
            }
        }
        return null;
    }

    static void AssignPrefab(EnemyData data, GameObject prefab)
    {
        if (data == null || prefab == null) return;
        data.enemyPrefab = prefab;
        EditorUtility.SetDirty(data);
    }

    static void ValidateGeneratedAssets(
        AnimatorController controller,
        EnemyData data,
        GameObject summonPrefab)
    {
        GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        bool valid = boss != null &&
                     Count<CharacterHealth>(boss) == 1 &&
                     Count<EnemyAIController>(boss) == 1 &&
                     Count<FinalBossBehaviour>(boss) == 1 &&
                     Count<EnemyAttackHitbox>(boss) == 1 &&
                     Count<EnemyAnimationEventRelay>(boss) == 1 &&
                     Count<Animator>(boss) == 1 &&
                     boss.GetComponentsInChildren<Transform>(true).Any(t => t.name == "FinalBoss_Sword") &&
                     boss.transform.Find("SummonPoints/DinoSpawn_Left") != null &&
                     boss.transform.Find("SummonPoints/DinoSpawn_Right") != null &&
                     data != null && data.attackPatterns.Count == 3 &&
                     summonPrefab != null;

        string[] required =
        {
            "Blend", "Horizontal", "Vertical", "Attack", "Attack2", "Attack3",
            "HeavyAttack", "Hit", "Stagger", "PowerUp", "Summon", "Die", "IsDead", "Phase2",
        };
        HashSet<string> parameters = new HashSet<string>(controller.parameters.Select(parameter => parameter.name));
        valid &= required.All(parameters.Contains);

        if (!valid)
        {
            Debug.LogError("[FinalBossBuilder] Validation failed: duplicates, references, patterns or Animator parameters are invalid.");
            return;
        }

        Debug.Log("[FinalBossBuilder] Validation passed: Basic-primary + Skill E/R, two arena-leashed summons and dedicated Animator.");
    }

    static int Count<T>(GameObject root) where T : Component =>
        root.GetComponentsInChildren<T>(true).Length;

    static Animator EnsureSingleAnimator(GameObject root)
    {
        Animator[] all = root.GetComponentsInChildren<Animator>(true);
        Animator keep = all.FirstOrDefault();
        if (keep == null)
        {
            Transform visual = root.transform.childCount > 0 ? root.transform.GetChild(0) : root.transform;
            keep = visual.gameObject.AddComponent<Animator>();
        }

        foreach (Animator animator in all)
        {
            if (animator != keep)
            {
                UnityEngine.Object.DestroyImmediate(animator, true);
            }
        }
        return keep;
    }

    static bool EnsureCommanderSword(GameObject root, Animator animator)
    {
        Transform hand = animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.RightHand)
            : null;
        hand ??= root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(transform => transform.name == "J_Bip_R_Hand") ??
            root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform =>
                    transform.name.IndexOf("RightHand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    transform.name.IndexOf("R_Hand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    transform.name.IndexOf("Right_wrist", StringComparison.OrdinalIgnoreCase) >= 0);
        if (hand == null)
        {
            Debug.LogError("[FinalBossBuilder] Could not find the Commander's right-hand bone for the sword.");
            return false;
        }

        Transform existing = hand.Find("FinalBoss_Sword");
        if (existing != null)
        {
            return true;
        }

        GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
        GameObject sword = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab, hand);
        if (sword == null)
        {
            Debug.LogError($"[FinalBossBuilder] Could not instantiate sword prefab: {SwordPrefabPath}");
            return false;
        }

        sword.name = "FinalBoss_Sword";
        // Same right-hand offsets already used by Player.prefab.
        sword.transform.localPosition = new Vector3(0.0601f, -0.0072f, 0.0087f);
        sword.transform.localRotation = Quaternion.identity;
        sword.transform.localScale = Vector3.one * 0.4f;
        return true;
    }

    static T EnsureSingle<T>(GameObject root) where T : Component
    {
        T[] existing = root.GetComponentsInChildren<T>(true);
        T keep = existing.FirstOrDefault(component => component.gameObject == root) ?? existing.FirstOrDefault();
        if (keep == null) keep = root.AddComponent<T>();
        foreach (T component in existing)
        {
            if (component != keep) UnityEngine.Object.DestroyImmediate(component, true);
        }
        return keep;
    }

    static T Ensure<T>(GameObject root) where T : Component =>
        root.GetComponent<T>() ?? root.AddComponent<T>();

    static Transform EnsureChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null) return child;
        GameObject created = new GameObject(childName);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static Bounds CalculateHumanoidRendererBounds(GameObject root)
    {
        // Skinned meshes are the Commander body/clothing. Regular Renderers
        // include the sword particles, trails, lights and HUD, none of which
        // belong in a physical receive collider.
        Renderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Cast<Renderer>()
            .ToArray();
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.up * 0.9f, new Vector3(0.9f, 1.8f, 0.9f));
        }

        Bounds bounds = new Bounds(root.transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            Vector3 min = renderer.bounds.min;
            Vector3 max = renderer.bounds.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                bounds.Encapsulate(root.transform.InverseTransformPoint(new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z)));
            }
        }
        return bounds;
    }

    static void SetObject(SerializedObject so, string name, UnityEngine.Object value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.objectReferenceValue = value;
    }

    static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.floatValue = value;
    }

    static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.intValue = value;
    }

    static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    static void SetVector3(SerializedObject so, string name, Vector3 value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.vector3Value = value;
    }

    static void SetEnum(SerializedObject so, string name, int value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.enumValueIndex = value;
    }
}
#endif
