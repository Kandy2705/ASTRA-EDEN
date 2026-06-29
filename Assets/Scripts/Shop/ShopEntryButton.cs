using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopEntryButton : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Button buyButton;

    int entryIndex = -1;
    ShopUIController owner;

    public void Bind(int index, ShopEntry entry, ShopUIController shopUI)
    {
        entryIndex = index;
        owner = shopUI;

        if (labelText != null && entry.item != null)
        {
            labelText.text = $"{entry.item.displayName} x{entry.quantity} — {entry.price} Gold";
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