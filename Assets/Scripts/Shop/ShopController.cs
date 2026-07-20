using System;
using UnityEngine;

public class ShopController : MonoBehaviour
{
    public static ShopController Instance { get; private set; }

    [SerializeField] private ShopData shopData;
    [SerializeField] private ShopUIController shopUI;

    public event Action Purchased;

    public ShopData Data => shopData;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void OpenShop()
    {
        if (shopData == null)
        {
            Debug.LogWarning("[Shop] Chưa gán ShopData.");
            return;
        }

        shopUI?.Open(shopData, this);
    }

    public void CloseShop()
    {
        shopUI?.Close();
    }

    public enum PurchaseFailReason
    {
        None,
        InvalidEntry,
        NoInventory,
        NoCurrencyItem,
        NotEnoughGold,
    }

    public bool TryPurchase(ShopEntry entry, PlayerInventoryService inventory)
    {
        return TryPurchase(entry, inventory, out _);
    }

    public bool TryPurchase(ShopEntry entry, PlayerInventoryService inventory, out PurchaseFailReason failReason)
    {
        failReason = PurchaseFailReason.None;

        if (entry == null || entry.item == null || shopData == null)
        {
            failReason = PurchaseFailReason.InvalidEntry;
            return false;
        }

        if (inventory == null)
        {
            failReason = PurchaseFailReason.NoInventory;
            Debug.LogWarning("[Shop] Không tìm thấy PlayerInventoryService trên Player.");
            return false;
        }

        ItemData currency = shopData.currencyItem != null
            ? shopData.currencyItem
            : PlayerInventoryService.ResolveGoldItem();

        if (currency == null)
        {
            failReason = PurchaseFailReason.NoCurrencyItem;
            Debug.LogWarning("[Shop] ShopData chưa gán currencyItem (Gold) và không resolve được SO_Item_Gold.");
            return false;
        }

        if (!inventory.HasItem(currency, entry.price))
        {
            failReason = PurchaseFailReason.NotEnoughGold;
            Debug.Log($"[Shop] Không đủ Gold (have={inventory.GetQuantity(currency)}, need={entry.price}).");
            return false;
        }

        if (!inventory.RemoveItem(currency, entry.price))
        {
            failReason = PurchaseFailReason.NotEnoughGold;
            return false;
        }

        inventory.AddItem(entry.item, entry.quantity);
        Purchased?.Invoke();

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.FlushPlayerPrefs();
        }

        return true;
    }
}