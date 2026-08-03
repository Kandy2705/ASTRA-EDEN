using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class PlayerInventoryService : MonoBehaviour
{
    [Header("Inventory Data")]
    [SerializeField] private List<InventoryItemStack> items = new List<InventoryItemStack>();

    [Header("Debug Test")]
    [SerializeField] private ItemData debugItem;
    [SerializeField] private int debugAmount = 1;

    public IReadOnlyList<InventoryItemStack> Items => items;

    public event Action OnInventoryChanged;

    private bool hasLoadedFromSave;

    public static PlayerInventoryService FindForPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return null;
        }

        PlayerInventoryService[] services = player.GetComponents<PlayerInventoryService>();
        if (services.Length > 1)
        {
            Debug.LogWarning(
                $"[Inventory] Player có {services.Length} PlayerInventoryService — dùng component đầu tiên, xóa bản trùng trong scene/prefab.");
        }

        return services.Length > 0 ? services[0] : null;
    }

    private void Start()
    {
        TryLoadFromGameData();

        if (!hasLoadedFromSave)
        {
            OnInventoryChanged?.Invoke();
        }
    }

    private void TryLoadFromGameData()
    {
        if (hasLoadedFromSave || GameDataManager.Instance == null)
        {
            return;
        }

        LoadFromGameData();
        hasLoadedFromSave = true;
    }

    private void OnApplicationQuit()
    {
        SaveToGameData();
    }

    private void OnDestroy()
    {
        SaveToGameData();
    }

    public void LoadFromGameData()
    {
        if (GameDataManager.Instance == null)
        {
            Debug.LogWarning("[Inventory] LoadFromGameData bỏ qua — chưa có GameDataManager.");
            return;
        }

        Dictionary<string, int> data = GameDataManager.Instance.LoadInventory();
        items.Clear();

        int loadedCount = 0;
        int missingCount = 0;

        foreach (var kvp in data)
        {
            ItemData item = GameDataManager.Instance.ResolveItem(kvp.Key);
            if (item != null)
            {
                items.Add(new InventoryItemStack(item, kvp.Value));
                loadedCount++;
            }
            else
            {
                missingCount++;
                Debug.LogWarning($"[Inventory] Không tìm thấy ItemData cho itemId='{kvp.Key}' (qty={kvp.Value}).");
            }
        }

        // Gold nguồn sự thật = inventory item. Migrate legacy ASTRA_CURRENCY → stack gold một lần.
        MigrateLegacyCurrencyIntoGoldStacks();
        SyncCurrencyMirrorToGameData();

        Debug.Log($"[Inventory] Loaded {loadedCount} stacks from save (missing={missingCount}, raw={data.Count}, gold={GetGoldQuantity()}).");
        OnInventoryChanged?.Invoke();
    }

    public void SaveToGameData()
    {
        if (GameDataManager.Instance == null)
        {
            Debug.LogWarning("[Inventory] SaveToGameData bỏ qua — chưa có GameDataManager.");
            return;
        }

        Dictionary<string, int> data = new Dictionary<string, int>();
        foreach (var stack in items)
        {
            if (stack?.itemData == null || string.IsNullOrEmpty(stack.itemData.itemId)) continue;
            data[stack.itemData.itemId] = stack.quantity;
        }

        GameDataManager.Instance.SaveInventory(data);
        SyncCurrencyMirrorToGameData();
        Debug.Log($"[Inventory] Saved {data.Count} item types to PlayerPrefs (gold={GetGoldQuantity()}).");
    }

    /// <summary>
    /// Nếu inventory chưa có gold nhưng PlayerPrefs còn ASTRA_CURRENCY cũ → chuyển vào stack gold.
    /// Inventory là nguồn sự thật sau migrate.
    /// </summary>
    void MigrateLegacyCurrencyIntoGoldStacks()
    {
        if (GameDataManager.Instance == null)
        {
            return;
        }

        int legacy = GameDataManager.Instance.Currency;
        if (legacy <= 0)
        {
            return;
        }

        ItemData gold = ResolveGoldItem();
        if (gold == null)
        {
            Debug.LogWarning("[Inventory] Không resolve được SO_Item_Gold để migrate Currency.");
            return;
        }

        int invGold = GetQuantity(gold);
        if (invGold > 0)
        {
            // Inventory đã có gold → giữ inventory, bỏ wallet int (không cộng đúp).
            return;
        }

        items.Add(new InventoryItemStack(gold, legacy));
        Debug.Log($"[Inventory] Migrated legacy Currency {legacy} → inventory gold.");
    }

    /// <summary>Đồng bộ field Currency của GameDataManager chỉ để mirror (UI/API cũ), không phải wallet thứ 2.</summary>
    void SyncCurrencyMirrorToGameData()
    {
        if (GameDataManager.Instance == null)
        {
            return;
        }

        GameDataManager.Instance.SetCurrencyMirror(GetGoldQuantity());
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

        Debug.Log($"[Inventory] Added {amount} x {itemData.name} (total stacks={items.Count}, service={name})");
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

    /// <summary>
    /// Chuyển toàn bộ item có thể rơi sang túi tử vong. Key Item được giữ lại để
    /// tránh khóa cứng tiến trình nếu Player không kịp quay lại trong 10 phút.
    /// </summary>
    public List<InventoryItemStack> ExtractDeathDropItems()
    {
        List<InventoryItemStack> dropped = new List<InventoryItemStack>();
        for (int i = items.Count - 1; i >= 0; i--)
        {
            InventoryItemStack stack = items[i];
            if (stack?.itemData == null || stack.quantity <= 0 ||
                stack.itemData.type == ItemType.KeyItem)
            {
                continue;
            }

            dropped.Add(new InventoryItemStack(stack.itemData, stack.quantity));
            items.RemoveAt(i);
        }

        if (dropped.Count > 0)
        {
            OnInventoryChanged?.Invoke();
            SaveToGameData();
        }

        return dropped;
    }

    /// <summary>Trả toàn bộ nội dung túi tử vong về inventory và chỉ save một lần.</summary>
    public void RestoreDeathDropItems(IReadOnlyList<InventoryItemStack> recoveredItems)
    {
        if (recoveredItems == null || recoveredItems.Count == 0)
        {
            return;
        }

        for (int i = 0; i < recoveredItems.Count; i++)
        {
            InventoryItemStack recovered = recoveredItems[i];
            if (recovered?.itemData == null || recovered.quantity <= 0)
            {
                continue;
            }

            InventoryItemStack existing = FindStack(recovered.itemData);
            if (existing != null)
            {
                existing.quantity += recovered.quantity;
            }
            else
            {
                items.Add(new InventoryItemStack(recovered.itemData, recovered.quantity));
            }
        }

        OnInventoryChanged?.Invoke();
        SaveToGameData();
    }

    public int GetQuantity(ItemData itemData)
    {
        InventoryItemStack stack = FindStack(itemData);
        return stack != null ? stack.quantity : 0;
    }

    public static ItemData ResolveGoldItem(ItemData assignedGold = null)
    {
        if (assignedGold != null)
        {
            return assignedGold;
        }

        if (GameDataManager.Instance != null)
        {
            ItemData fromManager = GameDataManager.Instance.ResolveItem("gold");
            if (fromManager != null)
            {
                return fromManager;
            }
        }

        return ItemRegistry.Get("gold");
    }

    /// <summary>Gold = số lượng item gold trong inventory (không đọc wallet int riêng).</summary>
    public int GetGoldQuantity(ItemData assignedGold = null)
    {
        ItemData gold = ResolveGoldItem(assignedGold);
        if (gold == null)
        {
            return 0;
        }

        return GetQuantity(gold);
    }

    public bool TrySpendGold(int amount, ItemData assignedGold = null)
    {
        if (amount <= 0)
        {
            return true;
        }

        ItemData gold = ResolveGoldItem(assignedGold);
        if (gold == null)
        {
            return false;
        }

        return RemoveItem(gold, amount);
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
