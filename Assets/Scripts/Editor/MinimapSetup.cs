#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Wire MinimapController vào HUD_MinimapPanel trong GameplayUI_Root.prefab.
/// Menu: ASTRA EDEN / Minimap / Setup Minimap
public static class MinimapSetup
{
    const string PrefabPath = "Assets/_Project/Prefab/UI/GameplayUI_Root.prefab";

    [MenuItem("ASTRA EDEN/Minimap/Setup Minimap")]
    public static void SetupMinimap()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[Minimap] Không load được prefab: {PrefabPath}");
            return;
        }

        try
        {
            Transform panel = FindDeep(root.transform, "HUD_MinimapPanel");
            if (panel == null)
            {
                Debug.LogError("[Minimap] Không tìm thấy HUD_MinimapPanel trong prefab.");
                return;
            }

            Transform mask = FindDeep(root.transform, "MinimapMask");
            Transform compass = FindDeep(root.transform, "CompassGroup");
            Transform legacyImage = FindDeep(root.transform, "MinimapImage");

            if (mask == null || compass == null)
            {
                Debug.LogError("[Minimap] Thiếu MinimapMask hoặc CompassGroup — kiểm tra lại prefab.");
                return;
            }

            var controller = panel.GetComponent<MinimapController>();
            if (controller == null) controller = panel.gameObject.AddComponent<MinimapController>();

            var so = new SerializedObject(controller);
            so.FindProperty("minimapMask").objectReferenceValue = mask.GetComponent<RectTransform>();
            so.FindProperty("compassGroup").objectReferenceValue = compass.GetComponent<RectTransform>();
            so.FindProperty("legacyStaticImage").objectReferenceValue = legacyImage != null ? legacyImage.gameObject : null;

            // Render mọi thứ trừ UI (layer 5). Terrain/environment sẽ hiện, UI screen-space không cần.
            int mask5 = 1 << 5;
            so.FindProperty("minimapCullingMask").intValue = ~mask5;

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[Minimap] Đã gắn MinimapController vào HUD_MinimapPanel. " +
                      "Gán sprite marker (PlayerMarker/EnemyMarker) trong Inspector nếu muốn icon đẹp hơn, " +
                      "rồi Play scene World_Eden7 để test.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindDeep(Transform parent, string targetName)
    {
        if (parent.name == targetName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindDeep(parent.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
