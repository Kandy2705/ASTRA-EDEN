using UnityEngine;

public sealed class AncientForestBossBehaviour : EnemyBossBehaviour
{
    [Header("Hybrid Combat")]
    [SerializeField, Min(0.1f)]
    private float fallbackMeleeRange = 5.5f;

    public override float GetEffectiveAttackRange(EnemyData data)
    {
        float meleeRange = Mathf.Max(0.1f, fallbackMeleeRange);

        if (data == null || data.attackPatterns == null)
        {
            return meleeRange;
        }

        foreach (AttackPatternData pattern in data.attackPatterns)
        {
            if (pattern == null || IsProjectile(pattern))
            {
                continue;
            }

            meleeRange = Mathf.Max(meleeRange, pattern.maxRange);
        }

        return meleeRange;
    }

    public override bool CanStartSpecialAttack(
        EnemyData data,
        float distance,
        float cooldownRemaining)
    {
        if (cooldownRemaining > 0f ||
            data == null ||
            data.attackPatterns == null)
        {
            return false;
        }

        foreach (AttackPatternData pattern in data.attackPatterns)
        {
            if (!IsProjectile(pattern))
            {
                continue;
            }

            if (distance >= pattern.minRange &&
                distance <= pattern.maxRange)
            {
                return true;
            }
        }

        return false;
    }
}
