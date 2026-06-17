#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CharacterDataSeeder
{
    const string SkillFolder = "Assets/_Project/ScriptableObjects/Skills";
    const string CharacterFolder = "Assets/_Project/ScriptableObjects/Characters";

    struct SkillRow
    {
        public string id;
        public string displayName;
        public SkillType type;
        public float cooldown;
        public float staminaCost;
        public float energyCost;
        public float energyGain;
        public float damageMultiplier;
        public float poiseDamage;
        public DamageElement element;
        public string description;
    }

    struct CharacterRow
    {
        public string id;
        public string displayName;
        public CharacterClass cls;
        public CharacterGender gender;
        public CharacterRarity rarity;
        public WeaponType weapon;
        public float hp, atk, def;
        public float critRate, critDmg;
        public float moveSpeed, attackSpeed;
        public float staminaMax, staminaRegen;
        public float energyMax, energyRegen;
        public float companionSynergy;
        public string normalId, heavyId, skill1Id, skill2Id, ultId;
    }

    static readonly SkillRow[] Skills = new[]
    {
        // SwordFighter — Seeker
        Skill("skill_core_slash",        "Core Slash",        SkillType.Skill1,    6,  10, 0, 12, 2.2f, 20, DamageElement.Core,     "Chém ngang tạo sóng năng lượng ngắn"),
        Skill("skill_rift_step",         "Rift Step",         SkillType.Skill2,   11,  18, 0, 15, 3.0f, 28, DamageElement.Core,     "Lướt ngắn rồi chém"),
        Skill("ult_astra_breaker",       "Astra Breaker",     SkillType.Ultimate,  0,   0,100,  0, 6.0f, 60, DamageElement.Core,     "Chém dọc xuống đất tạo nứt năng lượng"),
        // SwordFighter — Auren Vale
        Skill("skill_arc_cutter",        "Arc Cutter",        SkillType.Skill1,    7,  12, 0, 12, 2.5f, 24, DamageElement.Physical, "Chém vòng cung rộng"),
        Skill("skill_pulse_guard",       "Pulse Guard",       SkillType.Skill2,   12,  15, 0, 10, 2.0f, 35, DamageElement.Core,     "Stance phản đòn nếu timing chuẩn"),
        Skill("ult_eden_splitter",       "Eden Splitter",     SkillType.Ultimate,  0,   0,100,  0, 6.5f, 70, DamageElement.Core,     "Lao tới combo chém và nổ core"),
        // Lancer
        Skill("skill_piercing_thrust",   "Piercing Thrust",   SkillType.Skill1,    7,  12, 0, 12, 2.6f, 32, DamageElement.Physical, "Đâm xuyên thẳng, tốt với dino lớn"),
        Skill("skill_anchor_break",      "Anchor Break",      SkillType.Skill2,   13,  18, 0, 15, 3.5f, 45, DamageElement.Physical, "Lao tới ghim xuống đất gây stagger cao"),
        Skill("ult_alpha_huntline",      "Alpha Huntline",    SkillType.Ultimate,  0,   0,100,  0, 6.8f, 75, DamageElement.Physical, "Chuỗi đâm tốc độ cao vào weak point"),
        // Mage
        Skill("skill_crystal_burst",     "Crystal Burst",     SkillType.Skill1,    7,   8, 0, 14, 2.7f, 18, DamageElement.Crystal,  "Nổ tinh thể AOE nhỏ"),
        Skill("skill_prism_field",       "Prism Field",       SkillType.Skill2,   14,  12, 0, 16, 3.2f, 25, DamageElement.Crystal,  "Tạo vùng khống chế tinh thể"),
        Skill("ult_archive_nova",        "Archive Nova",      SkillType.Ultimate,  0,   0,100,  0, 7.0f, 60, DamageElement.Crystal,  "Vụ nổ core archive diện rộng"),
        // Gunner
        Skill("skill_scatter_shot",      "Scatter Shot",      SkillType.Skill1,    6,   8, 0, 12, 2.4f, 20, DamageElement.Physical, "Bắn nhiều viên tầm trung"),
        Skill("skill_shock_mine",        "Shock Mine",        SkillType.Skill2,   13,  12, 0, 12, 3.0f, 35, DamageElement.Shock,    "Đặt mìn shock gây stagger"),
        Skill("ult_full_burst_chamber",  "Full Burst Chamber",SkillType.Ultimate,  0,   0,100,  0, 6.4f, 65, DamageElement.Physical, "Bắn liên hoàn burst damage"),
        // Archer
        Skill("skill_tracking_volley",   "Tracking Volley",   SkillType.Skill1,    7,  10, 0, 12, 2.5f, 20, DamageElement.Physical, "Bắn loạt tên bám mục tiêu"),
        Skill("skill_snare_arrow",       "Snare Arrow",       SkillType.Skill2,   12,  14, 0, 12, 2.8f, 30, DamageElement.Nature,   "Bắn tên trói làm chậm"),
        Skill("ult_skyline_rain",        "Skyline Rain",      SkillType.Ultimate,  0,   0,100,  0, 6.6f, 60, DamageElement.Physical, "Mưa tên diện rộng"),
        // Support
        Skill("skill_purify_pulse",      "Purify Pulse",      SkillType.Skill1,    8,   8, 0, 14, 1.8f, 12, DamageElement.Light,    "Gây sát thương nhẹ và cleanse"),
        Skill("skill_resonance_blessing","Resonance Blessing",SkillType.Skill2,   14,  10, 0, 16, 0.0f,  0, DamageElement.Light,    "Buff/heal nhẹ cho player và companion"),
        Skill("ult_sanctuary_field",     "Sanctuary Field",   SkillType.Ultimate,  0,   0,100,  0, 2.5f, 30, DamageElement.Light,    "Tạo vùng hồi phục và đẩy lùi"),
        // HeavyBlade
        Skill("skill_ground_rend",       "Ground Rend",       SkillType.Skill1,    8,  16, 0, 12, 3.0f, 45, DamageElement.Physical, "Chém mạnh xuống đất"),
        Skill("skill_iron_resolve",      "Iron Resolve",      SkillType.Skill2,   15,  20, 0, 10, 1.5f, 60, DamageElement.Physical, "Tăng chống chịu và poise"),
        Skill("ult_titan_crash",         "Titan Crash",       SkillType.Ultimate,  0,   0,100,  0, 7.2f, 90, DamageElement.Physical, "Đập đại kiếm gây stagger cực cao"),
    };

    static readonly CharacterRow[] Characters = new[]
    {
        Char("seeker_male",   "Seeker Male",   CharacterClass.SwordFighter, CharacterGender.Male,   CharacterRarity.ThreeStar, WeaponType.Sword,
             1000, 100, 70, 0.05f, 0.50f, 6.0f, 1.00f, 100, 12, 100, 8, 1.00f,
             "", "", "skill_core_slash",      "skill_rift_step",   "ult_astra_breaker"),
        Char("seeker_female", "Seeker Female", CharacterClass.SwordFighter, CharacterGender.Female, CharacterRarity.ThreeStar, WeaponType.Sword,
             950, 105, 65, 0.06f, 0.50f, 6.2f, 1.05f, 100, 12, 100, 8, 1.00f,
             "", "", "skill_core_slash",      "skill_rift_step",   "ult_astra_breaker"),
        Char("auren_vale",    "Auren Vale",    CharacterClass.SwordFighter, CharacterGender.Male,   CharacterRarity.FourStar,  WeaponType.Longsword,
             1100, 120, 80, 0.08f, 0.55f, 6.0f, 1.00f, 110, 12, 100, 8, 1.05f,
             "", "", "skill_arc_cutter",      "skill_pulse_guard", "ult_eden_splitter"),
        Char("kaia_thorn",    "Kaia Thorn",    CharacterClass.Lancer,       CharacterGender.Female, CharacterRarity.FourStar,  WeaponType.Spear,
             1050, 130, 70, 0.07f, 0.60f, 6.1f, 0.95f, 115, 11, 100, 8, 1.00f,
             "", "", "skill_piercing_thrust", "skill_anchor_break","ult_alpha_huntline"),
        Char("selis_arca",    "Selis Arca",    CharacterClass.Mage,         CharacterGender.Female, CharacterRarity.FiveStar,  WeaponType.Catalyst,
             850, 150, 45, 0.10f, 0.65f, 5.6f, 0.85f,  80,  9, 120, 11, 1.10f,
             "", "", "skill_crystal_burst",   "skill_prism_field", "ult_archive_nova"),
        Char("rex_calder",    "Rex Calder",    CharacterClass.Gunner,       CharacterGender.Male,   CharacterRarity.FourStar,  WeaponType.Gun,
             900, 135, 55, 0.12f, 0.60f, 6.2f, 1.15f,  90, 10, 100, 9, 0.95f,
             "", "", "skill_scatter_shot",    "skill_shock_mine",  "ult_full_burst_chamber"),
        Char("mira_solen",    "Mira Solen",    CharacterClass.Archer,       CharacterGender.Female, CharacterRarity.FourStar,  WeaponType.Bow,
             900, 125, 55, 0.13f, 0.65f, 6.4f, 1.10f,  95, 11, 100, 9, 1.05f,
             "", "", "skill_tracking_volley", "skill_snare_arrow", "ult_skyline_rain"),
        Char("yuna_eir",      "Yuna Eir",      CharacterClass.Support,      CharacterGender.Female, CharacterRarity.FiveStar,  WeaponType.Staff,
             950,  95, 60, 0.06f, 0.50f, 5.8f, 0.95f,  90, 10, 130, 12, 1.25f,
             "", "", "skill_purify_pulse",    "skill_resonance_blessing", "ult_sanctuary_field"),
        Char("darius_flint",  "Darius Flint",  CharacterClass.HeavyBlade,   CharacterGender.Male,   CharacterRarity.FourStar,  WeaponType.Greatsword,
             1300, 135,100, 0.05f, 0.60f, 5.4f, 0.75f, 120,  9,  90, 7, 0.95f,
             "", "", "skill_ground_rend",     "skill_iron_resolve","ult_titan_crash"),
    };

    [MenuItem("ASTRA EDEN/Seed/Skills (CharacterSet)")]
    public static void SeedSkills()
    {
        EnsureFolder(SkillFolder);
        int created = 0, updated = 0;
        foreach (var row in Skills)
        {
            string path = $"{SkillFolder}/SO_Skill_{ToPascal(row.id)}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            bool isNew = asset == null;
            if (isNew)
            {
                asset = ScriptableObject.CreateInstance<SkillData>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.skillId = row.id;
            asset.displayName = row.displayName;
            asset.skillType = row.type;
            asset.cooldown = row.cooldown;
            asset.staminaCost = row.staminaCost;
            asset.energyCost = row.energyCost;
            asset.energyGain = row.energyGain;
            asset.damageMultiplier = row.damageMultiplier;
            asset.poiseDamage = row.poiseDamage;
            asset.element = row.element;
            asset.description = row.description;
            EditorUtility.SetDirty(asset);
            if (isNew) created++; else updated++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Seeder] Skills — created {created}, updated {updated} (total {Skills.Length}).");
    }

    [MenuItem("ASTRA EDEN/Seed/Characters (Roster)")]
    public static void SeedCharacters()
    {
        SeedSkills();
        EnsureFolder(CharacterFolder);

        var skillLookup = new Dictionary<string, SkillData>();
        var skillGuids = AssetDatabase.FindAssets("t:SkillData", new[] { SkillFolder });
        foreach (var guid in skillGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sd = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if (sd != null && !string.IsNullOrEmpty(sd.skillId)) skillLookup[sd.skillId] = sd;
        }

        int created = 0, updated = 0;
        foreach (var row in Characters)
        {
            string path = $"{CharacterFolder}/SO_Character_{ToPascal(row.id)}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            bool isNew = asset == null;
            if (isNew)
            {
                asset = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.characterId = row.id;
            asset.displayName = row.displayName;
            asset.characterClass = row.cls;
            asset.gender = row.gender;
            asset.rarity = row.rarity;
            asset.defaultWeaponType = row.weapon;

            if (asset.baseStats == null) asset.baseStats = new CharacterBaseStats();
            asset.baseStats.maxHP = row.hp;
            asset.baseStats.attack = row.atk;
            asset.baseStats.defense = row.def;
            asset.baseStats.critRate = row.critRate;
            asset.baseStats.critDamage = row.critDmg;
            asset.baseStats.moveSpeed = row.moveSpeed;
            asset.baseStats.attackSpeed = row.attackSpeed;
            asset.baseStats.staminaMax = row.staminaMax;
            asset.baseStats.staminaRegen = row.staminaRegen;
            asset.baseStats.energyMax = row.energyMax;
            asset.baseStats.energyRegen = row.energyRegen;
            asset.baseStats.companionSynergy = row.companionSynergy;

            asset.skill1 = Lookup(skillLookup, row.skill1Id);
            asset.skill2 = Lookup(skillLookup, row.skill2Id);
            asset.ultimate = Lookup(skillLookup, row.ultId);

            EditorUtility.SetDirty(asset);
            if (isNew) created++; else updated++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Seeder] Characters — created {created}, updated {updated} (total {Characters.Length}).");
    }

    static SkillData Lookup(Dictionary<string, SkillData> map, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        map.TryGetValue(id, out var sd);
        if (sd == null) Debug.LogWarning($"[Seeder] Missing skill id '{id}' — leave reference null.");
        return sd;
    }

    static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        var parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static string ToPascal(string snake)
    {
        if (string.IsNullOrEmpty(snake)) return snake;
        var parts = snake.Split('_');
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1) sb.Append(p.Substring(1));
        }
        return sb.ToString();
    }

    static SkillRow Skill(string id, string n, SkillType t, float cd, float sc, float ec, float eg, float dmg, float poise, DamageElement el, string desc)
        => new SkillRow { id = id, displayName = n, type = t, cooldown = cd, staminaCost = sc, energyCost = ec, energyGain = eg, damageMultiplier = dmg, poiseDamage = poise, element = el, description = desc };

    static CharacterRow Char(string id, string n, CharacterClass cls, CharacterGender g, CharacterRarity r, WeaponType w,
        float hp, float atk, float def, float crit, float critDmg, float ms, float atkSpd,
        float stamMax, float stamReg, float enMax, float enReg, float synergy,
        string normalId, string heavyId, string s1, string s2, string ult)
        => new CharacterRow
        {
            id = id, displayName = n, cls = cls, gender = g, rarity = r, weapon = w,
            hp = hp, atk = atk, def = def, critRate = crit, critDmg = critDmg, moveSpeed = ms, attackSpeed = atkSpd,
            staminaMax = stamMax, staminaRegen = stamReg, energyMax = enMax, energyRegen = enReg, companionSynergy = synergy,
            normalId = normalId, heavyId = heavyId, skill1Id = s1, skill2Id = s2, ultId = ult,
        };
}
#endif
