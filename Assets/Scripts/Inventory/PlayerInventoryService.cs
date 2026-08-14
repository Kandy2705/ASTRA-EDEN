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
    private bool ensuringCollectedDocuments;
    // Không được làm rơi các entry chỉ vì scene hiện tại chưa đăng ký đủ ItemData.
    // Các entry này sẽ được giữ nguyên trong PlayerPrefs và thử resolve lại sau.
    private readonly Dictionary<string, int> unresolvedSavedItems = new Dictionary<string, int>();

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
        }

        return services.Length > 0 ? services[0] : null;
    }

    private void Start()
    {
        TryLoadFromGameData();

        EnsureCollectedAncientMapItems();

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
        if (hasLoadedFromSave)
        {
            SaveToGameData();
        }
    }

    private void OnDestroy()
    {
        // Có thể bị destroy khi chuyển scene trước Start. Khi đó `items` vẫn rỗng
        // nhưng save thật có dữ liệu; tuyệt đối không ghi rỗng đè lên save.
        if (hasLoadedFromSave)
        {
            SaveToGameData();
        }
    }

    public void LoadFromGameData()
    {
        if (GameDataManager.Instance == null)
        {
            return;
        }

        Dictionary<string, int> data = GameDataManager.Instance.LoadInventory();
        items.Clear();
        unresolvedSavedItems.Clear();

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
                unresolvedSavedItems[kvp.Key] = kvp.Value;
            }
        }

        // Gold nguồn sự thật = inventory item. Migrate legacy ASTRA_CURRENCY → stack gold một lần.
        MigrateLegacyCurrencyIntoGoldStacks();
        SyncCurrencyMirrorToGameData();

        if (missingCount > 0)
        {
            Debug.LogWarning(
                $"[Inventory] Giữ lại {missingCount} item save chưa resolve được; " +
                "sẽ không ghi đè chúng khi Item Database của scene chưa đầy đủ.",
                this);
        }

        OnInventoryChanged?.Invoke();
    }

    public void SaveToGameData()
    {
        if (GameDataManager.Instance == null)
        {
            return;
        }

        if (!hasLoadedFromSave)
        {
            // Bảo hiểm cho object bị gọi Save trước Start: nếu save có item rồi,
            // không cho list runtime mặc định/rỗng phá dữ liệu cũ.
            if (GameDataManager.Instance.LoadInventory().Count > 0)
            {
                Debug.LogWarning(
                    "[Inventory] Bỏ qua Save trước khi LoadFromGameData để bảo vệ save hiện có.",
                    this);
                return;
            }
        }

        TryResolveDeferredSavedItems();

        Dictionary<string, int> data = new Dictionary<string, int>();
        foreach (var stack in items)
        {
            if (stack?.itemData == null || string.IsNullOrEmpty(stack.itemData.itemId)) continue;
            data[stack.itemData.itemId] = stack.quantity;
        }

        // Giữ lại item của save mà scene hiện tại chưa có ItemData để resolve.
        // Không có đoạn này, một lần mở scene thiếu registry có thể xóa cả inventory.
        foreach (KeyValuePair<string, int> unresolved in unresolvedSavedItems)
        {
            if (!data.ContainsKey(unresolved.Key))
            {
                data[unresolved.Key] = unresolved.Value;
            }
        }

        GameDataManager.Instance.SaveInventory(data);
        SyncCurrencyMirrorToGameData();
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
            return;
        }

        int invGold = GetQuantity(gold);
        if (invGold > 0)
        {
            // Inventory đã có gold → giữ inventory, bỏ wallet int (không cộng đúp).
            return;
        }

        items.Add(new InventoryItemStack(gold, legacy));
    }

    /// <summary>Đồng bộ field Currency của GameDataManager chỉ để mirror (UI/API cũ), không phải wallet thứ 2.</summary>
    void SyncCurrencyMirrorToGameData()
    {
        if (GameDataManager.Instance == null)
        {
            return;
        }

        ItemData gold = ResolveGoldItem();
        if (gold == null)
        {
            // Khi registry chưa sẵn sàng, GetGoldQuantity() trả 0. Không được
            // dùng số 0 tạm đó để ghi đè ASTRA_CURRENCY thật.
            return;
        }

        GameDataManager.Instance.SetCurrencyMirror(GetQuantity(gold));
    }

    public bool AddItem(ItemData itemData, int amount)
    {
        if (itemData == null)
        {
            return false;
        }

        if (amount <= 0)
        {
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

    /// <summary>Editor/demo helper. Gold vẫn đi qua inventory item duy nhất và save như giao dịch thường.</summary>
    public bool SetGoldForDebug(int targetAmount, ItemData assignedGold = null)
    {
        ItemData gold = ResolveGoldItem(assignedGold);
        if (gold == null)
        {
            return false;
        }

        int target = Mathf.Max(0, targetAmount);
        int current = GetQuantity(gold);
        if (target == current)
        {
            SyncCurrencyMirrorToGameData();
            return true;
        }

        return target > current
            ? AddItem(gold, target - current)
            : RemoveItem(gold, current - target);
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

    public bool CanUseItem(ItemData itemData)
    {
        if (itemData == null || !HasItem(itemData, 1))
        {
            return false;
        }

        return itemData.type == ItemType.Consumable || AncientMapProgression.IsMapItem(itemData);
    }

    public bool UseItem(ItemData itemData)
    {
        if (!CanUseItem(itemData))
        {
            return false;
        }

        if (itemData.type == ItemType.Consumable)
        {
            return UseConsumable(itemData);
        }

        return AncientMapProgression.TryUse(itemData);
    }

    public void EnsureCollectedAncientMapItems()
    {
        if (ensuringCollectedDocuments || GameDataManager.Instance == null)
        {
            return;
        }

        ensuringCollectedDocuments = true;
        try
        {
            TryResolveDeferredSavedItems();

            if (GameDataManager.Instance.IsAncientNoteCollected)
            {
                RestoreCollectedDocument(AncientMapProgression.ResolveMapItem());
            }

            if (GameDataManager.Instance.IsAncientNote2Collected)
            {
                RestoreCollectedDocument(AncientMapProgression.ResolveMapItem(null, true));
            }
        }
        finally
        {
            ensuringCollectedDocuments = false;
        }
    }

    private void TryResolveDeferredSavedItems()
    {
        if (GameDataManager.Instance == null || unresolvedSavedItems.Count == 0)
        {
            return;
        }

        List<string> resolvedIds = null;
        foreach (KeyValuePair<string, int> pending in unresolvedSavedItems)
        {
            ItemData item = GameDataManager.Instance.ResolveItem(pending.Key);
            if (item == null)
            {
                continue;
            }

            InventoryItemStack stack = FindStack(item);
            if (stack != null)
            {
                stack.quantity += pending.Value;
            }
            else
            {
                items.Add(new InventoryItemStack(item, pending.Value));
            }

            resolvedIds ??= new List<string>();
            resolvedIds.Add(pending.Key);
        }

        if (resolvedIds == null)
        {
            return;
        }

        for (int i = 0; i < resolvedIds.Count; i++)
        {
            unresolvedSavedItems.Remove(resolvedIds[i]);
        }

        OnInventoryChanged?.Invoke();
    }

    private void RestoreCollectedDocument(ItemData document)
    {
        if (document == null || GetQuantity(document) > 0)
        {
            return;
        }

        // Migration/safety: save cũ đã nhặt giấy vẫn nhận đúng Key Item riêng,
        // nhưng không tự mở khóa objective/route nếu người chơi chưa bấm Use.
        AddItem(document, 1);
        Debug.Log($"[AncientMap] Restored '{document.itemId}' into inventory.", this);
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
