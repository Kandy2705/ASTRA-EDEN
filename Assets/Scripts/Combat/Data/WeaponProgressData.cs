using System;

[Serializable]
public sealed class WeaponProgressData
{
    public string weaponId;
    public int upgradeLevel;

    public WeaponProgressData(string id)
    {
        weaponId = id;
        upgradeLevel = 0;
    }

    public void Sanitize()
    {
        upgradeLevel = Math.Max(0, upgradeLevel);
    }
}
