using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ShopUIController : MonoBehaviour
{
    public static ShopUIController Active { get; private set; }

    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button closeButton;
    [SerializeField] private ShopEntryButton[] entryButtons;
    [Header("Character / Weapon Store")]
    [FormerlySerializedAs("characterScreenPrefab")]
    [SerializeField] private GameObject characterScreen;
    [FormerlySerializedAs("weaponScreenPrefab")]
    [SerializeField] private GameObject weaponScreen;
    [SerializeField] private InventoryToggleController menuController;

    ShopController activeShop;
    PlayerInventoryService inventory;
    PopupTween popupTween;
    bool featuredLayoutBuilt;
    bool isOpen;
    int closedFrame = -1;

    public bool IsOpen => isOpen;
    public bool BlocksMenuToggle => isOpen || closedFrame == Time.frameCount;

    void Awake()
    {
        Active = this;
        BuildFeaturedLayout();
        if (characterScreen != null) characterScreen.SetActive(false);
        if (weaponScreen != null) weaponScreen.SetActive(false);
        if (root != null)
        {
            popupTween = root.GetComponent<PopupTween>();
            if (popupTween == null && featuredLayoutBuilt)
            {
                popupTween = root.AddComponent<PopupTween>();
            }

            if (popupTween != null)
            {
                popupTween.SetHiddenImmediate();
            }
            else
            {
                root.SetActive(false);
            }
        }
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    void Update()
    {
        if (isOpen && Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    public void Open(ShopData data, ShopController shop)
    {
        activeShop = shop;
        inventory = FindPlayerInventory();

        EnsureStoreScreens();
        menuController?.PrepareExternalMenuPanel(root);
        isOpen = true;
        ShowTab(StoreTab.Featured);

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
        isOpen = false;
        closedFrame = Time.frameCount;

        if (characterScreen != null) characterScreen.SetActive(false);
        if (weaponScreen != null) weaponScreen.SetActive(false);

        if (root != null)
        {
            if (popupTween != null)
            {
                popupTween.Hide(FinishClose);
                return;
            }
            root.SetActive(false);
        }

        FinishClose();
    }

    void FinishClose()
    {
        menuController?.FinishExternalMenuPanel();
        Time.timeScale = 1f;
        RestoreGameplayCursor();
    }

    public void ShowTab(StoreTab tab)
    {
        if (!isOpen) return;
        EnsureStoreScreens();
        bool featured = tab == StoreTab.Featured;
        if (featured)
        {
            if (root != null)
            {
                if (popupTween != null) popupTween.Show();
                else root.SetActive(true);
            }
        }
        else if (root != null)
        {
            root.SetActive(false);
        }

        if (characterScreen != null) characterScreen.SetActive(tab == StoreTab.Character);
        if (weaponScreen != null) weaponScreen.SetActive(tab == StoreTab.Weapon);
    }

    void EnsureStoreScreens()
    {
        if (characterScreen == null || weaponScreen == null)
            Debug.LogWarning("[ShopUI] Menu_Canvas/Panels chưa được wire Character hoặc Weapon screen.", this);
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

    void BuildFeaturedLayout()
    {
        if (featuredLayoutBuilt || root == null)
        {
            return;
        }

        Transform featuredTransform = root.name == "Featured"
            ? root.transform
            : FindDirectChild(root.transform, "Featured");

        if (featuredTransform == null)
        {
            Debug.LogWarning(
                "[ShopUI] Thiếu Menu_Canvas/Panels/Featured trong Hierarchy.",
                this);
            return;
        }

        featuredLayoutBuilt = true;
        RectTransform canvasRect = root.transform as RectTransform;
        if (canvasRect != null)
        {
            canvasRect.localScale = Vector3.one;
        }

        Canvas canvas = root.GetComponent<Canvas>();
        bool hasParentCanvas = root.transform.parent != null && root.transform.parent.GetComponentInParent<Canvas>() != null;
        if (canvas == null && !hasParentCanvas)
        {
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
        }

        Image oldBackground = root.GetComponent<Image>();
        if (oldBackground != null)
        {
            oldBackground.color = Color.clear;
        }

        // Chỉ cần ẩn visual Shop cũ khi Featured còn được đặt trong một Canvas bọc.
        if (featuredTransform != root.transform)
        {
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                child.gameObject.SetActive(child == featuredTransform);
            }
        }

        GameObject featured = featuredTransform.gameObject;
        RectTransform featuredRect = featured.transform as RectTransform;
        if (featuredRect != null)
        {
            featuredRect.anchorMin = Vector2.zero;
            featuredRect.anchorMax = Vector2.one;
            featuredRect.offsetMin = Vector2.zero;
            featuredRect.offsetMax = Vector2.zero;
            featuredRect.localScale = Vector3.one;
        }

        Transform promoList = FindChild(featured.transform, "List");
        if (promoList != null)
        {
            promoList.gameObject.SetActive(true);
        }

        Transform scrollRect = FindChild(featured.transform, "ScrollRect");
        if (scrollRect != null)
        {
            // Beacon Camp dùng hai thẻ khuyến mãi có sẵn trong Featured.prefab.
            scrollRect.gameObject.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnClickClose);
            closeButton.onClick.AddListener(OnClickClose);
        }
        BuildFeaturedOfferCards(promoList);
    }

    void BuildFeaturedOfferCards(Transform promoList)
    {
        if (promoList == null)
        {
            Debug.LogWarning("[ShopUI] Featured.prefab không có List.", this);
            return;
        }

        const int visibleOfferCount = 2;
        int offerCount = Mathf.Min(visibleOfferCount, promoList.childCount);
        entryButtons = new ShopEntryButton[offerCount];

        for (int i = 0; i < promoList.childCount; i++)
        {
            Transform offer = promoList.GetChild(i);
            bool shouldShow = i < visibleOfferCount;
            offer.gameObject.SetActive(shouldShow);
            if (!shouldShow)
            {
                continue;
            }

            TMP_Text itemName = FindText(offer, "Title");
            TMP_Text quantity = FindText(offer, "Title (2)");
            TMP_Text price = FindText(offer, "Text (TMP)");
            Image icon = FindImage(offer, "Icon");
            Transform buyTransform = FindChild(offer, "Button-Orange");
            Button buyButton = buyTransform != null ? buyTransform.GetComponent<Button>() : null;

            if (itemName == null || quantity == null || price == null || buyButton == null)
            {
                Debug.LogWarning(
                    $"[ShopUI] Thẻ shop '{offer.name}' thiếu Title/Title (2)/Button-Orange.",
                    offer);
            }

            if (icon != null)
            {
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            ShopEntryButton entryButton = offer.GetComponent<ShopEntryButton>();
            if (entryButton == null) entryButton = offer.gameObject.AddComponent<ShopEntryButton>();
            entryButton.InitializeFeatured(itemName, quantity, price, buyButton, icon);
            entryButtons[i] = entryButton;
        }
    }

    static Transform FindChild(Transform rootTransform, string childName)
    {
        Transform[] children = rootTransform.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName) return children[i];
        }

        return null;
    }

    static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child;
        }

        return null;
    }

    static Image FindImage(Transform rootTransform, string childName)
    {
        Transform child = FindChild(rootTransform, childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    static TMP_Text FindText(Transform rootTransform, string childName)
    {
        Transform child = FindChild(rootTransform, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
