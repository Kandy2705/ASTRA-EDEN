using UnityEngine;

public enum HeroStatType
{
    Health,
    Damage,
    Defense,
    MoveSpeed,
    Mana
}

public enum HeroType
{
    Infantry,
    Ranged,
    Riders,
    Tank,
    Master
}

[CreateAssetMenu(fileName = "SO_Hero_", menuName = "ASTRA EDEN/Heroes/Hero Definition")]
public sealed class HeroDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string heroId;
    [SerializeField] private string displayName;
    [SerializeField] private HeroType heroType = HeroType.Infantry;
    [SerializeField] private CharacterRarity rarity = CharacterRarity.FiveStar;
    [SerializeField, TextArea(2, 5)] private string description;

    [Header("Visuals")]
    [SerializeField] private Sprite portrait;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject modelPrefab;

    [Header("Base Stats")]
    [SerializeField, Min(1f)] private float baseHealth = 1000f;
    [SerializeField, Min(0f)] private float baseDamage = 20f;
    [SerializeField, Min(0f)] private float baseDefense = 10f;
    [SerializeField, Min(0f)] private float baseMoveSpeed = 6f;
    [SerializeField, Min(0f)] private float baseMana = 100f;

    [Header("Amount Per Upgrade")]
    [SerializeField, Min(0f)] private float healthUpgradeAmount = 100f;
    [SerializeField, Min(0f)] private float damageUpgradeAmount = 10f;
    [SerializeField, Min(0f)] private float defenseUpgradeAmount = 5f;
    [SerializeField, Min(0f)] private float moveSpeedUpgradeAmount = 0.2f;
    [SerializeField, Min(0f)] private float manaUpgradeAmount = 20f;

    [Header("UI Display Maximums (visual only)")]
    [SerializeField, Min(1f)] private float healthDisplayMaximum = 5000f;
    [SerializeField, Min(1f)] private float damageDisplayMaximum = 500f;
    [SerializeField, Min(1f)] private float defenseDisplayMaximum = 500f;
    [SerializeField, Min(1f)] private float moveSpeedDisplayMaximum = 15f;
    [SerializeField, Min(1f)] private float manaDisplayMaximum = 1000f;

    public string HeroId => heroId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public HeroType HeroType => heroType;
    public CharacterRarity Rarity => rarity;
    public string Description => description;
    public Sprite Portrait => portrait;
    public Sprite Icon => icon != null ? icon : portrait;
    public GameObject ModelPrefab => modelPrefab;

    public float GetBaseStat(HeroStatType statType)
    {
        switch (statType)
        {
            case HeroStatType.Health: return baseHealth;
            case HeroStatType.Damage: return baseDamage;
            case HeroStatType.Defense: return baseDefense;
            case HeroStatType.MoveSpeed: return baseMoveSpeed;
            case HeroStatType.Mana: return baseMana;
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
        if (string.IsNullOrWhiteSpace(heroId))
        {
            Debug.LogWarning($"[{name}] HeroDefinition requires a stable Hero ID before it can be saved.", this);
        }
    }
}
