using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LootCollector : MonoBehaviour
{
    [SerializeField] private bool logToConsole = true;

    private PlayerInventoryService inventoryService;

    public event Action<ItemData, int> Collected;

    private void Awake()
    {
        inventoryService = ResolveInventoryService();
    }

    private PlayerInventoryService ResolveInventoryService()
    {
        PlayerInventoryService onSelf = GetComponent<PlayerInventoryService>();
        if (onSelf != null)
        {
            return onSelf;
        }

        return GetComponentInParent<PlayerInventoryService>() ?? PlayerInventoryService.FindForPlayer();
    }

    public void Collect(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return;
        }

        if (inventoryService == null)
        {
            inventoryService = ResolveInventoryService();
        }

        // Gold và mọi item đều vào inventory (single source). Không cộng GameDataManager.Currency riêng.
        if (inventoryService != null)
        {
            inventoryService.AddItem(item, quantity);
        }
        else
        {
            Debug.LogWarning("[LootCollector] Không tìm thấy PlayerInventoryService — loot bị mất.", this);
        }

        Collected?.Invoke(item, quantity);

        if (logToConsole)
        {
            Debug.Log($"[Loot] +{quantity} {item.displayName} ({item.type})");
        }
    }
}
