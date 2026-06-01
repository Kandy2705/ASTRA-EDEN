using UnityEngine;

[CreateAssetMenu(fileName = "SO_Equipment_", menuName = "ASTRA EDEN/Combat/Equipment Data")]
public class EquipmentData : ScriptableObject
{
    public string equipmentId;
    public string displayName;
    public EquipmentSlot slot;
    public Sprite icon;

    [Header("Stats")]
    [Min(0f)] public float maxHPBonus;
    [Min(0f)] public float attackBonus;
    [Min(0f)] public float defenseBonus;
    [Min(0f)] public float staminaBonus;
    [Min(0f)] public float energyBonus;
    [Range(0f, 1f)] public float cooldownReductionBonus;
}
