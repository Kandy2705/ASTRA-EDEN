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
    [SerializeField] private float[] skillLockDurations = new float[4] { 0.75f, 1f, 1f, 1.2f };

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

    private float attackLockTimer;
    private float attackMoveRemaining;
    private float swingElapsed;
    private bool swingActive;
    private bool swingHitResolved;
    private readonly Collider[] attackHits = new Collider[16];
    private readonly CharacterHealth[] damagedTargets = new CharacterHealth[16];

    public bool IsAttacking => attackLockTimer > 0f;
    public bool IsAttackMoveActive => attackMoveRemaining > 0f;
    public float AttackMoveSpeed => attack2MoveDistance / Mathf.Max(attack2MoveDuration, 0.001f);
    public float AttackDamage => attackDamage;
    private int currentSkillIndex;

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

        AssignDefaultEnemyLayer();
    }

    private void Update()
    {
        TickAttackLock();

        if (inputReader == null || animatorBridge == null)
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

        if (!useAnimationEventDamage)
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

    /// <summary>Gọi từ Animation Event ở frame impact của clip attack.</summary>
    public void OnAttackHit()
    {
        if (!swingActive || swingHitResolved)
        {
            return;
        }
        swingHitResolved = true;
        ApplyAttackDamage();
    }

    /// <summary>Optional: gọi cuối clip để chắc chắn reset state.</summary>
    public void OnAttackEnd()
    {
        swingActive = false;
        swingHitResolved = false;
        swingElapsed = 0f;
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

        if (swingActive)
        {
            swingElapsed += Time.deltaTime;

            if (useAnimationEventDamage && !swingHitResolved && damageEventFallback > 0f && swingElapsed >= damageEventFallback)
            {
                OnAttackHit();
            }

            if (attackLockTimer <= 0f)
            {
                OnAttackEnd();
            }
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
            CharacterHealth targetHealth = attackHits[i].GetComponentInParent<CharacterHealth>();
            if (targetHealth == null || targetHealth == characterHealth || HasAlreadyDamaged(targetHealth, damagedCount))
            {
                continue;
            }

            targetHealth.TakeDamage(dmg);
            ApplyKnockback(targetHealth);
            damagedTargets[damagedCount] = targetHealth;
            damagedCount++;
        }
    }

    private float GetSkillCooldown(int skillIndex)
    {
        if (skillBindings == null || skillIndex < 0 || skillIndex >= skillBindings.Length) return 0f;
        SkillData skill = skillBindings[skillIndex];
        return skill != null ? skill.cooldown : 0f;
    }

    private float GetActiveDamage()
    {
        if (perSkillDamage != null && currentSkillIndex >= 0 && currentSkillIndex < perSkillDamage.Length)
        {
            float perSkill = perSkillDamage[currentSkillIndex];
            if (perSkill > 0f) return perSkill;
        }
        return attackDamage;
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

    private void AssignDefaultEnemyLayer()
    {
        if (damageMask.value != 0)
        {
            return;
        }

        int defaultEnemyLayer = LayerMask.GetMask("Enemy");
        if (defaultEnemyLayer != 0)
        {
            damageMask = defaultEnemyLayer;
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
    }
}
