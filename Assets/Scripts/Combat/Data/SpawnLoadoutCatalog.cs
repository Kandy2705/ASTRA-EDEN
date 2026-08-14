using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_SpawnLoadoutCatalog", menuName = "ASTRA EDEN/Combat/Spawn Loadout Catalog")]
public sealed class SpawnLoadoutCatalog : ScriptableObject
{
    [SerializeField] private List<CharacterData> heroes = new List<CharacterData>();
    [SerializeField] private List<WeaponData> weapons = new List<WeaponData>();
    [SerializeField] private HeroWeaponCompatibilityConfig compatibility;

    public IReadOnlyList<CharacterData> Heroes => heroes;
    public IReadOnlyList<WeaponData> Weapons => weapons;

    public CharacterData ResolveHero(string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return null;
        return heroes.Find(hero => hero != null && string.Equals(hero.HeroId, heroId, StringComparison.Ordinal));
    }

    public WeaponData ResolveWeapon(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId)) return null;
        return weapons.Find(weapon => weapon != null && string.Equals(weapon.weaponId, weaponId, StringComparison.Ordinal));
    }

    public bool CanEquip(CharacterData hero, WeaponData weapon)
    {
        return compatibility != null && compatibility.CanEquip(hero, weapon);
    }

    public WeaponData ResolveValidWeapon(CharacterData hero, string candidateWeaponId, GameDataManager data)
    {
        WeaponData candidate = ResolveWeapon(candidateWeaponId);
        if (IsAvailableForHero(hero, candidate, data)) return candidate;

        WeaponData fallback = ResolveWeapon(hero != null ? hero.DefaultWeaponId : null);
        return CanEquip(hero, fallback) ? fallback : null;
    }

    public bool IsAvailableForHero(CharacterData hero, WeaponData weapon, GameDataManager data)
    {
        if (!CanEquip(hero, weapon)) return false;
        bool isDefault = hero != null && string.Equals(hero.DefaultWeaponId, weapon.weaponId, StringComparison.Ordinal);
        return isDefault || (data != null && data.IsWeaponOwned(weapon.weaponId));
    }
}
