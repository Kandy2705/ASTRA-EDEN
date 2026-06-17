#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class LootDataSeeder
{
    const string ItemFolder = "Assets/_Project/ScriptableObjects/Items/Loot";
    const string LootFolder = "Assets/_Project/ScriptableObjects/Enemies/LootTables";
    const string EnemyFolder = "Assets/_Project/ScriptableObjects/Enemies/Units";

    struct ItemRow
    {
        public string id, name;
        public ItemType type;
        public ItemRarity rarity;
        public bool stackable;
        public int maxStack;
        public string description;
    }

    struct LootRow
    {
        public string tableId, enemyLabel;
        public int gold;
        // (itemId, dropChance, minQty, maxQty)
        public (string id, float chance, int minQ, int maxQ)[] entries;
    }

    static readonly ItemRow[] Items = new[]
    {
        Item("item_raptor_claw",           "Raptor Claw",           ItemType.Material,     ItemRarity.Common,   true, 999, "Móng vuốt raptor — vật liệu chế tạo cơ bản."),
        Item("item_dino_scale",            "Dino Scale",            ItemType.Material,     ItemRarity.Common,   true, 999, "Vảy dino — vật liệu chế tạo phòng thủ."),
        Item("item_small_claw",            "Small Claw",            ItemType.Material,     ItemRarity.Common,   true, 999, "Móng vuốt nhỏ — vật liệu chế tạo."),
        Item("item_venom_gland",           "Venom Gland",           ItemType.Material,     ItemRarity.Common,   true, 999, "Túi nọc độc — vật liệu chế đồ poison."),
        Item("item_acid_crystal",          "Acid Crystal",          ItemType.Material,     ItemRarity.Common,   true, 999, "Tinh thể acid — vật liệu chế tạo acid."),
        Item("item_core_dust",             "Core Dust",             ItemType.UpgradeMaterial, ItemRarity.Uncommon, true, 999, "Bụi core — nâng cấp cơ bản."),
        Item("item_core_fragment",        "Core Fragment",         ItemType.UpgradeMaterial, ItemRarity.Uncommon, true, 999, "Mảnh core — nâng cấp trung cấp."),
        Item("item_core_cell",             "Core Cell",             ItemType.UpgradeMaterial, ItemRarity.Uncommon, true, 999, "Tế bào core — nâng cấp cao cấp."),
        Item("item_rare_core_shard",       "Rare Core Shard",       ItemType.UpgradeMaterial, ItemRarity.Rare,     true, 999, "Mảnh core hiếm — nâng cấp tier cao."),
        Item("item_hardened_scale",        "Hardened Scale",        ItemType.Material,     ItemRarity.Common,   true, 999, "Vảy cứng — vật liệu chế giáp."),
        Item("item_shell_fragment",        "Shell Fragment",        ItemType.Material,     ItemRarity.Common,   true, 999, "Mảnh vỏ giáp dày."),
        Item("item_horn_shard",            "Horn Shard",            ItemType.Material,     ItemRarity.Uncommon, true, 999, "Mảnh sừng — vật liệu hiếm."),
        Item("item_toxic_residue",         "Toxic Residue",         ItemType.Material,     ItemRarity.Common,   true, 999, "Dư chất độc — vật liệu chế poison."),
        Item("item_toxic_crystal",         "Toxic Crystal",         ItemType.Material,     ItemRarity.Uncommon, true, 999, "Tinh thể độc — vật liệu cao cấp."),
        Item("item_alpha_fang_fragment",   "Alpha Fang Fragment",   ItemType.BossDrop,     ItemRarity.Rare,     true, 99,  "Mảnh răng Alpha — boss drop hiếm."),
        Item("item_crystal_ore",           "Crystal Ore",           ItemType.Material,     ItemRarity.Common,   true, 999, "Quặng tinh thể — vật liệu phổ thông."),
        Item("item_data_chip",             "Data Chip",             ItemType.Material,     ItemRarity.Uncommon, true, 999, "Chip dữ liệu — tài nguyên RuinedLab."),
        Item("item_circuit_shard",         "Circuit Shard",         ItemType.Material,     ItemRarity.Common,   true, 999, "Mảnh mạch điện."),
        Item("item_terminal_key_fragment", "Terminal Key Fragment", ItemType.KeyItem,      ItemRarity.Rare,     true, 99,  "Mảnh chìa khoá Terminal — mở cổng RuinedLab."),
        Item("item_corrupted_claw",        "Corrupted Claw",        ItemType.Material,     ItemRarity.Common,   true, 999, "Móng vuốt nhiễm corruption."),
        Item("item_gacha_ticket",          "Gacha Ticket",          ItemType.GachaTicket,  ItemRarity.Rare,     true, 999, "Vé gacha — quy đổi nhân vật."),
        Item("item_health_potion_small",   "Small Health Potion",   ItemType.Consumable,   ItemRarity.Uncommon, true, 99,  "Bình máu nhỏ — hồi HP."),
    };

    static readonly LootRow[] Loots = new[]
    {
        Loot("loot_raptor_basic",      "Wild Claw Raptor",       100,
            ("item_raptor_claw",         0.45f, 1, 2),
            ("item_dino_scale",          0.35f, 1, 2),
            ("item_core_dust",           0.12f, 1, 1),
            ("item_health_potion_small", 0.05f, 1, 1)),
        Loot("loot_lizard_basic",      "Scavenger Lizard",       100,
            ("item_small_claw",          0.40f, 1, 1),
            ("item_dino_scale",          0.25f, 1, 1)),
        Loot("loot_spitter_basic",     "Young Spitter",          100,
            ("item_venom_gland",         0.45f, 1, 2),
            ("item_acid_crystal",        0.30f, 1, 1),
            ("item_core_dust",           0.12f, 1, 1)),
        Loot("loot_tanker_basic",      "Driftback Boar-Lizard",  100,
            ("item_hardened_scale",      0.45f, 1, 2),
            ("item_shell_fragment",      0.35f, 1, 2),
            ("item_core_fragment",       0.12f, 1, 1)),
        Loot("loot_parasite_basic",    "Beach Parasite",         100,
            ("item_toxic_residue",       0.50f, 1, 2),
            ("item_core_dust",           0.08f, 1, 1)),
        Loot("loot_raptor_forest",     "Fang Raptor",            100,
            ("item_raptor_claw",         0.55f, 1, 2),
            ("item_dino_scale",          0.35f, 1, 2),
            ("item_core_dust",           0.18f, 1, 1)),
        Loot("loot_pack_leader",       "Raptor Pack Leader",     100,
            ("item_raptor_claw",         0.80f, 2, 4),
            ("item_alpha_fang_fragment", 0.35f, 1, 1),
            ("item_core_fragment",       0.35f, 1, 2)),
        Loot("loot_armored_herbivore", "Armored Herbivore",      100,
            ("item_hardened_scale",      0.70f, 1, 3),
            ("item_horn_shard",          0.35f, 1, 1),
            ("item_core_fragment",       0.15f, 1, 1)),
        Loot("loot_crystal_caster",    "Crystal Screecher",      100,
            ("item_crystal_ore",         0.55f, 1, 2),
            ("item_data_chip",           0.20f, 1, 1),
            ("item_core_fragment",       0.18f, 1, 1)),
        Loot("loot_spitter_poison",    "Poison Mire Spitter",    100,
            ("item_venom_gland",         0.60f, 1, 3),
            ("item_toxic_crystal",       0.30f, 1, 2),
            ("item_core_fragment",       0.15f, 1, 1)),
        Loot("loot_corrupted_raptor",  "Corrupted Lab Raptor",   100,
            ("item_corrupted_claw",      0.50f, 1, 2),
            ("item_core_fragment",       0.25f, 1, 2)),
        Loot("loot_core_spitter",      "Core Spitter",           100,
            ("item_acid_crystal",        0.50f, 1, 2),
            ("item_core_cell",           0.22f, 1, 1)),
        Loot("loot_warden_elite",      "Warden Drone-Beast",     100,
            ("item_circuit_shard",        0.70f, 2, 4),
            ("item_terminal_key_fragment",0.35f, 1, 1),
            ("item_gacha_ticket",         0.12f, 1, 1)),
        Loot("loot_flux_hound",        "Flux Hound",             100,
            ("item_circuit_shard",       0.55f, 1, 2),
            ("item_core_fragment",       0.20f, 1, 1)),
        Loot("loot_core_corrupted",    "Core Corrupted Raptor",  100,
            ("item_corrupted_claw",      0.55f, 1, 2),
            ("item_core_cell",           0.28f, 1, 1),
            ("item_data_chip",           0.18f, 1, 1)),
        Loot("loot_prism_screecher",   "Prism Screecher",        100,
            ("item_crystal_ore",         0.70f, 2, 3),
            ("item_rare_core_shard",     0.25f, 1, 1),
            ("item_data_chip",           0.30f, 1, 2)),
        Loot("loot_crystal_brute",     "Crystal Shell Brute",    100,
            ("item_shell_fragment",      0.80f, 2, 4),
            ("item_rare_core_shard",     0.30f, 1, 1),
            ("item_gacha_ticket",        0.18f, 1, 1)),
    };

    [MenuItem("ASTRA EDEN/Seed/Items (Loot Pool)")]
    public static void SeedItems()
    {
        EnsureFolder(ItemFolder);
        int c = 0, u = 0;
        foreach (var row in Items)
        {
            string path = $"{ItemFolder}/SO_Item_{ToPascal(row.id)}.asset";
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            bool isNew = it == null;
            if (isNew) { it = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(it, path); }
            it.itemId = row.id;
            it.displayName = row.name;
            it.type = row.type;
            it.rarity = row.rarity;
            it.stackable = row.stackable;
            it.maxStack = row.maxStack;
            it.description = row.description;
            EditorUtility.SetDirty(it);
            if (isNew) c++; else u++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Seeder] Items — created {c}, updated {u} (total {Items.Length}).");
    }

    [MenuItem("ASTRA EDEN/Seed/Loot Tables")]
    public static void SeedLootTables()
    {
        SeedItems();
        EnsureFolder(LootFolder);

        // Map itemId -> ItemData (tìm trong cả ItemFolder lẫn legacy folder)
        var itemLookup = new Dictionary<string, ItemData>();
        foreach (var guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(p);
            if (it != null && !string.IsNullOrEmpty(it.itemId)) itemLookup[it.itemId] = it;
        }

        int c = 0, u = 0;
        foreach (var row in Loots)
        {
            string path = $"{LootFolder}/SO_LootTable_{ToPascal(row.tableId)}.asset";
            var lt = AssetDatabase.LoadAssetAtPath<LootTableData>(path);
            bool isNew = lt == null;
            if (isNew) { lt = ScriptableObject.CreateInstance<LootTableData>(); AssetDatabase.CreateAsset(lt, path); }

            lt.entries = new List<LootTableData.Entry>();
            lt.rollCount = 0;     // không dùng weighted roll, dùng guaranteed-with-chance cho khớp bảng
            lt.rollChance = 1f;

            foreach (var e in row.entries)
            {
                if (!itemLookup.TryGetValue(e.id, out var itemSO))
                {
                    Debug.LogWarning($"[Seeder] LootTable '{row.tableId}' missing item '{e.id}'.");
                    continue;
                }
                lt.entries.Add(new LootTableData.Entry
                {
                    item = itemSO,
                    weight = 1f,
                    minQuantity = Mathf.Max(1, e.minQ),
                    maxQuantity = Mathf.Max(e.minQ, e.maxQ),
                    guaranteed = true,
                    guaranteedChance = e.chance,
                });
            }

            EditorUtility.SetDirty(lt);
            if (isNew) c++; else u++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Seeder] LootTables — created {c}, updated {u} (total {Loots.Length}). Re-run 'Seed → Enemies (Full)' để link loot vào EnemyData.");
    }

    [MenuItem("ASTRA EDEN/Seed/ALL (Items + Loot + Enemies)")]
    public static void SeedAll()
    {
        SeedItems();
        SeedLootTables();
        EnemyDataSeeder.SeedEnemiesFull();
        Debug.Log("[Seeder] Done all phases.");
    }

    static ItemRow Item(string id, string n, ItemType t, ItemRarity r, bool stackable, int maxStack, string desc)
        => new ItemRow { id = id, name = n, type = t, rarity = r, stackable = stackable, maxStack = maxStack, description = desc };

    static LootRow Loot(string tableId, string enemyLabel, int gold,
        params (string id, float chance, int minQ, int maxQ)[] entries)
        => new LootRow { tableId = tableId, enemyLabel = enemyLabel, gold = gold, entries = entries };

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
