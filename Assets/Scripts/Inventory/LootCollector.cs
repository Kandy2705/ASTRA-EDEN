using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tam thoi nhan item pickup va luu vao dictionary trong nho + cong Currency vao GameDataManager neu la Currency.
/// Khi InventoryService chinh thuc co, thay the boi service do.
/// </summary>
public class LootCollector : MonoBehaviour
{
    [SerializeField] private bool logToConsole = true;

    private readonly Dictionary<string, int> stash = new Dictionary<string, int>();

    public event Action<ItemData, int> Collected;

    public IReadOnlyDictionary<string, int> Stash => stash;

    public int GetQuantity(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        return stash.TryGetValue(itemId, out int q) ? q : 0;
    }

    public void Collect(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0) return;

        // Currency -> day vao GameDataManager
        if (item.type == ItemType.Currency && GameDataManager.Instance != null)
        {
            GameDataManager.Instance.AddCurrency(quantity);
        }

        // Cong vao stash chung (cho cac type khac, va de UI tam thoi co cho doc)
        if (string.IsNullOrEmpty(item.itemId))
        {
            if (logToConsole) Debug.LogWarning($"PickupItem: '{item.name}' khong co itemId.", item);
        }
        else
        {
            stash.TryGetValue(item.itemId, out int current);
            stash[item.itemId] = current + quantity;
        }

        Collected?.Invoke(item, quantity);

        if (logToConsole)
        {
            Debug.Log($"[Loot] +{quantity} {item.displayName} ({item.type})");
        }
    }
}
