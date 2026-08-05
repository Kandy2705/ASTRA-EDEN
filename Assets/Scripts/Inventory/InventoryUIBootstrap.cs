using UnityEngine;

/// <summary>
/// Đảm bảo scene luôn có InventoryToggleController, kể cả khi Managers bị ảnh hưởng bởi singleton khác.
/// Gắn script này lên Menu_Canvas.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class InventoryUIBootstrap : MonoBehaviour
{
    [SerializeField] private bool logDiagnostics;

    private void Awake()
    {
        InventoryToggleController existing = FindFirstObjectByType<InventoryToggleController>(FindObjectsInactive.Include);
        if (existing != null)
        {

            return;
        }

        InventoryToggleController created = gameObject.AddComponent<InventoryToggleController>();
    }
}