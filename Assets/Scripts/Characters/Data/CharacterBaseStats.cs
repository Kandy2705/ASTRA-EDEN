using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class CharacterBaseStats
{
    [FormerlySerializedAs("hp"), Min(1f)] public float maxHP = 100f;
    [FormerlySerializedAs("atk"), Min(0f)] public float attack = 20f;
    [FormerlySerializedAs("def"), Min(0f)] public float defense = 10f;

    [Range(0f, 1f)] public float critRate = 0.05f;
    [Min(0f)] public float critDamage = 0.5f;

    [Min(0f)] public float moveSpeed = 5f;
    [Min(0f)] public float attackSpeed = 1f;

    [Min(0f)] public float staminaMax = 100f;
    [Min(0f)] public float staminaRegen = 12f;

    [Min(0f)] public float energyMax = 100f;
    [Min(0f)] public float energyRegen = 5f;

    [Range(0f, 1f)] public float cooldownReduction = 0f;
    [Min(0f)] public float companionSynergy = 1f;
    [Range(0f, 1f)] public float statusResistance = 0f;
}
