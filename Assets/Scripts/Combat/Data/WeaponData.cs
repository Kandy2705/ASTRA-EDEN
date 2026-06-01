using UnityEngine;

[CreateAssetMenu(fileName = "SO_Weapon_", menuName = "ASTRA EDEN/Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponId;
    public string displayName;
    public WeaponType weaponType;
    public Sprite icon;
    public GameObject prefab;

    [Header("Stats")]
    [Min(0f)] public float attackBonus;
    [Min(0f)] public float attackSpeedBonus;
    [Range(0f, 1f)] public float critRateBonus;
    [Min(0f)] public float critDamageBonus;
}
