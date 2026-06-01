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
    [Min(0f)] public float damageMultiplier = 1f;
    [Min(0f)] public float poiseDamage;

    [Header("Presentation")]
    public string animationTrigger;
    public GameObject vfxPrefab;
    public AudioClip sfx;

    [Header("Status")]
    public StatusEffectData statusEffect;
}
