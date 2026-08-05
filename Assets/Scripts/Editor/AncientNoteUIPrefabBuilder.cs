#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tạo prefab Ancient Note UI đúng một lần từ layout chuẩn. Không overwrite asset
/// đã có, nên các chỉnh sửa UI về sau của designer luôn được giữ nguyên.
/// </summary>
[InitializeOnLoad]
public static class AncientNoteUIPrefabBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefab/AncientNotePopup.prefab";

    static AncientNoteUIPrefabBuilder()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    [MenuItem("Tools/ASTRA EDEN/UI/Create Missing Ancient Note Popup Prefab")]
    public static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (savedPrefab == null || savedPrefab.transform.Find("Parchment") == null)
        {
            savedPrefab = BuildAndSavePopup();
        }

        WirePopupIntoPickup(savedPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject BuildAndSavePopup()
    {
        GameObject root = new(
            "AncientNotePopup",
            typeof(RectTransform),
            typeof(CanvasGroup));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        AncientNoteUIController controller = root.AddComponent<AncientNoteUIController>();
        controller.EditorBuildPrefabLayout();
        root.SetActive(false);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[AncientNote] Created editable UI prefab: {PrefabPath}");
        return savedPrefab;
    }

    private static void WirePopupIntoPickup(GameObject popupPrefab)
    {
        if (popupPrefab == null)
        {
            return;
        }

        const string pickupPath = "Assets/_Project/Prefab/AncientNotePickup.prefab";
        GameObject pickupRoot = PrefabUtility.LoadPrefabContents(pickupPath);
        if (pickupRoot == null)
        {
            return;
        }

        try
        {
            AncientNotePickup pickup = pickupRoot.GetComponent<AncientNotePickup>();
            AncientNoteUIController popup = popupPrefab.GetComponent<AncientNoteUIController>();
            if (pickup == null || popup == null)
            {
                return;
            }

            SerializedObject serializedPickup = new(pickup);
            SerializedProperty prefabProperty = serializedPickup.FindProperty("noteUiPrefab");
            if (prefabProperty != null && prefabProperty.objectReferenceValue != popup)
            {
                prefabProperty.objectReferenceValue = popup;
                serializedPickup.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(pickupRoot, pickupPath);
                Debug.Log("[AncientNote] Wired editable popup prefab into AncientNotePickup.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(pickupRoot);
        }
    }
}
#endif
