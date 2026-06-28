using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InventorySlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject selectedRoot;

    private ItemData itemData;
    private Action<ItemData> clickedCallback;

    public ItemData ItemData => itemData;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    public void Setup(InventoryItemStack stack, Action<ItemData> onClicked)
    {
        if (stack == null || stack.itemData == null)
        {
            Clear();
            return;
        }

        itemData = stack.itemData;
        clickedCallback = onClicked;

        if (iconImage != null)
        {
            iconImage.sprite = itemData.icon;
            iconImage.enabled = itemData.icon != null;
        }

        if (quantityText != null)
        {
            quantityText.text = stack.quantity.ToString();
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(itemData.displayName)
                ? itemData.name
                : itemData.displayName;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }

        SetSelected(false);
        gameObject.SetActive(true);
    }

    public void SetSelected(bool isSelected)
    {
        if (selectedRoot != null)
        {
            selectedRoot.SetActive(isSelected);
        }
    }

    private void Clear()
    {
        itemData = null;
        clickedCallback = null;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (quantityText != null)
        {
            quantityText.text = string.Empty;
        }

        if (nameText != null)
        {
            nameText.text = string.Empty;
        }

        SetSelected(false);
    }

    private void HandleClicked()
    {
        if (itemData == null)
        {
            return;
        }

        clickedCallback?.Invoke(itemData);
    }
}