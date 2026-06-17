using UnityEngine;

[CreateAssetMenu(fileName = "SO_Skill_", menuName = "ASTRA EDEN/Combat/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillId;
    public string displayName;
    public SkillType skillType;

    [TextArea(3, 6)]
    public string description;

    [Header("Costs")]
    [Min(0f)] public float cooldown;
    [Min(0f)] public float staminaCost;
    [Min(0f)] public float energyCost;
    [Min(0f)] public float energyGain;

    [Header("Combat")]
    [Min(0f)] public float damageMultiplier = 1f; //hệ số sát thương lên máu địch.
    [Min(0f)] public float poiseDamage; //sát thương làm vỡ poise, nếu địch có poise > 0 thì sẽ không bị choáng mà chỉ bị giật lùi, khi nào poise về 0 thì mới choáng được.
    public DamageElement element = DamageElement.Physical;

    [Header("Presentation")]
    public string animationTrigger;
    public GameObject vfxPrefab;
    public AudioClip sfx;

    [Header("Status")]
    public StatusEffectData statusEffect;
}
