using UnityEngine;

[CreateAssetMenu(fileName = "SO_Weapon_", menuName = "ASTRA EDEN/Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponId;
    public string displayName;
    public WeaponType weaponType;
    public Sprite icon;
    public GameObject prefab;

    [Header("Damage Modifiers (0.10 = +10%)")]
    [Min(0f)] public float basicAttackDamageBonusPercent;
    [Min(0f)] public float skillDamageBonusPercent;

    [Header("Upgrade Scaling")]
    [Tooltip("Giá trị curve được cộng vào basic attack modifier theo Weapon Upgrade Level.")]
    public AnimationCurve basicAttackBonusByUpgrade = AnimationCurve.Linear(0f, 0f, 10f, 0f);
    [Tooltip("Giá trị curve được cộng vào skill modifier theo Weapon Upgrade Level.")]
    public AnimationCurve skillDamageBonusByUpgrade = AnimationCurve.Linear(0f, 0f, 10f, 0f);

    [Header("Optional Combat Stats")]
    [Min(0f)] public float attackSpeedBonus;
    [Range(0f, 1f)] public float critRateBonus;
    [Min(0f)] public float critDamageBonus;

    [Header("Attachment")]
    public WeaponSocket socket = WeaponSocket.RightHand;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale = Vector3.one;
    [Tooltip("Dùng weapon đã có sẵn trong Hero prefab thay vì tạo thêm một bản sao.")]
    public bool useBuiltInVisual;

    public float GetBasicAttackBonusPercent(int upgradeLevel)
    {
        return Mathf.Max(0f, basicAttackDamageBonusPercent + Evaluate(basicAttackBonusByUpgrade, upgradeLevel));
    }

    public float GetSkillDamageBonusPercent(int upgradeLevel)
    {
        return Mathf.Max(0f, skillDamageBonusPercent + Evaluate(skillDamageBonusByUpgrade, upgradeLevel));
    }

    private static float Evaluate(AnimationCurve curve, int level)
    {
        return curve == null || curve.length == 0 ? 0f : curve.Evaluate(Mathf.Max(0, level));
    }
}

public enum WeaponSocket
{
    RightHand,
    LeftHand,
    Back
}
