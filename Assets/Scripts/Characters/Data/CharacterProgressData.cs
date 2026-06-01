[System.Serializable]
public class CharacterProgressData
{
    public string characterId;

    public bool unlocked;
    public int level = 1;
    public int ascensionLevel;

    public int normalAttackLevel = 1;
    public int heavyAttackLevel = 1;
    public int skill1Level = 1;
    public int skill2Level = 1;
    public int ultimateLevel = 1;

    public string equippedWeaponId;
    public string[] equippedEquipmentIds = new string[4];

    public string selectedSkinId;
}
