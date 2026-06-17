using UnityEngine;

[CreateAssetMenu(fileName = "SO_AttackPattern_", menuName = "ASTRA EDEN/Enemies/Attack Pattern Data")]
public class AttackPatternData : ScriptableObject
{
    [Header("Identity")]
    public string attackId;
    public string displayName;
    public EnemyArchetype archetype = EnemyArchetype.Melee;
    public EnemyAttackRangeType rangeType = EnemyAttackRangeType.Melee;

    [Header("Range")]
    [Min(0f)] public float minRange = 0f;
    [Min(0f)] public float maxRange = 2f;

    [Header("Timing")]
    [Min(0f)] public float cooldown = 2f;
    [Tooltip("Telegraph trước khi đòn đánh active (giây).")]
    [Min(0f)] public float windup = 0.3f;
    [Tooltip("Thời gian hitbox/projectile active (giây).")]
    [Min(0f)] public float activeTime = 0.2f;
    [Tooltip("Thời gian recovery sau active.")]
    [Min(0f)] public float recovery = 0.5f;

    [Header("Damage")]
    [Min(0f)] public float damageMultiplier = 1f;
    [Min(0f)] public float poiseDamage = 0f;
    public DamageElement element = DamageElement.Physical;

    [Header("Behaviour")]
    public bool canBeInterrupted = true;

    [Header("Notes")]
    [TextArea(2, 4)] public string telegraph;
}
