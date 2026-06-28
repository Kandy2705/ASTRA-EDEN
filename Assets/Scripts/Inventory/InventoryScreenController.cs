using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum InventoryFilterTab
{
    All,
    Materials,
    Consumables,
    Quest,
    KeyItems
}

[DisallowMultipleComponent]
public class InventoryScreenController : MonoBehaviour
{
    [Header("Inventory Source")]
    [SerializeField] private PlayerInventoryService inventoryService;
    [SerializeField] private int inventoryCapacity = 30;
    [SerializeField] private bool hideCurrencyInGrid = true;

    [Header("Currency")]
    [SerializeField] private ItemData goldItem;
    [SerializeField] private TMP_Text goldAmountText;

    [Tooltip("Các item được tính chung vào tổng Core, ví dụ Core Fragment, Rare Core Shard, Core Dust, Core Cell.")]
    [SerializeField] private List<ItemData> coreItems = new List<ItemData>();
    [SerializeField] private TMP_Text coreAmountText;

    [Header("Grid")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private InventorySlotUI slotTemplate;
    [SerializeField] private bool hideExistingContentChildren = true;

    [Header("Counter")]
    [SerializeField] private TMP_Text filterTitleText;
    [SerializeField] private TMP_Text itemCountText;
    [SerializeField] private TMP_Text capacityText;

    [Header("Tabs")]
    [SerializeField] private Button tabAllButton;
    [SerializeField] private Button tabMaterialsButton;
    [SerializeField] private Button tabConsumablesButton;
    [SerializeField] private Button tabQuestButton;
    [SerializeField] private Button tabKeyItemsButton;

    [Header("Detail")]
    [SerializeField] private Image selectedIconImage;
    [SerializeField] private TMP_Text selectedNameText;
    [SerializeField] private TMP_Text selectedTypeText;
    [SerializeField] private TMP_Text selectedRarityText;
    [SerializeField] private TMP_Text selectedOwnedText;
    [SerializeField] private TMP_Text selectedDescriptionText;
    [SerializeField] private List<GameObject> rarityStars = new List<GameObject>();

    [Header("Consumable Stats")]
    [SerializeField] private TMP_Text restoreHpText;
    [SerializeField] private TMP_Text restoreStaminaText;
    [SerializeField] private TMP_Text restoreEnergyText;

    [Header("Actions")]
    [SerializeField] private Button useButton;
    [SerializeField] private Button dropButton;
    [SerializeField] private Button closeButton;

    [Tooltip("Kéo object có InventoryToggleController vào đây nếu muốn nút Close/Back đóng Inventory đúng state.")]
    [SerializeField] private InventoryToggleController inventoryToggleController;

    private readonly List<InventorySlotUI> spawnedSlots = new List<InventorySlotUI>();

    private InventoryFilterTab currentFilter = InventoryFilterTab.All;
    private ItemData selectedItemData;

    private void Awake()
    {
        if (inventoryService == null)
        {
            inventoryService = FindFirstObjectByType<PlayerInventoryService>();
        }

        PrepareTemplate();
        HideExistingChildrenExceptTemplate();
        HookButtons();
    }

    private void OnEnable()
    {
        if (inventoryService == null)
        {
            inventoryService = FindFirstObjectByType<PlayerInventoryService>();
        }

        if (inventoryService != null)
        {
            inventoryService.OnInventoryChanged += RefreshNow;
        }
    }

    private void OnDisable()
    {
        if (inventoryService != null)
        {
            inventoryService.OnInventoryChanged -= RefreshNow;
        }
    }

    private void OnDestroy()
    {
        UnhookButtons();
    }

    public void RefreshNow()
    {
        if (inventoryService == null)
        {
            inventoryService = FindFirstObjectByType<PlayerInventoryService>();
        }

        Debug.Log("[InventoryUI] RefreshNow called.");
        Refresh();
    }

    public void SetFilterAll()
    {
        SetFilter(InventoryFilterTab.All);
    }

    public void SetFilterMaterials()
    {
        SetFilter(InventoryFilterTab.Materials);
    }

    public void SetFilterConsumables()
    {
        SetFilter(InventoryFilterTab.Consumables);
    }

    public void SetFilterQuest()
    {
        SetFilter(InventoryFilterTab.Quest);
    }

    public void SetFilterKeyItems()
    {
        SetFilter(InventoryFilterTab.KeyItems);
    }

    private void SetFilter(InventoryFilterTab filter)
    {
        currentFilter = filter;
        selectedItemData = null;
        RefreshNow();
    }

    private void Refresh()
    {
        if (inventoryService == null || contentRoot == null || slotTemplate == null)
        {
            ClearDetail();
            return;
        }

        UpdateCurrencyTexts();
        ClearSpawnedSlots();

        List<InventoryItemStack> visibleStacks = GetVisibleStacks();
        UpdateCounter(visibleStacks.Count);

        if (selectedItemData == null || !ContainsItem(visibleStacks, selectedItemData))
        {
            selectedItemData = visibleStacks.Count > 0 ? visibleStacks[0].itemData : null;
        }

        for (int i = 0; i < visibleStacks.Count; i++)
        {
            InventoryItemStack stack = visibleStacks[i];

            InventorySlotUI slot = Instantiate(slotTemplate, contentRoot);
            slot.gameObject.name = $"Slot_{stack.itemData.itemId}";
            slot.gameObject.SetActive(true);

            slot.Setup(stack, SelectItem);
            slot.SetSelected(stack.itemData == selectedItemData);

            spawnedSlots.Add(slot);

            Debug.Log($"[InventoryUI] Spawn slot: {stack.itemData.displayName} x {stack.quantity}");
        }

        Canvas.ForceUpdateCanvases();

        if (contentRoot is RectTransform contentRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        RenderDetail();
    }

    private void SelectItem(ItemData itemData)
    {
        selectedItemData = itemData;

        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            InventorySlotUI slot = spawnedSlots[i];

            if (slot == null)
            {
                continue;
            }

            slot.SetSelected(slot.ItemData == selectedItemData);
        }

        RenderDetail();
    }

    private List<InventoryItemStack> GetVisibleStacks()
    {
        List<InventoryItemStack> result = new List<InventoryItemStack>();

        if (inventoryService == null)
        {
            return result;
        }

        IReadOnlyList<InventoryItemStack> stacks = inventoryService.Items;

        for (int i = 0; i < stacks.Count; i++)
        {
            InventoryItemStack stack = stacks[i];

            if (stack == null || stack.itemData == null || stack.quantity <= 0)
            {
                continue;
            }

            if (PassesFilter(stack.itemData))
            {
                result.Add(stack);
            }
        }

        return result;
    }

    private bool PassesFilter(ItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        if (hideCurrencyInGrid && itemData.type == ItemType.Currency)
        {
            return false;
        }

        switch (currentFilter)
        {
            case InventoryFilterTab.All:
                return true;

            case InventoryFilterTab.Materials:
                return itemData.type == ItemType.Material ||
                       itemData.type == ItemType.UpgradeMaterial ||
                       itemData.type == ItemType.BossDrop;

            case InventoryFilterTab.Consumables:
                return itemData.type == ItemType.Consumable;

            case InventoryFilterTab.Quest:
                return itemData.type == ItemType.KeyItem;

            case InventoryFilterTab.KeyItems:
                return itemData.type == ItemType.GachaTicket ||
                       itemData.type == ItemType.KeyItem;

            default:
                return true;
        }
    }

    private bool ContainsItem(List<InventoryItemStack> stacks, ItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        for (int i = 0; i < stacks.Count; i++)
        {
            InventoryItemStack stack = stacks[i];

            if (stack == null || stack.itemData == null)
            {
                continue;
            }

            if (stack.itemData == itemData)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(itemData.itemId) &&
                stack.itemData.itemId == itemData.itemId)
            {
                return true;
            }
        }

        return false;
    }

    private void RenderDetail()
    {
        if (selectedItemData == null || inventoryService == null)
        {
            ClearDetail();
            return;
        }

        int owned = inventoryService.GetQuantity(selectedItemData);

        if (selectedIconImage != null)
        {
            selectedIconImage.sprite = selectedItemData.icon;
            selectedIconImage.enabled = selectedItemData.icon != null;
        }

        if (selectedNameText != null)
        {
            selectedNameText.text = string.IsNullOrEmpty(selectedItemData.displayName)
                ? selectedItemData.name
                : selectedItemData.displayName;
        }

        if (selectedTypeText != null)
        {
            selectedTypeText.text = $"Type: {selectedItemData.type}";
        }

        if (selectedRarityText != null)
        {
            selectedRarityText.text = $"Rarity: {selectedItemData.rarity}";
        }

        if (selectedOwnedText != null)
        {
            selectedOwnedText.text = $"Owned: {owned}";
        }

        if (selectedDescriptionText != null)
        {
            selectedDescriptionText.text = selectedItemData.description;
        }

        UpdateRarityStars(selectedItemData.rarity);
        UpdateConsumableStats(selectedItemData);

        if (useButton != null)
        {
            useButton.interactable = selectedItemData.type == ItemType.Consumable && owned > 0;
        }

        if (dropButton != null)
        {
            dropButton.interactable = owned > 0 && selectedItemData.type != ItemType.Currency;
        }
    }

    private void ClearDetail()
    {
        if (selectedIconImage != null)
        {
            selectedIconImage.sprite = null;
            selectedIconImage.enabled = false;
        }

        if (selectedNameText != null)
        {
            selectedNameText.text = "-";
        }

        if (selectedTypeText != null)
        {
            selectedTypeText.text = string.Empty;
        }

        if (selectedRarityText != null)
        {
            selectedRarityText.text = string.Empty;
        }

        if (selectedOwnedText != null)
        {
            selectedOwnedText.text = "Owned: 0";
        }

        if (selectedDescriptionText != null)
        {
            selectedDescriptionText.text = string.Empty;
        }

        UpdateRarityStars(ItemRarity.Common);
        SetTextVisible(restoreHpText, false, string.Empty);
        SetTextVisible(restoreStaminaText, false, string.Empty);
        SetTextVisible(restoreEnergyText, false, string.Empty);

        if (useButton != null)
        {
            useButton.interactable = false;
        }

        if (dropButton != null)
        {
            dropButton.interactable = false;
        }
    }

    private void UpdateConsumableStats(ItemData itemData)
    {
        bool isConsumable = itemData != null && itemData.type == ItemType.Consumable;

        SetTextVisible(
            restoreHpText,
            isConsumable && itemData.restoreHP > 0f,
            $"HP: +{itemData.restoreHP:0}"
        );

        SetTextVisible(
            restoreStaminaText,
            isConsumable && itemData.restoreStamina > 0f,
            $"Stamina: +{itemData.restoreStamina:0}"
        );

        SetTextVisible(
            restoreEnergyText,
            isConsumable && itemData.restoreEnergy > 0f,
            $"Energy: +{itemData.restoreEnergy:0}"
        );
    }

    private void UpdateRarityStars(ItemRarity rarity)
    {
        int starCount = RarityToStarCount(rarity);

        for (int i = 0; i < rarityStars.Count; i++)
        {
            if (rarityStars[i] != null)
            {
                rarityStars[i].SetActive(i < starCount);
            }
        }
    }

    private int RarityToStarCount(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common:
                return 1;

            case ItemRarity.Uncommon:
                return 2;

            case ItemRarity.Rare:
                return 3;

            case ItemRarity.Epic:
                return 4;

            case ItemRarity.Legendary:
                return 5;

            default:
                return 1;
        }
    }

    private void UpdateCurrencyTexts()
    {
        if (inventoryService == null)
        {
            return;
        }

        if (goldAmountText != null && goldItem != null)
        {
            goldAmountText.text = inventoryService.GetQuantity(goldItem).ToString("N0");
        }

        if (coreAmountText != null)
        {
            int totalCoreAmount = GetTotalQuantity(coreItems);
            coreAmountText.text = totalCoreAmount.ToString("N0");
        }
    }

    private int GetTotalQuantity(List<ItemData> itemList)
    {
        if (itemList == null || inventoryService == null)
        {
            return 0;
        }

        int total = 0;

        for (int i = 0; i < itemList.Count; i++)
        {
            ItemData item = itemList[i];

            if (item == null)
            {
                continue;
            }

            total += inventoryService.GetQuantity(item);
        }

        return total;
    }

    private void UpdateCounter(int visibleCount)
    {
        if (filterTitleText != null)
        {
            filterTitleText.text = $"{GetFilterTitle()}:";
        }

        if (itemCountText != null)
        {
            itemCountText.text = visibleCount.ToString();
        }

        if (capacityText != null)
        {
            capacityText.text = inventoryCapacity.ToString();
        }
    }

    private string GetFilterTitle()
    {
        switch (currentFilter)
        {
            case InventoryFilterTab.Materials:
                return "Materials";

            case InventoryFilterTab.Consumables:
                return "Consumables";

            case InventoryFilterTab.Quest:
                return "Quest";

            case InventoryFilterTab.KeyItems:
                return "Key Items";

            default:
                return "All";
        }
    }

    private void HandleUseClicked()
    {
        if (selectedItemData == null || inventoryService == null)
        {
            return;
        }

        bool used = inventoryService.UseConsumable(selectedItemData);

        if (!used)
        {
            RefreshNow();
        }
    }

    private void HandleDropClicked()
    {
        if (selectedItemData == null || inventoryService == null)
        {
            return;
        }

        bool removed = inventoryService.RemoveItem(selectedItemData, 1);

        if (!removed)
        {
            RefreshNow();
        }
    }

    private void HandleCloseClicked()
    {
        if (inventoryToggleController != null)
        {
            inventoryToggleController.CloseInventory();
            return;
        }

        Debug.LogWarning("[InventoryUI] Close button clicked, but InventoryToggleController is not assigned.");
    }

    private void ClearSpawnedSlots()
    {
        for (int i = spawnedSlots.Count - 1; i >= 0; i--)
        {
            if (spawnedSlots[i] != null)
            {
                Destroy(spawnedSlots[i].gameObject);
            }
        }

        spawnedSlots.Clear();
    }

    private void PrepareTemplate()
    {
        if (slotTemplate != null)
        {
            slotTemplate.gameObject.SetActive(false);
        }
    }

    private void HideExistingChildrenExceptTemplate()
    {
        if (!hideExistingContentChildren || contentRoot == null || slotTemplate == null)
        {
            return;
        }

        for (int i = 0; i < contentRoot.childCount; i++)
        {
            Transform child = contentRoot.GetChild(i);

            if (child == slotTemplate.transform)
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    private void HookButtons()
    {
        if (tabAllButton != null)
        {
            tabAllButton.onClick.AddListener(SetFilterAll);
        }

        if (tabMaterialsButton != null)
        {
            tabMaterialsButton.onClick.AddListener(SetFilterMaterials);
        }

        if (tabConsumablesButton != null)
        {
            tabConsumablesButton.onClick.AddListener(SetFilterConsumables);
        }

        if (tabQuestButton != null)
        {
            tabQuestButton.onClick.AddListener(SetFilterQuest);
        }

        if (tabKeyItemsButton != null)
        {
            tabKeyItemsButton.onClick.AddListener(SetFilterKeyItems);
        }

        if (useButton != null)
        {
            useButton.onClick.AddListener(HandleUseClicked);
        }

        if (dropButton != null)
        {
            dropButton.onClick.AddListener(HandleDropClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
        }
    }

    private void UnhookButtons()
    {
        if (tabAllButton != null)
        {
            tabAllButton.onClick.RemoveListener(SetFilterAll);
        }

        if (tabMaterialsButton != null)
        {
            tabMaterialsButton.onClick.RemoveListener(SetFilterMaterials);
        }

        if (tabConsumablesButton != null)
        {
            tabConsumablesButton.onClick.RemoveListener(SetFilterConsumables);
        }

        if (tabQuestButton != null)
        {
            tabQuestButton.onClick.RemoveListener(SetFilterQuest);
        }

        if (tabKeyItemsButton != null)
        {
            tabKeyItemsButton.onClick.RemoveListener(SetFilterKeyItems);
        }

        if (useButton != null)
        {
            useButton.onClick.RemoveListener(HandleUseClicked);
        }

        if (dropButton != null)
        {
            dropButton.onClick.RemoveListener(HandleDropClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
        }
    }

    private void SetTextVisible(TMP_Text text, bool isVisible, string value)
    {
        if (text == null)
        {
            return;
        }

        text.gameObject.SetActive(isVisible);
        text.text = value;
    }
}