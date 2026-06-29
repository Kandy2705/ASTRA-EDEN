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

        if (inventoryService != null)
        {
            inventoryService.AddItem(item, quantity);
        }

        if (item.type == ItemType.Currency && GameDataManager.Instance != null)
        {
            GameDataManager.Instance.AddCurrency(quantity);
        }

        Collected?.Invoke(item, quantity);

        if (logToConsole)
        {
            Debug.Log($"[Loot] +{quantity} {item.displayName} ({item.type})");
        }
    }
}
