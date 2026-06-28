using System;
using UnityEngine;

[Serializable]
public class InventoryItemStack
{
    public ItemData itemData;
    public int quantity;

    public InventoryItemStack(ItemData itemData, int quantity)
    {
        this.itemData = itemData;
        this.quantity = Mathf.Max(0, quantity);
    }
}