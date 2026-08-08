#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Setup tiến trình Floating Tree:
/// 1. Tạo prefab AncientNotePickupNote2 (bản sao của AncientNotePickup với noteId = Note2Id).
/// 2. Gắn FloatingTreeProgression lên "Flying_Tree_Zone_2" trong scene và trỏ note2Prefab.
/// Menu: ASTRA EDEN → Floating Tree → ...
/// </summary>
public static class FloatingTreeNote2Setup
{
    const string BaseNotePath = "Assets/_Project/Prefab/AncientNotePickup.prefab";
    const string Note2Path = "Assets/_Project/Prefab/AncientNotePickupNote2.prefab";
    const string TreeObjectName = "Flying_Tree_Zone_2";

    [MenuItem("ASTRA EDEN/Floating Tree/Build Note #2 Prefab")]
    public static void BuildNote2Prefab()
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseNotePath);
        if (basePrefab == null)
        {
            Debug.LogError($"[FloatingTreeSetup] Missing base note prefab: {BaseNotePath}");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(Note2Path) == null)
        {
            if (!AssetDatabase.CopyAsset(BaseNotePath, Note2Path))
            {
                Debug.LogError($"[FloatingTreeSetup] Could not copy note prefab to {Note2Path}");
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(Note2Path);
        try
        {
            root.name = "AncientNotePickupNote2";
            AncientNotePickup pickup = root.GetComponent<AncientNotePickup>();
            if (pickup == null)
            {
                Debug.LogError("[FloatingTreeSetup] Note #2 prefab missing AncientNotePickup component.");
                return;
            }

            SerializedObject so = new SerializedObject(pickup);
            so.FindProperty("noteId").stringValue = AncientNotePickup.Note2Id;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, Note2Path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FloatingTreeSetup] Note #2 prefab ready: {Note2Path}");
    }

    [MenuItem("ASTRA EDEN/Floating Tree/Setup Floating Tree in Scene")]
    public static void SetupSceneTree()
    {
        GameObject tree = GameObject.Find(TreeObjectName);
        if (tree == null)
        {
            Debug.LogError(
                $"[FloatingTreeSetup] Không tìm thấy '{TreeObjectName}' trong scene đang mở. " +
                "Hãy mở World_Eden7 rồi chạy menu này.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(Note2Path) == null)
        {
            BuildNote2Prefab();
        }

        FloatingTreeProgression progression = tree.GetComponent<FloatingTreeProgression>();
        if (progression == null)
        {
            progression = tree.AddComponent<FloatingTreeProgression>();
        }

        GameObject note2Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Note2Path);
        SerializedObject so = new SerializedObject(progression);
        so.FindProperty("note2Prefab").objectReferenceValue = note2Prefab;
        so.FindProperty("note2GroundClearance").floatValue = 0.08f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(progression);

        EditorSceneManager.MarkSceneDirty(tree.scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[FloatingTreeSetup] Đã gắn FloatingTreeProgression lên '{TreeObjectName}' và trỏ Note #2 prefab.", tree);
    }
}
#endif
