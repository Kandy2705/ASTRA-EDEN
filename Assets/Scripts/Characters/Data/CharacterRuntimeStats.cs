using UnityEngine;

[System.Serializable]
public class CharacterRuntimeStats
{
    [Min(1f)] public float maxHP;
    [Min(0f)] public float currentHP;

    [Min(0f)] public float attack;
    [Min(0f)] public float defense;

    [Range(0f, 1f)] public float critRate;
    [Min(0f)] public float critDamage;

    [Min(0f)] public float moveSpeed;
    [Min(0f)] public float attackSpeed;

    [Min(0f)] public float staminaMax;
    [Min(0f)] public float currentStamina;
    [Min(0f)] public float staminaRegen;

    [Min(0f)] public float energyMax;
    [Min(0f)] public float currentEnergy;
    [Min(0f)] public float energyRegen;

    [Range(0f, 1f)] public float cooldownReduction;
    [Min(0f)] public float companionSynergy;
    [Range(0f, 1f)] public float statusResistance;

    public static CharacterRuntimeStats FromBaseStats(CharacterBaseStats baseStats)
    {
        if (baseStats == null)
        {
            baseStats = new CharacterBaseStats();
        }

        CharacterRuntimeStats stats = new CharacterRuntimeStats();
        stats.maxHP = baseStats.maxHP;
        stats.currentHP = stats.maxHP;
        stats.attack = baseStats.attack;
        stats.defense = baseStats.defense;
        stats.critRate = baseStats.critRate;
        stats.critDamage = baseStats.critDamage;
        stats.moveSpeed = baseStats.moveSpeed;
        stats.attackSpeed = baseStats.attackSpeed;
        stats.staminaMax = baseStats.staminaMax;
        stats.currentStamina = stats.staminaMax;
        stats.staminaRegen = baseStats.staminaRegen;
        stats.energyMax = baseStats.energyMax;
        stats.currentEnergy = stats.energyMax;
        stats.energyRegen = baseStats.energyRegen;
        stats.cooldownReduction = baseStats.cooldownReduction;
        stats.companionSynergy = baseStats.companionSynergy;
        stats.statusResistance = baseStats.statusResistance;
        return stats;
    }
}
