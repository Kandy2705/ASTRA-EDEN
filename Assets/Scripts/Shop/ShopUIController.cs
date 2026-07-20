using TMPro;
using UnityEngine;

public class ShopUIController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private ShopEntryButton[] entryButtons;

    ShopController activeShop;
    PlayerInventoryService inventory;

    void Awake()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    public void Open(ShopData data, ShopController shop)
    {
        activeShop = shop;
        inventory = FindPlayerInventory();

        if (root != null)
        {
            root.SetActive(true);
        }

        if (titleText != null)
        {
            titleText.text = data != null ? data.shopName : "Shop";
        }

        RefreshStatus();
        BindEntries(data);
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Close()
    {
        activeShop = null;

        if (root != null)
        {
            root.SetActive(false);
        }

        Time.timeScale = 1f;
        RestoreGameplayCursor();
    }

    public void OnClickClose()
    {
        Close();
    }

    public void TryBuy(int index)
    {
        if (activeShop == null || activeShop.Data == null)
        {
            return;
        }

        // Tìm lại inventory mỗi lần mua — phòng camp load player trễ / scene swap.
        if (inventory == null)
        {
            inventory = FindPlayerInventory();
        }

        if (inventory == null)
        {
            if (statusText != null)
            {
                statusText.text = "No player inventory.";
            }

            return;
        }

        if (index < 0 || index >= activeShop.Data.entries.Count)
        {
            return;
        }

        ShopEntry entry = activeShop.Data.entries[index];
        bool ok = activeShop.TryPurchase(entry, inventory, out ShopController.PurchaseFailReason reason);
        RefreshStatus();
        if (statusText != null)
        {
            statusText.text = ok
                ? $"Purchased {entry.item.displayName} x{entry.quantity}"
                : FailMessage(reason, entry);
        }
    }

    static string FailMessage(ShopController.PurchaseFailReason reason, ShopEntry entry)
    {
        switch (reason)
        {
            case ShopController.PurchaseFailReason.NotEnoughGold:
                return "Not enough Gold.";
            case ShopController.PurchaseFailReason.NoInventory:
                return "No player inventory.";
            case ShopController.PurchaseFailReason.NoCurrencyItem:
                return "Shop currency not configured.";
            case ShopController.PurchaseFailReason.InvalidEntry:
                return "Invalid shop entry.";
            default:
                return entry != null && entry.item != null
                    ? $"Cannot buy {entry.item.displayName}."
                    : "Purchase failed.";
        }
    }

    void BindEntries(ShopData data)
    {
        if (entryButtons == null || data == null)
        {
            return;
        }

        for (int i = 0; i < entryButtons.Length; i++)
        {
            ShopEntryButton button = entryButtons[i];
            if (button == null) continue;

            if (i < data.entries.Count && data.entries[i].item != null)
            {
                button.gameObject.SetActive(true);
                button.Bind(i, data.entries[i], this);
            }
            else
            {
                button.gameObject.SetActive(false);
            }
        }
    }

    void RefreshStatus()
    {
        if (statusText == null || activeShop == null)
        {
            return;
        }

        if (inventory == null)
        {
            inventory = FindPlayerInventory();
        }

        if (inventory == null)
        {
            statusText.text = "Gold: —";
            return;
        }

        ItemData currency = activeShop.Data != null
            ? (activeShop.Data.currencyItem != null
                ? activeShop.Data.currencyItem
                : PlayerInventoryService.ResolveGoldItem())
            : PlayerInventoryService.ResolveGoldItem();

        int gold = currency != null
            ? inventory.GetQuantity(currency)
            : inventory.GetGoldQuantity();
        statusText.text = $"Gold: {gold}";
    }

    static PlayerInventoryService FindPlayerInventory()
    {
        return PlayerInventoryService.FindForPlayer();
    }

    static void RestoreGameplayCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}