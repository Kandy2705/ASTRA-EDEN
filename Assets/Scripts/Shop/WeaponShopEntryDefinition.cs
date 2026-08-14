using UnityEngine;

[CreateAssetMenu(fileName = "SO_WeaponShopEntry_", menuName = "ASTRA EDEN/Shop/Weapon Entry")]
public sealed class WeaponShopEntryDefinition : ScriptableObject
{
    [SerializeField] private WeaponData weapon;
    [SerializeField, Min(0)] private int goldPrice;
    [SerializeField] private bool isAvailableInStore = true;

    public WeaponData Weapon => weapon;
    public int GoldPrice => Mathf.Max(0, goldPrice);
    public bool IsAvailableInStore => isAvailableInStore && weapon != null && !string.IsNullOrWhiteSpace(weapon.weaponId);
}
