using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopEntryButton : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Image iconImage;

    int entryIndex = -1;
    ShopUIController owner;

    public void Initialize(TMP_Text label, Button button, Image icon)
    {
        labelText = label;
        quantityText = null;
        priceText = null;
        buyButton = button;
        iconImage = icon;
    }

    public void InitializeFeatured(
        TMP_Text itemName,
        TMP_Text quantity,
        TMP_Text price,
        Button button,
        Image icon)
    {
        labelText = itemName;
        quantityText = quantity;
        priceText = price;
        buyButton = button;
        iconImage = icon;
    }

    public void Bind(int index, ShopEntry entry, ShopUIController shopUI)
    {
        entryIndex = index;
        owner = shopUI;

        if (labelText != null && entry.item != null)
        {
            labelText.text = quantityText == null && priceText == null
                ? $"{entry.item.displayName}\n<size=75%>x{entry.quantity}   •   {entry.price} Gold</size>"
                : entry.item.displayName;
        }

        if (quantityText != null)
        {
            quantityText.text = $"x{entry.quantity}";
        }

        if (priceText != null)
        {
            priceText.text = $"{entry.price} Gold";
        }

        if (iconImage != null && entry.item != null)
        {
            iconImage.sprite = entry.item.icon;
            iconImage.enabled = entry.item.icon != null;
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
        }
    }

    void OnBuyClicked()
    {
        if (owner != null && entryIndex >= 0)
        {
            owner.TryBuy(entryIndex);
        }
    }
}
