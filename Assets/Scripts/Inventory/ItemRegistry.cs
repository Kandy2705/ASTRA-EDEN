using System.Collections.Generic;
using UnityEngine;

public static class ItemRegistry
{
    private static Dictionary<string, ItemData> itemsById;
    private static bool initialized;

    public static void Initialize(List<ItemData> allItems)
    {
        itemsById = new Dictionary<string, ItemData>();
        initialized = true;

        if (allItems == null) return;

        foreach (ItemData item in allItems)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
            if (!itemsById.ContainsKey(item.itemId))
            {
                itemsById[item.itemId] = item;
            }
        }
    }

    public static ItemData Get(string itemId)
    {
        if (!initialized) return null;
        if (string.IsNullOrEmpty(itemId)) return null;
        itemsById.TryGetValue(itemId, out ItemData item);
        return item;
    }

    public static bool HasItem(string itemId)
    {
        return Get(itemId) != null;
    }
}
