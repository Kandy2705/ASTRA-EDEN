using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_StoreCatalog", menuName = "ASTRA EDEN/Shop/Store Catalog")]
public sealed class StoreCatalogData : ScriptableObject
{
    [SerializeField] private List<CharacterShopEntryDefinition> characters = new List<CharacterShopEntryDefinition>();
    [SerializeField] private List<WeaponShopEntryDefinition> weapons = new List<WeaponShopEntryDefinition>();

    public IReadOnlyList<CharacterShopEntryDefinition> Characters => characters;
    public IReadOnlyList<WeaponShopEntryDefinition> Weapons => weapons;
}

public enum StoreContentType
{
    Character,
    Weapon
}

public enum StoreTab
{
    Featured,
    Character,
    Weapon
}
