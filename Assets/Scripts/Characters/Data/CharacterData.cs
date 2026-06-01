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

    [Header("Visual")]
    public GameObject characterPrefab;
    public Sprite portrait;
    public Sprite icon;
    public SkinData defaultSkin;

    [Header("Stats")]
    public CharacterBaseStats baseStats = new CharacterBaseStats();

    [Header("Combat")]
    public WeaponType defaultWeaponType = WeaponType.Sword;
    [FormerlySerializedAs("basicAttackSkill")] public SkillData normalAttack;
    public SkillData heavyAttack;
    public SkillData skill1;
    public SkillData skill2;
    [FormerlySerializedAs("ultimateSkill")] public SkillData ultimate;

    [Header("Animation")]
    public RuntimeAnimatorController animatorController;

    [Header("Unlock")]
    public CharacterUnlockType unlockType = CharacterUnlockType.Default;

    [Header("Notes")]
    [TextArea(3, 6)]
    public string description;
}
