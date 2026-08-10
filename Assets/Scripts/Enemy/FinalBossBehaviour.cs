using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Commander-only combat layer. EnemyAIController still owns locomotion,
/// melee, Hurt/Stagger and death; this component owns Summon and Phase 2.
/// </summary>
[DisallowMultipleComponent]
public sealed class FinalBossBehaviour : EnemyBossBehaviour
{
    enum ExclusiveAction
    {
        None,
        Summon,
        PowerUp,
    }

    static readonly int SummonHash = Animator.StringToHash("Summon");
    static readonly int PowerUpHash = Animator.StringToHash("PowerUp");
    static readonly int Phase2Hash = Animator.StringToHash("Phase2");

    [Header("Boss Identity")]
    [SerializeField] private string bossDisplayName = "COMMANDER";

    [Header("Summon Ranged Dinosaurs")]
    [SerializeField] private GameObject rangedDinoSummonPrefab;
    [SerializeField] private Transform summonSpawnLeft;
    [SerializeField] private Transform summonSpawnRight;
    [SerializeField, Min(0f)] private float summonMinPlayerDistance = 6f;
    [SerializeField, Min(0f)] private float summonCooldownAfterBothDead = 6f;
    [SerializeField, Min(0.25f)] private float summonActionDuration = 1.7f;
    [SerializeField, Range(0.05f, 0.95f)] private float summonFallbackEventNormalized = 0.55f;
    [SerializeField] private GameObject summonVfxPrefab;
    [SerializeField] private AudioClip summonSfx;
    [SerializeField] private AudioSource audioSource;

    [Header("Phase 2")]
    [SerializeField, Range(0.05f, 0.95f)] private float phase2HealthThreshold = 0.5f;
    [SerializeField, Min(0.25f)] private float powerUpDuration = 1.8f;
    [SerializeField, Min(1f)] private float phase2MovementMultiplier = 1.15f;
    [SerializeField, Min(1f)] private float phase2DamageMultiplier = 1.2f;
    [SerializeField, Range(0.1f, 1f)] private float phase2CooldownMultiplier = 0.85f;
    [SerializeField] private GameObject powerUpVfx;

    [Header("Low Health Skill R")]
    [SerializeField, Range(0.05f, 0.5f)] private float lowHealthRThreshold = 0.25f;
    [SerializeField] private string skillRAttackId = "final_skill_r";

    [Header("Melee Skill Pacing")]
    [SerializeField] private string basicAttackId = "final_basic_attack";
    [Tooltip("Khoảng cách tâm-to-tâm phải áp sát trước khi Commander được phép bắt đầu động tác đánh.")]
    [SerializeField, Min(0.5f)] private float meleeEngageDistance = 1.45f;
    [SerializeField, Range(0f, 1f)] private float specialSkillChance = 0.3f;
    [SerializeField, Min(0f)] private float specialSkillCooldown = 5.5f;

    [Header("Summon Arena Leash")]
    [SerializeField, Min(2f)] private float summonArenaRadius = 15.5f;

    [Header("Death")]
    [SerializeField] private bool cleanupSummonsOnBossDeath = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool phase2Activated;
    [SerializeField] private bool phase2CombatEnabled;
    [SerializeField] private int activeSummonCount;
    [SerializeField] private string activeActionName;
    [SerializeField] private bool lowHealthRTriggered;

    readonly List<CharacterHealth> activeSummons = new List<CharacterHealth>(2);

    EnemyAIController owner;
    CharacterHealth ownerHealth;
    Animator ownerAnimator;
    ExclusiveAction activeAction;
    bool phase2Pending;
    bool lowHealthRPending;
    bool actionEventResolved;
    bool actionEndRequested;
    bool summonGroupWasActive;
    float actionElapsed;
    float nextSummonAllowedTime;
    float nextSpecialSkillAllowedTime;
    int actionToken;
    Vector3 summonArenaCenter;

    public string BossDisplayName => bossDisplayName;
    public int ActiveSummonCount => activeSummons.Count;
    public int CurrentActionToken => actionToken;
    public bool IsPhase2 => phase2CombatEnabled;

    public override void Initialize(EnemyAIController controller)
    {
        base.Initialize(controller);
        owner = controller;
        ownerHealth = GetComponent<CharacterHealth>();
        ownerAnimator = GetComponentInChildren<Animator>(true);
        summonArenaCenter = transform.position;

        if (ownerHealth != null)
        {
            ownerHealth.Changed -= HandleOwnerHealthChanged;
            ownerHealth.Changed += HandleOwnerHealthChanged;
            ownerHealth.Died -= HandleOwnerDied;
            ownerHealth.Died += HandleOwnerDied;
            EvaluateHealthThresholds();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    void OnDestroy()
    {
        if (ownerHealth != null)
        {
            ownerHealth.Changed -= HandleOwnerHealthChanged;
            ownerHealth.Died -= HandleOwnerDied;
        }

        UnsubscribeAllSummons();
    }

    void Update()
    {
        PruneDestroyedSummons();
    }

    void LateUpdate()
    {
        KeepSummonsInsideArena();
    }

    public override float GetEffectiveAttackRange(EnemyData data)
    {
        // Pattern.maxRange là reach của hitbox, không phải khoảng cách được
        // phép bắt đầu animation. Humanoid phải chạy sát rồi mới vung kiếm.
        return Mathf.Max(0.5f, meleeEngageDistance);
    }

    public override bool CanStartExclusiveAction(
        EnemyData data,
        float distanceToPlayer,
        float attackCooldownRemaining)
    {
        PruneDestroyedSummons();

        if (ownerHealth == null || ownerHealth.IsDead || activeAction != ExclusiveAction.None)
        {
            return false;
        }

        // Phase transition has priority and is independent from melee distance.
        if (phase2Pending && !phase2Activated)
        {
            return true;
        }

        // Khi đã vào ngưỡng nguy cấp, ưu tiên áp sát và dùng R trước;
        // không chen một lượt Summon vào giữa.
        if (lowHealthRPending)
        {
            return false;
        }

        return rangedDinoSummonPrefab != null &&
               summonSpawnLeft != null &&
               summonSpawnRight != null &&
               activeSummons.Count == 0 &&
               Time.time >= nextSummonAllowedTime &&
               distanceToPlayer >= summonMinPlayerDistance;
    }

    public override bool CanStartSpecialAttack(
        EnemyData data,
        float distance,
        float cooldownRemaining)
    {
        AttackPatternData skillR = FindSkillR(data);
        return lowHealthRPending &&
               skillR != null &&
               distance >= skillR.minRange &&
               distance <= Mathf.Min(skillR.maxRange, meleeEngageDistance);
    }

    public override AttackPatternData SelectPriorityAttackPattern(
        EnemyData data,
        float distanceToPlayer)
    {
        AttackPatternData skillR = FindSkillR(data);
        if (lowHealthRPending)
        {
            if (IsPatternInRange(skillR, distanceToPlayer) &&
                distanceToPlayer <= meleeEngageDistance)
            {
                lowHealthRPending = false;
                lowHealthRTriggered = true;
                nextSpecialSkillAllowedTime = Time.time + specialSkillCooldown;
                Debug.Log($"[FinalBoss] {name}: HP nguy cấp -> ưu tiên dùng Skill R ngay.", this);
                return skillR;
            }

            return null;
        }

        AttackPatternData basic = FindPattern(data, basicAttackId);
        if (!IsPatternInRange(basic, distanceToPlayer))
        {
            return null;
        }

        // Đánh thường là chính. E/R chỉ được roll khi hết cooldown riêng và
        // qua xác suất, nên không thể nối special liên tục.
        if (Time.time < nextSpecialSkillAllowedTime || Random.value > specialSkillChance)
        {
            return basic;
        }

        List<AttackPatternData> availableSkills = new List<AttackPatternData>(2);
        if (data != null && data.attackPatterns != null)
        {
            foreach (AttackPatternData pattern in data.attackPatterns)
            {
                if (pattern != null &&
                    pattern != basic &&
                    IsPatternInRange(pattern, distanceToPlayer))
                {
                    availableSkills.Add(pattern);
                }
            }
        }

        if (availableSkills.Count == 0)
        {
            return basic;
        }

        nextSpecialSkillAllowedTime = Time.time + specialSkillCooldown;
        return availableSkills[Random.Range(0, availableSkills.Count)];
    }

    public override void BeginExclusiveAction(Animator animator, Transform player)
    {
        ownerAnimator = animator != null ? animator : ownerAnimator;
        actionElapsed = 0f;
        actionEventResolved = false;
        actionEndRequested = false;
        actionToken++;

        if (phase2Pending && !phase2Activated)
        {
            activeAction = ExclusiveAction.PowerUp;
            activeActionName = "PowerUp";
            // Mark the one-shot transition as consumed immediately. PowerUp has
            // hyper armor, so it cannot visually restart from further HP events.
            phase2Activated = true;
            phase2Pending = false;
            ResetTrigger(SummonHash);
            SetTrigger(PowerUpHash);
            if (powerUpVfx != null)
            {
                powerUpVfx.SetActive(true);
            }
            return;
        }

        activeAction = ExclusiveAction.Summon;
        activeActionName = "Summon";
        ResetTrigger(PowerUpHash);
        SetTrigger(SummonHash);
    }

    public override bool TickExclusiveAction(float deltaTime, Animator animator, Transform player)
    {
        if (activeAction == ExclusiveAction.None)
        {
            return true;
        }

        actionElapsed += Mathf.Max(0f, deltaTime);
        float duration = activeAction == ExclusiveAction.PowerUp
            ? powerUpDuration
            : summonActionDuration;

        // Null-safe fallback when a controller loses its state behaviour/event.
        if (activeAction == ExclusiveAction.Summon &&
            !actionEventResolved &&
            actionElapsed >= duration * summonFallbackEventNormalized)
        {
            ResolveExclusiveActionEvent(actionToken);
        }

        if (!actionEndRequested && actionElapsed < duration)
        {
            return false;
        }

        if (activeAction == ExclusiveAction.PowerUp)
        {
            phase2CombatEnabled = true;
            if (ownerAnimator != null && HasParameter(Phase2Hash, AnimatorControllerParameterType.Bool))
            {
                ownerAnimator.SetBool(Phase2Hash, true);
            }
        }

        FinishExclusiveAction();
        return true;
    }

    public override void CancelExclusiveAction()
    {
        if (activeAction == ExclusiveAction.None)
        {
            return;
        }

        actionToken++;
        ResetTrigger(SummonHash);
        ResetTrigger(PowerUpHash);

        // PowerUp is non-interruptible during combat; this branch is used by
        // death/scene teardown only. Never enable Phase 2 after death.
        if (powerUpVfx != null)
        {
            powerUpVfx.SetActive(false);
        }

        activeAction = ExclusiveAction.None;
        activeActionName = string.Empty;
        actionEventResolved = true;
        actionEndRequested = false;
        actionElapsed = 0f;
    }

    public override void Anim_OnExclusiveActionEvent()
    {
        ResolveExclusiveActionEvent(actionToken);
    }

    public void ResolveExclusiveActionEvent(int expectedToken)
    {
        if (expectedToken != actionToken ||
            activeAction != ExclusiveAction.Summon ||
            actionEventResolved ||
            owner == null ||
            owner.State != EnemyAIController.AIState.Special ||
            ownerHealth == null ||
            ownerHealth.IsDead ||
            activeSummons.Count != 0)
        {
            return;
        }

        actionEventResolved = true;
        SpawnSummonPair();
    }

    public override void Anim_OnExclusiveActionEnd()
    {
        RequestExclusiveActionEnd(actionToken);
    }

    public void RequestExclusiveActionEnd(int expectedToken)
    {
        if (expectedToken == actionToken && activeAction != ExclusiveAction.None)
        {
            actionEndRequested = true;
        }
    }

    public override bool ExclusiveActionCanBeInterrupted =>
        activeAction != ExclusiveAction.PowerUp;

    public override float GetMovementSpeedMultiplier() =>
        phase2CombatEnabled ? phase2MovementMultiplier : 1f;

    public override float GetAttackDamageMultiplier() =>
        phase2CombatEnabled ? phase2DamageMultiplier : 1f;

    public override float GetAttackCooldownMultiplier() =>
        phase2CombatEnabled ? phase2CooldownMultiplier : 1f;

    public override void OnOwnerDeath()
    {
        phase2Pending = false;
        lowHealthRPending = false;
        CancelExclusiveAction();

        if (cleanupSummonsOnBossDeath)
        {
            CleanupLivingSummons();
        }
    }

    void HandleOwnerHealthChanged(CharacterHealth _)
    {
        EvaluateHealthThresholds();
    }

    void HandleOwnerDied(CharacterHealth _)
    {
        OnOwnerDeath();
    }

    void EvaluateHealthThresholds()
    {
        if (ownerHealth == null || ownerHealth.IsDead)
        {
            return;
        }

        float healthRatio = ownerHealth.NormalizedHealth;
        if (!phase2Activated && healthRatio <= phase2HealthThreshold)
        {
            phase2Pending = true;
        }

        if (!lowHealthRTriggered && healthRatio <= lowHealthRThreshold)
        {
            lowHealthRPending = true;
        }
    }

    AttackPatternData FindSkillR(EnemyData data) => FindPattern(data, skillRAttackId);

    static AttackPatternData FindPattern(EnemyData data, string attackId)
    {
        if (data == null || data.attackPatterns == null)
        {
            return null;
        }

        foreach (AttackPatternData pattern in data.attackPatterns)
        {
            if (pattern != null && pattern.attackId == attackId)
            {
                return pattern;
            }
        }

        return null;
    }

    static bool IsPatternInRange(AttackPatternData pattern, float distance) =>
        pattern != null && distance >= pattern.minRange && distance <= pattern.maxRange;

    void KeepSummonsInsideArena()
    {
        float radius = Mathf.Max(2f, summonArenaRadius);
        float radiusSquared = radius * radius;
        for (int i = 0; i < activeSummons.Count; i++)
        {
            CharacterHealth summon = activeSummons[i];
            if (summon == null || summon.IsDead)
            {
                continue;
            }

            Transform summonRoot = summon.transform.root;
            Vector3 planar = summonRoot.position - summonArenaCenter;
            planar.y = 0f;
            if (planar.sqrMagnitude <= radiusSquared)
            {
                continue;
            }

            Vector3 clamped = summonArenaCenter + planar.normalized * radius;
            clamped.y = summonRoot.position.y;
            NavMeshAgent summonAgent = summonRoot.GetComponent<NavMeshAgent>();
            if (summonAgent != null && summonAgent.enabled && summonAgent.isOnNavMesh &&
                NavMesh.SamplePosition(clamped, out NavMeshHit hit, 2f, summonAgent.areaMask))
            {
                summonAgent.Warp(hit.position);
            }
            else
            {
                summonRoot.position = clamped;
            }
        }
    }

    void SpawnSummonPair()
    {
        if (rangedDinoSummonPrefab == null || summonSpawnLeft == null || summonSpawnRight == null)
        {
            Debug.LogError($"[FinalBoss] {name}: thiếu prefab hoặc hai Summon Point; không spawn nửa cặp.", this);
            return;
        }

        CharacterHealth left = SpawnTrackedSummon(summonSpawnLeft, "DinoSummon_Left");
        CharacterHealth right = SpawnTrackedSummon(summonSpawnRight, "DinoSummon_Right");

        if (left == null || right == null)
        {
            if (left != null) Destroy(left.transform.root.gameObject);
            if (right != null) Destroy(right.transform.root.gameObject);
            UnsubscribeAllSummons();
            activeSummons.Clear();
            activeSummonCount = 0;
            Debug.LogError($"[FinalBoss] {name}: summon pair invalid; đã huỷ cả cặp để không tạo 1 minion lẻ.", this);
            return;
        }

        summonGroupWasActive = true;
        activeSummonCount = activeSummons.Count;
        if (summonSfx != null && audioSource != null)
        {
            audioSource.PlayOneShot(summonSfx);
        }

        Debug.Log($"[FinalBoss] {name}: spawned exactly 2 ranged dinosaurs.", this);
    }

    CharacterHealth SpawnTrackedSummon(Transform point, string instanceName)
    {
        if (summonVfxPrefab != null)
        {
            Instantiate(summonVfxPrefab, point.position, point.rotation);
        }

        GameObject instance = Instantiate(
            rangedDinoSummonPrefab,
            point.position,
            point.rotation);
        instance.name = instanceName;

        CharacterHealth summonHealth = instance.GetComponent<CharacterHealth>() ??
                                        instance.GetComponentInChildren<CharacterHealth>(true);
        if (summonHealth == null)
        {
            Destroy(instance);
            return null;
        }

        summonHealth.Died -= HandleSummonDied;
        summonHealth.Died += HandleSummonDied;
        activeSummons.Add(summonHealth);
        return summonHealth;
    }

    void HandleSummonDied(CharacterHealth summon)
    {
        if (summon != null)
        {
            summon.Died -= HandleSummonDied;
        }

        activeSummons.Remove(summon);
        RefreshSummonCountAndCooldown();
    }

    void PruneDestroyedSummons()
    {
        bool removed = false;
        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            if (activeSummons[i] == null)
            {
                activeSummons.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
        {
            RefreshSummonCountAndCooldown();
        }
        else
        {
            activeSummonCount = activeSummons.Count;
        }
    }

    void RefreshSummonCountAndCooldown()
    {
        activeSummonCount = activeSummons.Count;
        if (summonGroupWasActive && activeSummons.Count == 0)
        {
            summonGroupWasActive = false;
            nextSummonAllowedTime = Time.time + summonCooldownAfterBothDead;
            Debug.Log($"[FinalBoss] {name}: both summons cleared; summon unlocks in {summonCooldownAfterBothDead:F1}s.", this);
        }
    }

    void CleanupLivingSummons()
    {
        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            CharacterHealth summon = activeSummons[i];
            if (summon == null) continue;
            summon.Died -= HandleSummonDied;
            Destroy(summon.transform.root.gameObject);
        }

        activeSummons.Clear();
        activeSummonCount = 0;
        summonGroupWasActive = false;
    }

    void UnsubscribeAllSummons()
    {
        foreach (CharacterHealth summon in activeSummons)
        {
            if (summon != null)
            {
                summon.Died -= HandleSummonDied;
            }
        }
    }

    void FinishExclusiveAction()
    {
        ResetTrigger(SummonHash);
        ResetTrigger(PowerUpHash);
        if (powerUpVfx != null)
        {
            powerUpVfx.SetActive(false);
        }

        activeAction = ExclusiveAction.None;
        activeActionName = string.Empty;
        actionElapsed = 0f;
        actionEndRequested = false;
    }

    void SetTrigger(int hash)
    {
        if (ownerAnimator != null && HasParameter(hash, AnimatorControllerParameterType.Trigger))
        {
            ownerAnimator.ResetTrigger(hash);
            ownerAnimator.SetTrigger(hash);
        }
    }

    void ResetTrigger(int hash)
    {
        if (ownerAnimator != null && HasParameter(hash, AnimatorControllerParameterType.Trigger))
        {
            ownerAnimator.ResetTrigger(hash);
        }
    }

    bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (ownerAnimator == null || ownerAnimator.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in ownerAnimator.parameters)
        {
            if (parameter.nameHash == hash && parameter.type == type)
            {
                return true;
            }
        }

        return false;
    }
}
