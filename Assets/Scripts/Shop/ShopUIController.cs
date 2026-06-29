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
        if (activeShop == null || activeShop.Data == null || inventory == null)
        {
            return;
        }

        if (index < 0 || index >= activeShop.Data.entries.Count)
        {
            return;
        }

        ShopEntry entry = activeShop.Data.entries[index];
        bool ok = activeShop.TryPurchase(entry, inventory);
        RefreshStatus();
        if (statusText != null)
        {
            statusText.text = ok
                ? $"Purchased {entry.item.displayName} x{entry.quantity}"
                : "Not enough Gold.";
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
        if (statusText == null || activeShop == null || activeShop.Data == null || inventory == null)
        {
            return;
        }

        int gold = activeShop.Data.currencyItem != null
            ? inventory.GetQuantity(activeShop.Data.currencyItem)
            : 0;
        statusText.text = $"Gold: {gold}";
    }

    static PlayerInventoryService FindPlayerInventory()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<PlayerInventoryService>() : null;
    }

    static void RestoreGameplayCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}