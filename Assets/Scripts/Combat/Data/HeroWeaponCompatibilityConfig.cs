using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_HeroWeaponCompatibility", menuName = "ASTRA EDEN/Combat/Hero Weapon Compatibility")]
public sealed class HeroWeaponCompatibilityConfig : ScriptableObject
{
    [Serializable]
    private sealed class HeroTypeRule
    {
        public HeroType heroType;
        public List<WeaponType> allowedWeaponTypes = new List<WeaponType>();
    }

    [SerializeField] private List<HeroTypeRule> typeRules = new List<HeroTypeRule>();

    public bool CanEquip(CharacterData hero, WeaponData weapon)
    {
        if (hero == null || weapon == null || string.IsNullOrWhiteSpace(weapon.weaponId))
        {
            return false;
        }

        if (string.Equals(hero.DefaultWeaponId, weapon.weaponId, StringComparison.Ordinal))
        {
            return true;
        }

        if (hero.OverrideTypeWeaponCompatibility)
        {
            return hero.AllowsWeaponTypeOverride(weapon.weaponType);
        }

        for (int i = 0; i < typeRules.Count; i++)
        {
            HeroTypeRule rule = typeRules[i];
            if (rule != null && rule.heroType == hero.HeroType)
            {
                return rule.allowedWeaponTypes != null && rule.allowedWeaponTypes.Contains(weapon.weaponType);
            }
        }

        return false;
    }
}
