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

    public bool TryPurchase(ShopEntry entry, PlayerInventoryService inventory)
    {
        if (entry == null || entry.item == null || inventory == null || shopData == null)
        {
            return false;
        }

        ItemData currency = shopData.currencyItem;
        if (currency == null)
        {
            Debug.LogWarning("[Shop] ShopData chưa gán currencyItem (Gold).");
            return false;
        }

        if (!inventory.HasItem(currency, entry.price))
        {
            Debug.Log("[Shop] Không đủ Gold.");
            return false;
        }

        inventory.RemoveItem(currency, entry.price);
        inventory.AddItem(entry.item, entry.quantity);
        Purchased?.Invoke();

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.FlushPlayerPrefs();
        }

        return true;
    }
}