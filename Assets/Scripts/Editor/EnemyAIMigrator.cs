#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Auto-migrate enemy prefab sang stack AI mới (EnemyAIController + EnemySensor + EnemyData).
/// - Gỡ EnemyPatrol cũ (nếu có).
/// - Add NavMeshAgent, CharacterHealth, EnemySensor, EnemyAIController, CharacterKnockback.
/// - Match EnemyData SO theo asset name của prefab (mapping name → enemyId).
/// Idempotent — chạy lại không nhân đôi component.
/// </summary>
public static class EnemyAIMigrator
{
    const string EnemySOFolder = "Assets/_Project/ScriptableObjects/Enemies/Units";

    // Mapping asset prefix → enemyId. Match theo asset name chứa key (case-insensitive).
    static readonly (string nameContains, string enemyId)[] NameToEnemyId = new[]
    {
        ("velociraptor",     "enemy_wild_claw_raptor"),
        ("scavenger",        "enemy_scavenger_lizard"),
        ("young_spitter",    "enemy_young_spitter"),
        ("spitter_young",    "enemy_young_spitter"),
        ("boarlizard",       "enemy_driftback_boarlizard"),
        ("boar_lizard",      "enemy_driftback_boarlizard"),
        ("parasite",         "enemy_beach_parasite"),
        ("fang",             "enemy_fang_raptor"),
        ("pack_leader",      "enemy_raptor_pack_leader"),
        ("armored_herbivore","enemy_armored_herbivore_juvenile"),
        ("crystal_screecher","enemy_crystal_screecher"),
        ("poison_mire",      "enemy_poison_mire_spitter"),
        ("corrupted_lab",    "enemy_corrupted_lab_raptor"),
        ("core_spitter",     "enemy_core_spitter"),
        ("warden",           "enemy_security_warden_dronebeast"),
        ("flux_hound",       "enemy_flux_hound"),
        ("core_corrupted",   "enemy_core_corrupted_raptor"),
        ("prism_screecher",  "enemy_prism_screecher"),
        ("shell_brute",      "enemy_crystal_shell_brute"),
        ("brute",            "enemy_crystal_shell_brute"),
    };

    [MenuItem("ASTRA EDEN/Migrate/Apply Enemy AI to Selection")]
    public static void ApplyToSelection()
    {
        var assets = Selection.objects;
        if (assets == null || assets.Length == 0)
        {
            EditorUtility.DisplayDialog("Enemy AI Migrator", "Chọn 1 hoặc nhiều prefab enemy trong Project rồi chạy lại menu này.", "OK");
            return;
        }

        var enemyLookup = BuildEnemyLookup();
        int processed = 0, linked = 0, missing = 0;

        foreach (var obj in assets)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool linkedThis = MigratePrefab(root, path, enemyLookup);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                processed++;
                if (linkedThis) linked++; else missing++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log($"[EnemyAIMigrator] Processed {processed} prefab(s). Linked EnemyData: {linked}. Missing/unmapped: {missing}.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("ASTRA EDEN/Migrate/Apply Enemy AI to Selection", true)]
    static bool ValidateApply()
    {
        return Selection.objects != null && Selection.objects.Length > 0;
    }

    static Dictionary<string, EnemyData> BuildEnemyLookup()
    {
        var map = new Dictionary<string, EnemyData>();
        if (!AssetDatabase.IsValidFolder(EnemySOFolder)) return map;
        foreach (var guid in AssetDatabase.FindAssets("t:EnemyData", new[] { EnemySOFolder }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var ed = AssetDatabase.LoadAssetAtPath<EnemyData>(p);
            if (ed != null && !string.IsNullOrEmpty(ed.enemyId)) map[ed.enemyId] = ed;
        }
        return map;
    }

    static EnemyData ResolveEnemyData(string assetPath, Dictionary<string, EnemyData> lookup)
    {
        string assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        foreach (var (token, id) in NameToEnemyId)
        {
            if (assetName.Contains(token) && lookup.TryGetValue(id, out var ed)) return ed;
        }
        return null;
    }

    static bool MigratePrefab(GameObject root, string assetPath, Dictionary<string, EnemyData> lookup)
    {
        // 1) Đọc các setting đáng giữ từ EnemyPatrol cũ TRƯỚC khi gỡ.
        bool oldFlipForward180 = false;
        bool hasOldPatrol = false;
        var oldPatrol = root.GetComponent<EnemyPatrol>();
        if (oldPatrol != null)
        {
            hasOldPatrol = true;
            var soOld = new SerializedObject(oldPatrol);
            var flipProp = soOld.FindProperty("flipForward180");
            if (flipProp != null) oldFlipForward180 = flipProp.boolValue;
            Object.DestroyImmediate(oldPatrol, true);
        }

        // 2) Đảm bảo có NavMeshAgent + CharacterHealth.
        EnsureComponent<NavMeshAgent>(root);
        EnsureComponent<CharacterHealth>(root);

        // 3) Add EnemySensor + EnemyAIController + CharacterKnockback nếu chưa.
        var sensor = EnsureComponent<EnemySensor>(root);
        var ai = EnsureComponent<EnemyAIController>(root);
        EnsureComponent<CharacterKnockback>(root);

        // 3b) Migrate flipForward180 từ EnemyPatrol cũ sang EnemyAIController.
        if (hasOldPatrol)
        {
            var soAI = new SerializedObject(ai);
            var flipNew = soAI.FindProperty("flipForward180");
            if (flipNew != null) flipNew.boolValue = oldFlipForward180;
            soAI.ApplyModifiedPropertiesWithoutUndo();
        }

        // 4) Match EnemyData → gán vào AI + Sensor.
        var enemyData = ResolveEnemyData(assetPath, lookup);
        if (enemyData != null)
        {
            var soAI = new SerializedObject(ai);
            soAI.FindProperty("enemyData").objectReferenceValue = enemyData;
            soAI.ApplyModifiedPropertiesWithoutUndo();

            var soSensor = new SerializedObject(sensor);
            soSensor.FindProperty("enemyData").objectReferenceValue = enemyData;
            soSensor.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
        Debug.LogWarning($"[EnemyAIMigrator] Không tìm thấy EnemyData khớp cho '{assetPath}'. Tự gán SO trong Inspector hoặc cập nhật mapping NameToEnemyId.");
        return false;
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }
}
#endif
