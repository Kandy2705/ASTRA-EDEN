using System;

[Serializable]
public sealed class HeroProgressData
{
    public string heroId;
    public int healthUpgradeLevel;
    public int damageUpgradeLevel;
    public int defenseUpgradeLevel;
    public int moveSpeedUpgradeLevel;
    public int manaUpgradeLevel;

    public HeroProgressData()
    {
    }

    public HeroProgressData(string id)
    {
        heroId = id;
    }

    public int GetUpgradeLevel(HeroStatType statType)
    {
        switch (statType)
        {
            case HeroStatType.Health: return healthUpgradeLevel;
            case HeroStatType.Damage: return damageUpgradeLevel;
            case HeroStatType.Defense: return defenseUpgradeLevel;
            case HeroStatType.MoveSpeed: return moveSpeedUpgradeLevel;
            case HeroStatType.Mana: return manaUpgradeLevel;
            default: return 0;
        }
    }

    public int IncrementUpgradeLevel(HeroStatType statType)
    {
        switch (statType)
        {
            case HeroStatType.Health: return ++healthUpgradeLevel;
            case HeroStatType.Damage: return ++damageUpgradeLevel;
            case HeroStatType.Defense: return ++defenseUpgradeLevel;
            case HeroStatType.MoveSpeed: return ++moveSpeedUpgradeLevel;
            case HeroStatType.Mana: return ++manaUpgradeLevel;
            default: return 0;
        }
    }

    public void Sanitize()
    {
        healthUpgradeLevel = Math.Max(0, healthUpgradeLevel);
        damageUpgradeLevel = Math.Max(0, damageUpgradeLevel);
        defenseUpgradeLevel = Math.Max(0, defenseUpgradeLevel);
        moveSpeedUpgradeLevel = Math.Max(0, moveSpeedUpgradeLevel);
        manaUpgradeLevel = Math.Max(0, manaUpgradeLevel);
    }
}
