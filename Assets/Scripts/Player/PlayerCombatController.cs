using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterHealth))]
public class PlayerCombatController : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerAnimatorBridge animatorBridge;
    [SerializeField] private float attackLockDuration = 0.75f;
    [SerializeField] private float attack2MoveDistance = 3f;
    [SerializeField] private float attack2MoveDuration = 1f;
    [SerializeField] private int attackMoveSkillIndex = 1;
    [SerializeField] private float[] skillLockDurations = new float[4] { 0.75f, 1f, 1f, 3.5f };

    [Header("Damage")]
    [SerializeField] private CharacterHealth characterHealth;
    [SerializeField] private Transform attackPoint;
    [FormerlySerializedAs("enemyLayer")]
    [SerializeField] private LayerMask damageMask;
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private float attackRadius = 1.8f;
    [SerializeField] private float attackForwardOffset = 1.3f;
    [SerializeField] private float enemyKnockbackDistance = 1.2f;
    [SerializeField] private float enemyKnockbackDuration = 0.18f;

    [Header("Cooldown")]
    [SerializeField] private PlayerSkillCooldown skillCooldown;
    [Tooltip("Map skill index (0..3) -> SkillData. Khi assign, cooldown sẽ lấy từ SkillData.cooldown. Để null = dùng baseCooldown trong PlayerSkillCooldown.")]
    [SerializeField] private SkillData[] skillBindings = new SkillData[4];

    [Header("Damage Timing")]
    [Tooltip("Bật để chỉ gây dame khi Animation Event OnAttackHit() được gọi từ clip. Tắt = quay lại flow cũ (dame ngay lúc bấm).")]
    [SerializeField] private bool useAnimationEventDamage = true;
    [Tooltip("Fallback: nếu bật và sau X giây không có Animation Event nào, sẽ tự gây dame để tránh kẹt. 0 = tắt fallback.")]
    [SerializeField] private float damageEventFallback = 0.4f;
    [Tooltip("Dame cho từng skillIndex (0..3). Để 0 = dùng attackDamage chung.")]
    [SerializeField] private float[] perSkillDamage = new float[4] { 0f, 0f, 0f, 0f };

    [Header("Audio")]
    [SerializeField] private PlayerAudioController audioController;

    [Header("Skill R — vùng sát thương (Crystal Burst)")]
    [SerializeField] private int areaDamageSkillIndex = 3;
    [SerializeField] private float areaDamageRadius = 5f;
    [SerializeField, Min(0.02f)] private float areaDamageTickInterval = 0.02f;
    [Tooltip("Giới hạn an toàn nếu OnAttackEnd không được gọi. 0 = chỉ dừng theo animation.")]
    [SerializeField, Min(0f)] private float areaDamageMaxDuration = 8f;
    [Tooltip("Để 0 = tự tính từ attackDamage × damageMultiplier của SkillData R.")]
    [SerializeField] private float areaDamagePerTick = 0f;
    [SerializeField] private Transform areaDamageOrigin;
    [Tooltip("Tâm vùng AOE theo trục Y từ player (khớp VFX).")]
    [SerializeField] private float areaDamageHeightOffset = 2f;

    private float attackLockTimer;
    private float attackMoveRemaining;
    private float swingElapsed;
    private bool swingActive;
    private bool swingHitResolved;
    private readonly Collider[] attackHits = new Collider[16];
    private readonly CharacterHealth[] damagedTargets = new CharacterHealth[16];
    private readonly HashSet<CharacterHealth> areaTickTargets = new HashSet<CharacterHealth>();
    private Coroutine areaDamageRoutine;
    private bool areaDamageWindowOpen;
    private int currentSkillIndex;
    private float hitInputLockedUntil;

    public bool IsAttacking => attackLockTimer > 0f;
    public bool IsAttackMoveActive => attackMoveRemaining > 0f;
    public bool IsUsingSpecialSkill =>
        currentSkillIndex > 0 &&
        (attackLockTimer > 0f || swingActive || areaDamageWindowOpen);
    public float AttackMoveSpeed => attack2MoveDistance / Mathf.Max(attack2MoveDuration, 0.001f);
    public float AttackDamage => characterHealth != null && characterHealth.RuntimeStats != null
        ? characterHealth.RuntimeStats.attack
        : attackDamage;

    public void SetAttackDamageForDebug(float damage)
    {
        attackDamage = Mathf.Max(0.1f, damage);
        if (characterHealth != null && characterHealth.RuntimeStats != null)
        {
            characterHealth.RuntimeStats.attack = attackDamage;
        }
    }

    private void Reset()
    {
        inputReader = GetComponent<PlayerInputReader>();
        animatorBridge = GetComponent<PlayerAnimatorBridge>();
        characterHealth = GetComponent<CharacterHealth>();
    }

    private void Awake()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        if (animatorBridge == null)
        {
            animatorBridge = GetComponent<PlayerAnimatorBridge>();
        }

        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        if (skillCooldown == null)
        {
            skillCooldown = GetComponent<PlayerSkillCooldown>();
        }

        if (audioController == null)
        {
            audioController = GetComponent<PlayerAudioController>();
        }

        AssignDefaultDamageMask();
    }

    private void OnDisable()
    {
        CloseAreaDamageWindow();
    }

    private void Update()
    {
        if (PlayerDeathController.IsPlayerDead || (characterHealth != null && characterHealth.IsDead))
        {
            CloseAreaDamageWindow();
            attackLockTimer = 0f;
            return;
        }

        TickAttackLock();

        if (inputReader == null || animatorBridge == null)
        {
            return;
        }

        if (Time.time < hitInputLockedUntil)
        {
            return;
        }

        if (!IsAttacking)
        {
            int pressedSkill = inputReader.SkillIndexPressed;
            if (pressedSkill >= 0)
            {
                if (skillCooldown == null || skillCooldown.CanUseCombatSkill(pressedSkill))
                {
                    StartAttack(pressedSkill);
                }
                return;
            }

            if (inputReader.AttackPressed)
            {
                StartAttack(0);
            }
        }
    }

    private void StartAttack(int skillIndex)
    {
        currentSkillIndex = Mathf.Clamp(skillIndex, 0, 3);
        attackLockTimer = skillLockDurations[currentSkillIndex];

        if (currentSkillIndex == attackMoveSkillIndex)
        {
            attackMoveRemaining = attack2MoveDuration;
        }

        if (animatorBridge != null)
        {
            animatorBridge.TriggerCastSkill(currentSkillIndex);
        }

        if (skillCooldown != null && currentSkillIndex > 0)
        {
            float duration = GetSkillCooldown(currentSkillIndex);
            skillCooldown.StartCooldownForCombatIndex(currentSkillIndex, duration);
        }

        BeginSwing();

        // Phát âm thanh khi bắt đầu attack
        if (audioController != null)
        {
            audioController.OnAttackStarted(currentSkillIndex);
        }

        if (!useAnimationEventDamage && currentSkillIndex != areaDamageSkillIndex)
        {
            ApplyAttackDamage();
            swingHitResolved = true;
        }
    }

    private void BeginSwing()
    {
        swingActive = true;
        swingHitResolved = false;
        swingElapsed = 0f;
    }

    public void InterruptForHit(float duration)
    {
        hitInputLockedUntil =
            Mathf.Max(hitInputLockedUntil, Time.time + Mathf.Max(0f, duration));
        attackLockTimer = 0f;
        attackMoveRemaining = 0f;
        swingActive = false;
        swingHitResolved = false;
        swingElapsed = 0f;
        CloseAreaDamageWindow();
    }

    /// <summary>Bắt đầu vùng sát thương chiêu R. Gọi từ SpawnMultipleSlashesVFX / OnAttackHit.</summary>
    public void OnAreaDamageStart()
    {
        if (areaDamageRoutine != null)
        {
            return;
        }

        areaDamageWindowOpen = true;
        areaDamageRoutine = StartCoroutine(AreaDamagePulseRoutine());
    }

    /// <summary>Dừng vùng sát thương chiêu R. Gọi từ OnAttackEnd.</summary>
    public void OnAreaDamageEnd()
    {
        CloseAreaDamageWindow();
    }

    /// <summary>Gọi từ Animation Event ở frame impact của clip attack.</summary>
    public void OnAttackHit()
    {
        if (IsAreaSkillActive())
        {
            OnAreaDamageStart();
            return;
        }

        if (!swingActive || swingHitResolved)
        {
            return;
        }

        swingHitResolved = true;
        ApplyAttackDamage();

        // Phát âm thanh khi trúng (OnAttackHit)
        if (audioController != null)
        {
            audioController.OnAttackHitSound();
        }
    }

    /// <summary>Gọi cuối clip để dừng vùng damage + reset swing.</summary>
    public void OnAttackEnd()
    {
        if (areaDamageWindowOpen || areaDamageRoutine != null)
        {
            CloseAreaDamageWindow();
        }

        swingActive = false;
        swingHitResolved = false;
        swingElapsed = 0f;

        // Phát âm thanh kết thúc attack
        if (audioController != null)
        {
            audioController.OnAttackEndSound();
        }
    }

    private void TickAttackLock()
    {
        if (attackLockTimer > 0f)
        {
            attackLockTimer -= Time.deltaTime;
        }

        if (attackMoveRemaining > 0f)
        {
            attackMoveRemaining -= Time.deltaTime;
        }

        if (!swingActive)
        {
            return;
        }

        swingElapsed += Time.deltaTime;

        if (useAnimationEventDamage
            && !IsAreaSkillActive()
            && !swingHitResolved
            && damageEventFallback > 0f
            && swingElapsed >= damageEventFallback)
        {
            OnAttackHit();
        }

        if (attackLockTimer <= 0f)
        {
            swingActive = false;
            swingHitResolved = false;
            swingElapsed = 0f;

            if (areaDamageWindowOpen || areaDamageRoutine != null)
            {
                CloseAreaDamageWindow();
            }
        }
    }

    private IEnumerator AreaDamagePulseRoutine()
    {
        float interval = Mathf.Max(0.1f, areaDamageTickInterval);
        float safetyEnd = areaDamageMaxDuration > 0f
            ? Time.time + areaDamageMaxDuration
            : float.PositiveInfinity;

        while (areaDamageWindowOpen && Time.time < safetyEnd)
        {
            ApplyAreaDamageTick();

            float waited = 0f;
            while (waited < interval && areaDamageWindowOpen && Time.time < safetyEnd)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        areaDamageWindowOpen = false;
        areaDamageRoutine = null;
    }

    private void CloseAreaDamageWindow()
    {
        areaDamageWindowOpen = false;

        if (areaDamageRoutine == null)
        {
            return;
        }

        StopCoroutine(areaDamageRoutine);
        areaDamageRoutine = null;
    }

    private bool IsAreaSkillActive()
    {
        return currentSkillIndex == areaDamageSkillIndex && (swingActive || IsAttacking || areaDamageWindowOpen);
    }

    private void ApplyAreaDamageTick()
    {
        float damage = areaDamagePerTick > 0f
            ? areaDamagePerTick
            : GetDamageForSkill(areaDamageSkillIndex);

        if (damage <= 0f)
        {
            return;
        }

        Vector3 center = GetAreaDamageCenter();
        Collider[] hits = Physics.OverlapSphere(center, areaDamageRadius, ~0, QueryTriggerInteraction.Collide);
        areaTickTargets.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            CharacterHealth target = ResolveEnemyHealth(hits[i]);
            if (target == null || !areaTickTargets.Add(target))
            {
                continue;
            }

            target.TakeDamage(damage, triggerHitReaction: true);
        }
    }

    private void ApplyAttackDamage()
    {
        for (int i = 0; i < damagedTargets.Length; i++)
        {
            damagedTargets[i] = null;
        }

        Vector3 center = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * attackForwardOffset + Vector3.up;

        int hitCount = Physics.OverlapSphereNonAlloc(center, attackRadius, attackHits, damageMask, QueryTriggerInteraction.Collide);
        int damagedCount = 0;
        float dmg = GetActiveDamage();

        for (int i = 0; i < hitCount; i++)
        {
            CharacterHealth targetHealth = ResolveEnemyHealth(attackHits[i]);
            if (targetHealth == null || HasAlreadyDamaged(targetHealth, damagedCount))
            {
                continue;
            }

            targetHealth.TakeDamage(dmg);
            ApplyKnockback(targetHealth);
            damagedTargets[damagedCount] = targetHealth;
            damagedCount++;
        }
    }

    private Vector3 GetAreaDamageCenter()
    {
        if (areaDamageOrigin != null)
        {
            return areaDamageOrigin.position;
        }

        return transform.position + Vector3.up * areaDamageHeightOffset;
    }

    private CharacterHealth ResolveEnemyHealth(Collider hitCollider)
    {
        if (hitCollider == null || IsSelfCollider(hitCollider))
        {
            return null;
        }

        CharacterHealth targetHealth = hitCollider.GetComponent<CharacterHealth>();
        if (targetHealth == null)
        {
            targetHealth = hitCollider.GetComponentInParent<CharacterHealth>();
        }

        if (targetHealth == null)
        {
            targetHealth = hitCollider.GetComponentInChildren<CharacterHealth>();
        }

        if (targetHealth == null || targetHealth == characterHealth || targetHealth.IsDead)
        {
            return null;
        }

        if (targetHealth.CompareTag("Player"))
        {
            return null;
        }

        return targetHealth;
    }

    private bool IsSelfCollider(Collider hitCollider)
    {
        Transform hitTransform = hitCollider.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    private float GetSkillCooldown(int skillIndex)
    {
        if (skillBindings == null || skillIndex < 0 || skillIndex >= skillBindings.Length) return 0f;
        SkillData skill = skillBindings[skillIndex];
        return skill != null ? skill.cooldown : 0f;
    }

    private float GetActiveDamage()
    {
        return GetDamageForSkill(currentSkillIndex);
    }

    private float GetDamageForSkill(int skillIndex)
    {
        if (perSkillDamage != null && skillIndex >= 0 && skillIndex < perSkillDamage.Length)
        {
            float perSkill = perSkillDamage[skillIndex];
            if (perSkill > 0f)
            {
                return perSkill;
            }
        }

        float damage = characterHealth != null && characterHealth.RuntimeStats != null
            ? Mathf.Max(0.1f, characterHealth.RuntimeStats.attack)
            : attackDamage;
        if (skillBindings != null && skillIndex >= 0 && skillIndex < skillBindings.Length)
        {
            SkillData skill = skillBindings[skillIndex];
            if (skill != null && skill.damageMultiplier > 0f)
            {
                damage *= skill.damageMultiplier;
            }
        }

        return damage;
    }

    private void ApplyKnockback(CharacterHealth targetHealth)
    {
        CharacterKnockback knockback = targetHealth.GetComponent<CharacterKnockback>();
        if (knockback == null)
        {
            knockback = targetHealth.gameObject.AddComponent<CharacterKnockback>();
        }

        Vector3 direction = targetHealth.transform.position - transform.position;
        knockback.Apply(direction, enemyKnockbackDistance, enemyKnockbackDuration);
    }

    private bool HasAlreadyDamaged(CharacterHealth targetHealth, int damagedCount)
    {
        for (int i = 0; i < damagedCount; i++)
        {
            if (damagedTargets[i] == targetHealth)
            {
                return true;
            }
        }

        return false;
    }

    private void AssignDefaultDamageMask()
    {
        if (damageMask.value != 0)
        {
            return;
        }

        damageMask = ~0;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * attackForwardOffset + Vector3.up;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, attackRadius);

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(GetAreaDamageCenter(), areaDamageRadius);
    }
}
