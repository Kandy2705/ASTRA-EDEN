using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "SO_Character_", menuName = "ASTRA EDEN/Characters/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    [FormerlySerializedAs("id")] public string characterId;
    [FormerlySerializedAs("characterName")] public string displayName;
    public CharacterClass characterClass = CharacterClass.SwordFighter;
    public CharacterGender gender = CharacterGender.Unknown;
    public CharacterRarity rarity = CharacterRarity.ThreeStar;
    [Min(1)] public int rank = 1;
    public HeroType heroType = HeroType.Infantry;

    [Header("Visual")]
    public GameObject characterPrefab;
    public Sprite portrait;
    public Sprite icon;
    public SkinData defaultSkin;

    [Header("Stats")]
    public CharacterBaseStats baseStats = new CharacterBaseStats();

    [Header("Combat")]
    public WeaponType defaultWeaponType = WeaponType.Sword;
    public string defaultWeaponId;
    public bool overrideTypeWeaponCompatibility;
    public List<WeaponType> allowedWeaponTypes = new List<WeaponType>();
    [FormerlySerializedAs("basicAttackSkill")] public SkillData normalAttack;
    public SkillData heavyAttack;
    public SkillData skill1;
    public SkillData skill2;
    [FormerlySerializedAs("ultimateSkill")] public SkillData ultimate;

    [Header("Animation")]
    public RuntimeAnimatorController animatorController;

    [Header("Unlock")]
    public CharacterUnlockType unlockType = CharacterUnlockType.Default;

    [Header("Character Store")]
    [Min(0)] public int storeGoldPrice;
    public bool isAvailableInStore = true;

    [Header("Hero Stat Upgrade Amounts")]
    [Min(0f)] public float healthUpgradeAmount = 100f;
    [Min(0f)] public float damageUpgradeAmount = 10f;
    [Min(0f)] public float defenseUpgradeAmount = 5f;
    [Min(0f)] public float moveSpeedUpgradeAmount = 0.2f;
    [Min(0f)] public float manaUpgradeAmount = 20f;

    [Header("Hero Stat Display Maximums (visual only)")]
    [Min(1f)] public float healthDisplayMaximum = 5000f;
    [Min(1f)] public float damageDisplayMaximum = 500f;
    [Min(1f)] public float defenseDisplayMaximum = 500f;
    [Min(1f)] public float moveSpeedDisplayMaximum = 15f;
    [Min(1f)] public float manaDisplayMaximum = 1000f;

    [Header("Notes")]
    [TextArea(3, 6)]
    public string description;

    public string HeroId => characterId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public HeroType HeroType => heroType;
    public CharacterRarity Rarity => rarity;
    public string Description => description;
    public Sprite Portrait => portrait;
    public Sprite Icon => icon != null ? icon : portrait;
    public GameObject ModelPrefab => characterPrefab;
    public GameObject GameplayPrefab => characterPrefab;
    public string DefaultWeaponId => defaultWeaponId;
    public int StoreGoldPrice => Mathf.Max(0, storeGoldPrice);
    public bool IsAvailableInStore => isAvailableInStore && !string.IsNullOrWhiteSpace(HeroId);
    public bool IsOwned => GameDataManager.Instance != null && GameDataManager.Instance.IsHeroOwned(HeroId);
    public bool OverrideTypeWeaponCompatibility => overrideTypeWeaponCompatibility;
    public IReadOnlyList<WeaponType> AllowedWeaponTypes => allowedWeaponTypes;

    public bool AllowsWeaponTypeOverride(WeaponType weaponType)
    {
        return allowedWeaponTypes != null && allowedWeaponTypes.Contains(weaponType);
    }

    public float GetBaseStat(HeroStatType statType)
    {
        CharacterBaseStats stats = baseStats ?? new CharacterBaseStats();
        switch (statType)
        {
            case HeroStatType.Health: return stats.maxHP;
            case HeroStatType.Damage: return stats.attack;
            case HeroStatType.Defense: return stats.defense;
            case HeroStatType.MoveSpeed: return stats.moveSpeed;
            case HeroStatType.Mana: return stats.energyMax;
            default: return 0f;
        }
    }

    public float GetUpgradeAmount(HeroStatType statType)
    {
        switch (statType)
        {
            case HeroStatType.Health: return healthUpgradeAmount;
            case HeroStatType.Damage: return damageUpgradeAmount;
            case HeroStatType.Defense: return defenseUpgradeAmount;
            case HeroStatType.MoveSpeed: return moveSpeedUpgradeAmount;
            case HeroStatType.Mana: return manaUpgradeAmount;
            default: return 0f;
        }
    }

    public float GetDisplayMaximum(HeroStatType statType)
    {
        switch (statType)
        {
            case HeroStatType.Health: return healthDisplayMaximum;
            case HeroStatType.Damage: return damageDisplayMaximum;
            case HeroStatType.Defense: return defenseDisplayMaximum;
            case HeroStatType.MoveSpeed: return moveSpeedDisplayMaximum;
            case HeroStatType.Mana: return manaDisplayMaximum;
            default: return 1f;
        }
    }

    public float CalculateFinalStat(HeroStatType statType, int upgradeLevel)
    {
        return GetBaseStat(statType) + Mathf.Max(0, upgradeLevel) * GetUpgradeAmount(statType);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            Debug.LogWarning($"[{name}] CharacterData requires a stable Character ID before it can be saved.", this);
        }
    }
}
