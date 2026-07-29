#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EnemyDataSeeder
{
    const string AttackFolder = "Assets/_Project/ScriptableObjects/Enemies/AttackPatterns";
    const string EnemyFolder = "Assets/_Project/ScriptableObjects/Enemies/Units";
    const string LootFolder = "Assets/_Project/ScriptableObjects/Enemies/LootTables";

    struct AttackRow
    {
        public string id, name;
        public EnemyArchetype archetype;
        public EnemyAttackRangeType rangeType;
        public float minRange, maxRange;
        public float cooldown, windup, activeTime, recovery;
        public float damageMultiplier, poiseDamage;
        public DamageElement element;
        public bool canInterrupt;
        public string telegraph;
    }

    struct EnemyRow
    {
        public string id, name;
        public EnemyArchetype archetype;
        public EnemyRank rank;
        public EnemyZone zone;
        public float hp, atk, def, poise;
        public float moveSpeed, turnSpeed;
        public float sightRange, sightAngle, hearingRange, aggroKeepRange;
        public float attackRange, attackCooldown;
        public int expReward, goldMin, goldMax;
        public string lootTableId;
        public string[] attackIds;
    }

    static readonly AttackRow[] Attacks = new[]
    {
        Atk("atk_raptor_bite",         "Bite",            EnemyArchetype.Melee,         EnemyAttackRangeType.Melee,         0.0f, 2.0f, 1.8f, 0.25f, 0.18f, 0.45f, 1.0f, 18, DamageElement.Physical, true,  "Raptor hạ đầu trước khi cắn"),
        Atk("atk_raptor_claw_combo",   "Claw Combo",      EnemyArchetype.Melee,         EnemyAttackRangeType.Melee,         0.0f, 2.2f, 2.6f, 0.30f, 0.35f, 0.55f, 1.4f, 28, DamageElement.Physical, true,  "Giơ móng và xoay thân"),
        Atk("atk_raptor_leap_bite",    "Leap Bite",       EnemyArchetype.Fast,          EnemyAttackRangeType.Leap,          3.0f, 7.5f, 4.5f, 0.45f, 0.25f, 0.70f, 1.7f, 35, DamageElement.Physical, true,  "Hạ thấp người rồi phóng tới"),
        Atk("atk_spitter_acid_spit",   "Acid Spit",       EnemyArchetype.Ranged,        EnemyAttackRangeType.Projectile,    4.0f,11.0f, 2.8f, 0.55f, 0.10f, 0.55f, 1.2f, 15, DamageElement.Poison,   true,  "Cổ họng phồng và sáng xanh"),
        Atk("atk_spitter_acid_pool",   "Acid Pool",       EnemyArchetype.Ranged,        EnemyAttackRangeType.ProjectileAOE, 5.0f,10.0f, 6.0f, 0.75f, 0.20f, 0.80f, 1.0f, 12, DamageElement.Poison,   true,  "Miệng tụ acid lâu hơn"),
        Atk("atk_velociraptor_poison_orb", "Poison Orb",  EnemyArchetype.Ranged,        EnemyAttackRangeType.Projectile,    4.0f,12.0f, 1.3f, 0.35f, 0.15f, 0.20f, 1.0f, 20, DamageElement.Poison,   true,  "Khóa hướng nhìn rồi phóng một Poison Orb thẳng về phía trước"),
        Atk("atk_tanker_head_slam",    "Head Slam",       EnemyArchetype.Tanker,        EnemyAttackRangeType.MeleeAOE,      0.0f, 2.8f, 2.8f, 0.55f, 0.25f, 0.75f, 1.5f, 45, DamageElement.Physical, false, "Nâng đầu rồi dập xuống"),
        Atk("atk_tanker_body_shove",   "Body Shove",      EnemyArchetype.Tanker,        EnemyAttackRangeType.Melee,         0.0f, 3.0f, 3.6f, 0.45f, 0.30f, 0.80f, 1.7f, 55, DamageElement.Physical, false, "Nghiêng người lấy đà"),
        Atk("atk_caster_crystal_shard","Crystal Shard",   EnemyArchetype.CasterSupport, EnemyAttackRangeType.Projectile,    5.0f,12.0f, 3.2f, 0.60f, 0.10f, 0.60f, 1.3f, 18, DamageElement.Crystal,  true,  "Đầu/cổ phát sáng tím"),
        Atk("atk_caster_buff_roar",    "Pack Buff Roar",  EnemyArchetype.CasterSupport, EnemyAttackRangeType.BuffAOE,       0.0f,10.0f, 8.0f, 0.80f, 0.50f, 1.00f, 0.0f,  0, DamageElement.Crystal,  true,  "Screecher ngửa cổ hú"),
        Atk("atk_elite_charge",        "Charge",          EnemyArchetype.Elite,         EnemyAttackRangeType.Charge,        4.0f,12.0f, 6.0f, 0.75f, 0.60f, 1.00f, 2.0f, 70, DamageElement.Physical, false, "Cào đất / tụ lực"),
        Atk("atk_elite_roar",          "Roar Shock",      EnemyArchetype.Elite,         EnemyAttackRangeType.AOE,           0.0f, 6.0f, 9.0f, 0.65f, 0.30f, 1.00f, 1.2f, 40, DamageElement.Core,     true,  "Gầm lớn tạo vòng sóng"),
    };

    // ATK đã balance lại (~35–40% bản cũ): trước đây raw full, không trừ DEF → 1 pack raptor xé player.
    // Player ~1000 HP / DEF ~70: trash ~10–20 dmg/hit sau DEF, elite/boss cao hơn một chút.
    static readonly EnemyRow[] Enemies = new[]
    {
        Enemy("enemy_wild_claw_raptor",           "Wild Claw Raptor",             EnemyArchetype.Melee,         EnemyRank.Normal, EnemyZone.BeachCrash,
            350,  16, 20,  40, 4.2f, 720, 14, 110,  7, 24, 2.0f, 1.8f,  25,  8, 14, "loot_raptor_basic",      "atk_raptor_bite", "atk_raptor_claw_combo"),
        Enemy("enemy_scavenger_lizard",           "Scavenger Lizard",             EnemyArchetype.Fast,          EnemyRank.Normal, EnemyZone.BeachCrash,
            220,  12, 12,  25, 4.8f, 760, 12, 100,  8, 20, 1.5f, 1.4f,  18,  5, 10, "loot_lizard_basic",      "atk_raptor_bite"),
        Enemy("enemy_young_spitter",              "Young Spitter",                EnemyArchetype.Ranged,        EnemyRank.Normal, EnemyZone.BeachCrash,
            280,  14, 15,  25, 3.2f, 600, 16, 120,  6, 26, 9.0f, 2.8f,  28,  8, 16, "loot_spitter_basic",     "atk_spitter_acid_spit"),
        Enemy("enemy_driftback_boarlizard",       "Driftback Boar-Lizard",        EnemyArchetype.Tanker,        EnemyRank.Normal, EnemyZone.BeachCrash,
            550,  20, 45,  80, 2.8f, 420, 12, 100,  7, 22, 2.4f, 2.4f,  40, 14, 24, "loot_tanker_basic",      "atk_tanker_head_slam", "atk_tanker_body_shove"),
        Enemy("enemy_beach_parasite",             "Beach Parasite",               EnemyArchetype.Debuff,        EnemyRank.Normal, EnemyZone.BeachCrash,
            180,  10,  8,  15, 3.8f, 650, 10, 100,  6, 18, 1.6f, 2.0f,  15,  4,  8, "loot_parasite_basic",    "atk_raptor_bite"),
        Enemy("enemy_fang_raptor",                "Fang Raptor",                  EnemyArchetype.Fast,          EnemyRank.Normal, EnemyZone.PrimevalForest,
            420,  20, 25,  35, 5.4f, 820, 16, 115,  8, 28, 1.8f, 1.5f,  40, 12, 20, "loot_raptor_forest",     "atk_raptor_leap_bite", "atk_raptor_bite"),
        Enemy("enemy_raptor_pack_leader",         "Wild Claw Raptor Pack Leader", EnemyArchetype.Ranged,        EnemyRank.Elite,  EnemyZone.PrimevalForest,
            900,  28, 40,  75, 4.5f, 720, 18, 120, 10, 30,12.0f, 2.0f,  90, 30, 55, "loot_pack_leader",       "atk_velociraptor_poison_orb"),
        Enemy("enemy_armored_herbivore_juvenile", "Armored Herbivore Juvenile",   EnemyArchetype.Tanker,        EnemyRank.Normal, EnemyZone.PrimevalForest,
            800,  22, 60, 110, 2.6f, 380, 12, 100,  7, 24, 2.6f, 2.8f,  60, 20, 36, "loot_armored_herbivore", "atk_tanker_head_slam", "atk_tanker_body_shove"),
        Enemy("enemy_crystal_screecher",          "Crystal Screecher",            EnemyArchetype.CasterSupport, EnemyRank.Normal, EnemyZone.PrimevalForest,
            360,  18, 20,  30, 3.0f, 550, 17, 130,  8, 28,10.0f, 3.5f,  55, 16, 28, "loot_crystal_caster",    "atk_caster_crystal_shard", "atk_caster_buff_roar"),
        Enemy("enemy_poison_mire_spitter",        "Poison Mire Spitter",          EnemyArchetype.Ranged,        EnemyRank.Normal, EnemyZone.PrimevalForest,
            480,  22, 22,  35, 3.1f, 600, 18, 125,  7, 30,10.5f, 2.7f,  58, 18, 32, "loot_spitter_poison",    "atk_spitter_acid_spit", "atk_spitter_acid_pool"),
        Enemy("enemy_corrupted_lab_raptor",       "Corrupted Lab Raptor",         EnemyArchetype.Melee,         EnemyRank.Normal, EnemyZone.RuinedLab,
            620,  26, 35,  55, 4.4f, 720, 15, 115,  8, 28, 2.0f, 1.7f,  70, 22, 40, "loot_corrupted_raptor",  "atk_raptor_claw_combo", "atk_raptor_bite"),
        Enemy("enemy_core_spitter",               "Core Spitter",                 EnemyArchetype.Ranged,        EnemyRank.Normal, EnemyZone.RuinedLab,
            560,  26, 30,  40, 3.0f, 580, 18, 125,  7, 30,11.0f, 2.6f,  72, 24, 42, "loot_core_spitter",      "atk_spitter_acid_spit"),
        Enemy("enemy_security_warden_dronebeast", "Security Warden Drone-Beast",  EnemyArchetype.CasterSupport, EnemyRank.Elite,  EnemyZone.RuinedLab,
            850,  28, 50,  80, 3.4f, 620, 18, 140,  8, 30, 9.0f, 3.2f, 110, 38, 65, "loot_warden_elite",      "atk_caster_crystal_shard", "atk_elite_roar"),
        Enemy("enemy_flux_hound",                 "Flux Hound",                   EnemyArchetype.Fast,          EnemyRank.Normal, EnemyZone.RuinedLab,
            520,  26, 25,  32, 5.8f, 850, 16, 115,  9, 30, 1.7f, 1.3f,  68, 22, 38, "loot_flux_hound",        "atk_raptor_leap_bite", "atk_raptor_bite"),
        Enemy("enemy_core_corrupted_raptor",      "Core Corrupted Raptor",        EnemyArchetype.Melee,         EnemyRank.Normal, EnemyZone.CrystalCore,
            760,  30, 45,  65, 4.6f, 740, 17, 120,  8, 32, 2.0f, 1.6f,  90, 32, 55, "loot_core_corrupted",    "atk_raptor_claw_combo", "atk_raptor_bite"),
        Enemy("enemy_prism_screecher",            "Prism Screecher",              EnemyArchetype.CasterSupport, EnemyRank.Elite,  EnemyZone.CrystalCore,
            950,  32, 45,  70, 3.2f, 600, 20, 145,  9, 34,11.0f, 3.2f, 130, 45, 80, "loot_prism_screecher",   "atk_caster_crystal_shard", "atk_caster_buff_roar", "atk_elite_roar"),
        Enemy("enemy_crystal_shell_brute",        "Crystal Shell Brute",          EnemyArchetype.Tanker,        EnemyRank.Elite,  EnemyZone.CrystalCore,
            1400, 36, 80, 150, 2.5f, 360, 14, 110,  7, 30, 3.0f, 2.8f, 150, 55, 95, "loot_crystal_brute",     "atk_tanker_head_slam", "atk_elite_charge"),
    };

    [MenuItem("ASTRA EDEN/Seed/Attack Patterns")]
    public static void SeedAttacks()
    {
        EnsureFolder(AttackFolder);
        int c = 0, u = 0;
        foreach (var row in Attacks)
        {
            string path = $"{AttackFolder}/SO_AttackPattern_{ToPascal(row.id)}.asset";
            var a = AssetDatabase.LoadAssetAtPath<AttackPatternData>(path);
            bool isNew = a == null;
            if (isNew) { a = ScriptableObject.CreateInstance<AttackPatternData>(); AssetDatabase.CreateAsset(a, path); }
            a.attackId = row.id;
            a.displayName = row.name;
            a.archetype = row.archetype;
            a.rangeType = row.rangeType;
            a.minRange = row.minRange;
            a.maxRange = row.maxRange;
            a.cooldown = row.cooldown;
            a.windup = row.windup;
            a.activeTime = row.activeTime;
            a.recovery = row.recovery;
            a.damageMultiplier = row.damageMultiplier;
            a.poiseDamage = row.poiseDamage;
            a.element = row.element;
            a.canBeInterrupted = row.canInterrupt;
            a.telegraph = row.telegraph;
            EditorUtility.SetDirty(a);
            if (isNew) c++; else u++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Seeder] AttackPatterns — created {c}, updated {u} (total {Attacks.Length}).");
    }

    [MenuItem("ASTRA EDEN/Seed/Enemies (Full)")]
    public static void SeedEnemiesFull()
    {
        SeedAttacks();
        EnsureFolder(EnemyFolder);

        var atkLookup = new Dictionary<string, AttackPatternData>();
        foreach (var guid in AssetDatabase.FindAssets("t:AttackPatternData", new[] { AttackFolder }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var ap = AssetDatabase.LoadAssetAtPath<AttackPatternData>(p);
            if (ap != null && !string.IsNullOrEmpty(ap.attackId)) atkLookup[ap.attackId] = ap;
        }

        // Loot tables: tìm theo asset name (SO_LootTable_<PascalId>) trong LootFolder nếu user đã tạo sẵn.
        var lootLookup = new Dictionary<string, LootTableData>();
        if (AssetDatabase.IsValidFolder(LootFolder))
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LootTableData", new[] { LootFolder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var lt = AssetDatabase.LoadAssetAtPath<LootTableData>(p);
                if (lt == null) continue;
                string assetName = System.IO.Path.GetFileNameWithoutExtension(p);
                lootLookup[assetName] = lt;
            }
        }

        int c = 0, u = 0;
        foreach (var row in Enemies)
        {
            string path = $"{EnemyFolder}/SO_Enemy_{ToPascal(row.id)}.asset";
            var e = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            bool isNew = e == null;
            if (isNew) { e = ScriptableObject.CreateInstance<EnemyData>(); AssetDatabase.CreateAsset(e, path); }
            e.enemyId = row.id;
            e.displayName = row.name;
            e.archetype = row.archetype;
            e.rank = row.rank;
            e.zone = row.zone;

            if (e.baseStats == null) e.baseStats = new EnemyBaseStats();
            e.baseStats.maxHP = row.hp;
            e.baseStats.attack = row.atk;
            e.baseStats.defense = row.def;
            e.baseStats.poise = row.poise;
            e.baseStats.moveSpeed = row.moveSpeed;
            e.baseStats.turnSpeed = row.turnSpeed;

            e.sightRange = row.sightRange;
            e.sightAngle = row.sightAngle;
            e.hearingRange = row.hearingRange;
            e.aggroKeepRange = row.aggroKeepRange;
            e.attackRange = row.attackRange;
            e.attackCooldown = row.attackCooldown;

            e.expReward = row.expReward;
            e.goldMin = row.goldMin;
            e.goldMax = row.goldMax;

            // Link loot bằng asset name (vd SO_LootTable_LootRaptorBasic). Chỉ link nếu đã tồn tại — không phá link cũ nếu user đã set tay.
            string lootAssetName = $"SO_LootTable_{ToPascal(row.lootTableId)}";
            if (lootLookup.TryGetValue(lootAssetName, out var lt)) e.mainLootTable = lt;

            e.attackPatterns = new List<AttackPatternData>();
            foreach (var aid in row.attackIds)
            {
                if (atkLookup.TryGetValue(aid, out var ap)) e.attackPatterns.Add(ap);
                else Debug.LogWarning($"[Seeder] Enemy '{row.id}' missing attack '{aid}'.");
            }

            EditorUtility.SetDirty(e);
            if (isNew) c++; else u++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Seeder] Enemies — created {c}, updated {u} (total {Enemies.Length}). Loot tables sẽ link ở Phase 3 (cần ItemData SO trước).");
    }

    static AttackRow Atk(string id, string n, EnemyArchetype arch, EnemyAttackRangeType rt,
        float minR, float maxR, float cd, float wu, float act, float rec,
        float dmg, float poise, DamageElement el, bool interrupt, string telegraph)
        => new AttackRow
        {
            id = id, name = n, archetype = arch, rangeType = rt,
            minRange = minR, maxRange = maxR, cooldown = cd, windup = wu, activeTime = act, recovery = rec,
            damageMultiplier = dmg, poiseDamage = poise, element = el, canInterrupt = interrupt, telegraph = telegraph,
        };

    static EnemyRow Enemy(string id, string n, EnemyArchetype arch, EnemyRank rank, EnemyZone zone,
        float hp, float atk, float def, float poise, float ms, float ts,
        float sight, float sightAng, float hearing, float aggro,
        float atkRange, float atkCd,
        int exp, int gMin, int gMax, string lootId, params string[] attackIds)
        => new EnemyRow
        {
            id = id, name = n, archetype = arch, rank = rank, zone = zone,
            hp = hp, atk = atk, def = def, poise = poise, moveSpeed = ms, turnSpeed = ts,
            sightRange = sight, sightAngle = sightAng, hearingRange = hearing, aggroKeepRange = aggro,
            attackRange = atkRange, attackCooldown = atkCd,
            expReward = exp, goldMin = gMin, goldMax = gMax,
            lootTableId = lootId, attackIds = attackIds,
        };

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
}
#endif
