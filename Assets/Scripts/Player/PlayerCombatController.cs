using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterHealth))]
public class PlayerCombatController : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerAnimatorBridge animatorBridge;
    [SerializeField] private float attackLockDuration = 0.75f;

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

    private float attackLockTimer;
    private readonly Collider[] attackHits = new Collider[16];
    private readonly CharacterHealth[] damagedTargets = new CharacterHealth[16];

    public bool IsAttacking => attackLockTimer > 0f;
    public float AttackDamage => attackDamage;

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

        AssignDefaultEnemyLayer();
    }

    private void Update()
    {
        TickAttackLock();

        if (inputReader == null || animatorBridge == null)
        {
            return;
        }

        if (!IsAttacking && inputReader.AttackPressed)
        {
            StartAttack();
        }
    }

    private void StartAttack()
    {
        attackLockTimer = attackLockDuration;
        animatorBridge.TriggerAttack();
        ApplyAttackDamage();
    }

    private void TickAttackLock()
    {
        if (attackLockTimer > 0f)
        {
            attackLockTimer -= Time.deltaTime;
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
        for (int i = 0; i < hitCount; i++)
        {
            CharacterHealth targetHealth = attackHits[i].GetComponentInParent<CharacterHealth>();
            if (targetHealth == null || targetHealth == characterHealth || HasAlreadyDamaged(targetHealth, damagedCount))
            {
                continue;
            }

            targetHealth.TakeDamage(attackDamage);
            ApplyKnockback(targetHealth);
            damagedTargets[damagedCount] = targetHealth;
            damagedCount++;
        }
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
