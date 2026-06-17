using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_Enemy_", menuName = "ASTRA EDEN/Enemies/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyId;
    public string displayName;
    public EnemyArchetype archetype = EnemyArchetype.Melee;
    public EnemyRank rank = EnemyRank.Normal;
    public EnemyZone zone = EnemyZone.Unknown;

    [Header("Visual")]
    public GameObject enemyPrefab;
    public Sprite portrait;
    public Sprite icon;

    [Header("Stats")]
    public EnemyBaseStats baseStats = new EnemyBaseStats();

    [Header("Perception (AI)")]
    [Tooltip("Tầm nhìn tối đa (m).")]
    [Min(0f)] public float sightRange = 14f;
    [Tooltip("Góc nhìn tổng (độ).")]
    [Range(10f, 360f)] public float sightAngle = 110f;
    [Tooltip("Tầm nghe — bỏ qua FOV nếu player vào trong tầm này.")]
    [Min(0f)] public float hearingRange = 7f;
    [Tooltip("Khoảng cách enemy còn giữ aggro sau khi mất sight.")]
    [Min(0f)] public float aggroKeepRange = 22f;

    [Header("Combat Range")]
    [Tooltip("Tầm engage chính (m).")]
    [Min(0f)] public float attackRange = 2f;
    [Tooltip("Cooldown giữa các đòn tấn công tổng quát (giây).")]
    [Min(0f)] public float attackCooldown = 2f;

    [Header("Attacks")]
    public List<AttackPatternData> attackPatterns = new List<AttackPatternData>();

    [Header("Rewards")]
    [Min(0)] public int expReward = 0;
    [Min(0)] public int goldMin = 0;
    [Min(0)] public int goldMax = 0;
    public LootTableData mainLootTable;

    [Header("Notes")]
    [TextArea(3, 6)] public string description;
}
