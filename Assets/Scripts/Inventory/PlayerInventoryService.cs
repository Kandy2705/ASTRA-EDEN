using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventoryService : MonoBehaviour
{
    [Header("Inventory Data")]
    [SerializeField] private List<InventoryItemStack> items = new List<InventoryItemStack>();

    [Header("Debug Test")]
    [SerializeField] private ItemData debugItem;
    [SerializeField] private int debugAmount = 1;

    public IReadOnlyList<InventoryItemStack> Items => items;

    public event Action OnInventoryChanged;

    private void Start()
    {
        LoadFromGameData();
    }

    private void OnDestroy()
    {
        SaveToGameData();
    }

    public void LoadFromGameData()
    {
        if (GameDataManager.Instance == null) return;

        Dictionary<string, int> data = GameDataManager.Instance.LoadInventory();
        items.Clear();

        foreach (var kvp in data)
        {
            ItemData item = ItemRegistry.Get(kvp.Key);
            if (item != null)
            {
                items.Add(new InventoryItemStack(item, kvp.Value));
            }
        }

        OnInventoryChanged?.Invoke();
    }

    public void SaveToGameData()
    {
        if (GameDataManager.Instance == null) return;

        Dictionary<string, int> data = new Dictionary<string, int>();
        foreach (var stack in items)
        {
            if (stack?.itemData == null || string.IsNullOrEmpty(stack.itemData.itemId)) continue;
            data[stack.itemData.itemId] = stack.quantity;
        }

        GameDataManager.Instance.SaveInventory(data);
    }

    public bool AddItem(ItemData itemData, int amount)
    {
        if (itemData == null)
        {
            Debug.LogWarning("[Inventory] AddItem failed: itemData is null.");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning("[Inventory] AddItem failed: amount <= 0.");
            return false;
        }

        InventoryItemStack existingStack = FindStack(itemData);

        if (existingStack != null)
        {
            existingStack.quantity += amount;
        }
        else
        {
            items.Add(new InventoryItemStack(itemData, amount));
        }

        Debug.Log($"[Inventory] Added {amount} x {itemData.name}");
        OnInventoryChanged?.Invoke();
        SaveToGameData();
        return true;
    }

    public bool RemoveItem(ItemData itemData, int amount)
    {
        if (itemData == null || amount <= 0)
            return false;

        InventoryItemStack existingStack = FindStack(itemData);

        if (existingStack == null)
            return false;

        if (existingStack.quantity < amount)
            return false;

        existingStack.quantity -= amount;

        if (existingStack.quantity <= 0)
        {
            items.Remove(existingStack);
        }

        OnInventoryChanged?.Invoke();
        SaveToGameData();
        return true;
    }

    public int GetQuantity(ItemData itemData)
    {
        InventoryItemStack stack = FindStack(itemData);
        return stack != null ? stack.quantity : 0;
    }

    public bool HasItem(ItemData itemData, int amount)
    {
        return GetQuantity(itemData) >= amount;
    }

    /// <summary>Dung 1 consumable: tru 1 trong inventory, ap dung restore HP cho CharacterHealth tren player.</summary>
    public bool UseConsumable(ItemData itemData)
    {
        if (itemData == null || itemData.type != ItemType.Consumable) return false;
        if (!HasItem(itemData, 1)) return false;

        var health = GetComponent<CharacterHealth>();
        if (health != null && itemData.restoreHP > 0f)
        {
            health.Heal(itemData.restoreHP);
        }

        // TODO: stamina/energy hooks khi co system, hien tai chua co CharacterStamina/CharacterEnergy component.
        RemoveItem(itemData, 1);
        return true;
    }

    private InventoryItemStack FindStack(ItemData itemData)
    {
        if (itemData == null)
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItemStack stack = items[i];

            if (stack == null || stack.itemData == null)
            {
                continue;
            }

            if (stack.itemData == itemData)
            {
                return stack;
            }

            if (!string.IsNullOrEmpty(itemData.itemId) &&
                stack.itemData.itemId == itemData.itemId)
            {
                return stack;
            }
        }

        return null;
    }

    [ContextMenu("Debug Add Test Item")]
    private void DebugAddTestItem()
    {
        AddItem(debugItem, debugAmount);
    }

    [ContextMenu("Debug Print Inventory")]
    private void DebugPrintInventory()
    {
        Debug.Log("===== PLAYER INVENTORY =====");

        foreach (InventoryItemStack stack in items)
        {
            if (stack.itemData == null) continue;
            Debug.Log($"{stack.itemData.name} x {stack.quantity}");
        }
    }
}