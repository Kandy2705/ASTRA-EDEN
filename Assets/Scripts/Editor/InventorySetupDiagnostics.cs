#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class InventorySetupDiagnostics
{
    [MenuItem("ASTRA EDEN/Debug/Diagnose Inventory (scene hiện tại)")]
    public static void DiagnoseActiveScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        Debug.Log($"===== INVENTORY DIAGNOSE: {scene.name} =====");

        InventoryToggleController toggle = Object.FindFirstObjectByType<InventoryToggleController>(FindObjectsInactive.Include);
        if (toggle == null)
        {
            Debug.LogError("[Diagnose] THIẾU InventoryToggleController trong scene.");
        }
        else
        {
            SerializedObject so = new SerializedObject(toggle);
            var root = so.FindProperty("inventoryRoot").objectReferenceValue as GameObject;
            var screen = so.FindProperty("inventoryScreenController").objectReferenceValue;
            var hud = so.FindProperty("gameplayHudCanvas").objectReferenceValue as GameObject;
            int key = so.FindProperty("toggleKey").intValue;

            Debug.Log($"[Diagnose] Toggle on '{toggle.gameObject.name}', enabled={toggle.enabled}, key={(Key)key}");
            Debug.Log($"[Diagnose] inventoryRoot={(root != null ? root.name : "NULL")}, active={(root != null && root.activeSelf)}");
            Debug.Log($"[Diagnose] screenController={(screen != null ? screen.name : "NULL")}");
            Debug.Log($"[Diagnose] gameplayHud={(hud != null ? hud.name : "NULL")}");
        }

        GameObject panels = GameObject.Find("Panels");
        if (panels == null)
        {
            Debug.LogError("[Diagnose] THIẾU object 'Panels' trong scene.");
        }
        else
        {
            Transform inventory = panels.transform.Find("Inventory");
            Debug.Log(inventory != null
                ? $"[Diagnose] Panels/Inventory OK, active={inventory.gameObject.activeSelf}"
                : "[Diagnose] THIẾU Panels/Inventory.");
        }

        Canvas menuCanvas = GameObject.Find("Menu_Canvas")?.GetComponent<Canvas>();
        if (menuCanvas == null)
        {
            Debug.LogError("[Diagnose] THIẾU Menu_Canvas hoặc Canvas component.");
        }
        else
        {
            Debug.Log($"[Diagnose] Menu_Canvas renderMode={menuCanvas.renderMode}, camera={menuCanvas.worldCamera}, sortingOrder={menuCanvas.sortingOrder}");
        }

        InventoryScreenController screenController = Object.FindFirstObjectByType<InventoryScreenController>(FindObjectsInactive.Include);
        Debug.Log(screenController != null
            ? $"[Diagnose] InventoryScreenController on '{screenController.gameObject.name}'"
            : "[Diagnose] THIẾU InventoryScreenController.");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[Diagnose] THIẾU Player (tag 'Player').");
        }
        else
        {
            PlayerInventoryService[] services = player.GetComponents<PlayerInventoryService>();
            if (services.Length == 0)
            {
                Debug.LogError("[Diagnose] Player không có PlayerInventoryService.");
            }
            else if (services.Length > 1)
            {
                Debug.LogError($"[Diagnose] Player có {services.Length} PlayerInventoryService — XÓA bản trùng (chỉ giữ 1).");
            }
            else
            {
                Debug.Log($"[Diagnose] PlayerInventoryService OK trên '{player.name}'.");
            }
        }

        if (GameDataManager.Instance != null && toggle != null && toggle.gameObject == GameDataManager.Instance.gameObject)
        {
            Debug.LogWarning("[Diagnose] GameDataManager và InventoryToggle cùng 1 GameObject — OK sau khi fix Destroy(this).");
        }

        Debug.Log("===== Kết thúc diagnose — Play mode: bấm B, tìm log [InventoryToggle] Opened =====");
    }

    [MenuItem("ASTRA EDEN/Debug/Fix Inventory Scene Wiring")]
    public static void FixInventorySceneWiring()
    {
        VerticalSliceDemoSetup.FixInventorySceneWiringPublic();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Diagnose] Đã chạy fix wiring. Save scene rồi Play test B.");
    }
}
#endif