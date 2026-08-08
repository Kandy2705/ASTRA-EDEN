using UnityEngine;

public sealed class AncientForestBossBehaviour : EnemyBossBehaviour
{
    [Header("Hybrid Combat")]
    [SerializeField, Min(0.1f)]
    private float fallbackMeleeRange = 5.5f;

    [Header("Tail Whip")]
    [Tooltip("Bone gần đầu đuôi. Để trống sẽ tự tìm Bip01_Tail4/TailNub trong model khi đánh.")]
    [SerializeField] private Transform tailWhipAnchor;

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

    public override void ConfigureAttackHitbox(
        EnemyAttackHitbox hitbox,
        AttackPatternData pattern)
    {
        if (hitbox == null || pattern == null ||
            pattern.attackId != "atk_ancient_forest_tail_whip")
        {
            return;
        }

        if (tailWhipAnchor == null)
        {
            tailWhipAnchor = FindTailWhipAnchor();
        }

        if (tailWhipAnchor != null)
        {
            // TailWhip pattern dùng Sphere offset (0,0,0), vậy damage chỉ xảy ra
            // quanh chính bone đuôi tại frame impact, không còn một Box khổng lồ
            // tĩnh ở root boss.
            hitbox.SetDynamicAnchor(tailWhipAnchor);
        }
        else
        {
            Debug.LogWarning($"[{name}] Không tìm thấy tail bone cho TailWhip; dùng fallback hitbox.", this);
        }
    }

    Transform FindTailWhipAnchor()
    {
        Transform tail3Fallback = null;
        foreach (Transform node in GetComponentsInChildren<Transform>(true))
        {
            if (node.name == "Bip01_Tail4" || node.name == "Bip01_TailNub")
            {
                return node;
            }

            if (node.name == "Bip01_Tail3")
            {
                tail3Fallback = node;
            }
        }

        return tail3Fallback;
    }
}
