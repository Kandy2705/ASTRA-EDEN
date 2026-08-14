using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CharacterWeaponStoreView : MonoBehaviour
{
    [Header("Mode / Data")]
    [SerializeField] private StoreContentType contentType;
    [SerializeField] private StoreCatalogData catalog;

    [Header("List")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private StoreEntryCardView cardTemplate;

    [Header("Preview / Details")]
    [SerializeField] private SpawnLoadoutPreview preview;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text expText;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text ownershipText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;
    [SerializeField] private StoreConfirmationModal confirmation;
    [SerializeField] private HeroUpgradeToast toast;
    [SerializeField] private bool combinePurchaseInfoInDescription;

    private readonly List<StoreEntryCardView> cards = new List<StoreEntryCardView>();
    private GameDataManager data;
    private PlayerInventoryService inventory;
    private CharacterShopEntryDefinition selectedCharacter;
    private WeaponShopEntryDefinition selectedWeapon;
    private bool transactionInProgress;

    private void Awake()
    {
        if (buyButton != null) buyButton.onClick.AddListener(RequestPurchase);
        if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        data = GameDataManager.Instance;
        inventory = PlayerInventoryService.FindForPlayer();
        Subscribe();
        EnsureSelection();
        RebuildCards();
        RefreshDetails();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (confirmation != null) confirmation.Cancel();
    }

    private void OnDestroy()
    {
        if (buyButton != null) buyButton.onClick.RemoveListener(RequestPurchase);
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (data != null)
        {
            data.OnCurrencyChanged += HandleCurrencyChanged;
            data.HeroOwnershipChanged += HandleOwnershipChanged;
            data.WeaponOwnershipChanged += HandleOwnershipChanged;
        }
        if (inventory != null) inventory.OnInventoryChanged += HandleInventoryChanged;
    }

    private void Unsubscribe()
    {
        if (data != null)
        {
            data.OnCurrencyChanged -= HandleCurrencyChanged;
            data.HeroOwnershipChanged -= HandleOwnershipChanged;
            data.WeaponOwnershipChanged -= HandleOwnershipChanged;
        }
        if (inventory != null) inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    private void HandleCurrencyChanged(int _) => RefreshDetails();
    private void HandleInventoryChanged() => RefreshDetails();
    private void HandleOwnershipChanged()
    {
        RebuildCards();
        RefreshDetails();
    }

    private void EnsureSelection()
    {
        if (catalog == null) return;
        if (contentType == StoreContentType.Character)
        {
            if (selectedCharacter != null && selectedCharacter.IsAvailableInStore) return;
            IReadOnlyList<CharacterShopEntryDefinition> entries = catalog.Characters;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].IsAvailableInStore) { selectedCharacter = entries[i]; break; }
        }
        else
        {
            if (selectedWeapon != null && selectedWeapon.IsAvailableInStore) return;
            IReadOnlyList<WeaponShopEntryDefinition> entries = catalog.Weapons;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].IsAvailableInStore) { selectedWeapon = entries[i]; break; }
        }
    }

    private void RebuildCards()
    {
        for (int i = 0; i < cards.Count; i++) if (cards[i] != null) Destroy(cards[i].gameObject);
        cards.Clear();
        if (catalog == null || cardTemplate == null || contentRoot == null) return;

        if (contentType == StoreContentType.Character)
        {
            IReadOnlyList<CharacterShopEntryDefinition> entries = catalog.Characters;
            for (int i = 0; i < entries.Count; i++)
            {
                CharacterShopEntryDefinition entry = entries[i];
                if (entry == null || !entry.IsAvailableInStore) continue;
                CharacterData character = entry.Character;
                StoreEntryCardView card = Instantiate(cardTemplate, contentRoot);
                card.gameObject.SetActive(true);
                bool owned = character.IsOwned;
                card.Bind(character.Portrait, character.DisplayName, character.HeroType.ToString(), FormatRarity(character.Rarity),
                    owned, entry == selectedCharacter, () => Select(entry));
                cards.Add(card);
            }
        }
        else
        {
            IReadOnlyList<WeaponShopEntryDefinition> entries = catalog.Weapons;
            for (int i = 0; i < entries.Count; i++)
            {
                WeaponShopEntryDefinition entry = entries[i];
                if (entry == null || !entry.IsAvailableInStore) continue;
                WeaponData weapon = entry.Weapon;
                StoreEntryCardView card = Instantiate(cardTemplate, contentRoot);
                card.gameObject.SetActive(true);
                bool owned = data != null && data.IsWeaponOwned(weapon.weaponId);
                card.Bind(weapon.icon, weapon.displayName, weapon.weaponType.ToString(), string.Empty,
                    owned, entry == selectedWeapon, () => Select(entry));
                cards.Add(card);
            }
        }
    }

    private void Select(CharacterShopEntryDefinition entry)
    {
        selectedCharacter = entry;
        RebuildCards();
        RefreshDetails();
    }

    private void Select(WeaponShopEntryDefinition entry)
    {
        selectedWeapon = entry;
        RebuildCards();
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        inventory ??= PlayerInventoryService.FindForPlayer();
        int gold = inventory != null ? inventory.GetGoldQuantity() : 0;
        SetText(goldText, $"Gold: {gold:N0}");

        if (contentType == StoreContentType.Character)
        {
            CharacterData hero = selectedCharacter != null ? selectedCharacter.Character : null;
            bool owned = hero != null && hero.IsOwned;
            int price = selectedCharacter != null ? selectedCharacter.GoldPrice : 0;
            SetText(nameText, hero != null ? hero.DisplayName : "No Character");
            SetText(typeText, hero != null ? $"{hero.HeroType} Hero" : "-");
            SetText(rarityText, hero != null ? $"Rank {hero.rank}  •  {FormatRarity(hero.Rarity)}" : "-");
            string description = hero != null ? hero.Description : "-";
            SetText(descriptionText, description);
            HeroProgressData progress = owned && data != null ? data.GetHeroProgress(hero.HeroId) : null;
            SetText(expText, $"EXP  {(progress != null ? progress.experience : 0):N0}");
            SetText(killsText, $"KILLS  {(progress != null ? progress.kills : 0):N0}");
            RefreshPurchaseState(owned, price, gold, description);
            if (preview != null) preview.Show(hero, null);
        }
        else
        {
            WeaponData weapon = selectedWeapon != null ? selectedWeapon.Weapon : null;
            bool owned = weapon != null && data != null && data.IsWeaponOwned(weapon.weaponId);
            int price = selectedWeapon != null ? selectedWeapon.GoldPrice : 0;
            int level = owned && data != null ? data.GetWeaponUpgradeLevel(weapon.weaponId) : 0;
            SetText(nameText, weapon != null ? weapon.displayName : "No Weapon");
            SetText(typeText, weapon != null ? weapon.weaponType.ToString() : "-");
            SetText(rarityText, owned ? $"Upgrade Lv. {level}" : "Not owned");
            SetText(descriptionText, weapon == null ? "-" :
                $"Basic Attack  +{weapon.GetBasicAttackBonusPercent(level):P0}\nSkill Damage  +{weapon.GetSkillDamageBonusPercent(level):P0}");
            SetText(expText, string.Empty);
            SetText(killsText, string.Empty);
            RefreshPurchaseState(owned, price, gold, null);
            if (preview != null) preview.ShowWeapon(weapon);
        }
    }

    private void RefreshPurchaseState(bool owned, int price, int gold, string description)
    {
        SetText(priceText, owned ? "OWNED" : $"{price:N0} Gold");
        SetText(ownershipText, owned ? "OWNED" : "AVAILABLE");
        SetText(buyButtonText, owned ? "OWNED" : "BUY");
        if (combinePurchaseInfoInDescription && descriptionText != null)
        {
            string body = string.IsNullOrWhiteSpace(description) ? "Hero available for permanent unlock." : description;
            descriptionText.text = owned
                ? $"{body}\n\nOWNED\nGold: {gold:N0}"
                : $"{body}\n\nPrice: {price:N0} Gold\nAvailable Gold: {gold:N0}";
        }
        if (buyButton != null) buyButton.interactable = !transactionInProgress && !owned && gold >= price;
    }

    private void RequestPurchase()
    {
        if (transactionInProgress || confirmation == null) return;
        if (contentType == StoreContentType.Character && selectedCharacter != null)
        {
            confirmation.Show($"Purchase {selectedCharacter.Character.DisplayName}\nfor {selectedCharacter.GoldPrice:N0} Gold?",
                () => PurchaseCharacter(selectedCharacter));
        }
        else if (contentType == StoreContentType.Weapon && selectedWeapon != null)
        {
            confirmation.Show($"Purchase {selectedWeapon.Weapon.displayName}\nfor {selectedWeapon.GoldPrice:N0} Gold?",
                () => PurchaseWeapon(selectedWeapon));
        }
    }

    private void PurchaseCharacter(CharacterShopEntryDefinition entry)
    {
        transactionInProgress = true;
        data = GameDataManager.Instance;
        inventory ??= PlayerInventoryService.FindForPlayer();
        CharacterData hero = entry != null ? entry.Character : null;
        bool valid = entry != null && entry.IsAvailableInStore && hero != null && data != null && inventory != null &&
            !data.IsHeroOwned(hero.HeroId) && inventory.GetGoldQuantity() >= entry.GoldPrice;
        bool spent = valid && inventory.TrySpendGold(entry.GoldPrice);
        bool unlocked = spent && data.OwnHero(hero.HeroId);
        if (spent && !unlocked) Refund(entry.GoldPrice);
        if (unlocked) CompletePurchase($"{hero.DisplayName} Unlocked");
        transactionInProgress = false;
        RebuildCards();
        RefreshDetails();
    }

    private void PurchaseWeapon(WeaponShopEntryDefinition entry)
    {
        transactionInProgress = true;
        data = GameDataManager.Instance;
        inventory ??= PlayerInventoryService.FindForPlayer();
        WeaponData weapon = entry != null ? entry.Weapon : null;
        bool valid = entry != null && entry.IsAvailableInStore && weapon != null && data != null && inventory != null &&
            !data.IsWeaponOwned(weapon.weaponId) && inventory.GetGoldQuantity() >= entry.GoldPrice;
        bool spent = valid && inventory.TrySpendGold(entry.GoldPrice);
        bool unlocked = spent && data.OwnWeapon(weapon.weaponId);
        if (spent && !unlocked) Refund(entry.GoldPrice);
        if (unlocked) CompletePurchase($"{weapon.displayName} Unlocked");
        transactionInProgress = false;
        RebuildCards();
        RefreshDetails();
    }

    private void Refund(int gold)
    {
        ItemData currency = PlayerInventoryService.ResolveGoldItem();
        if (inventory != null && currency != null) inventory.AddItem(currency, gold);
    }

    private void CompletePurchase(string message)
    {
        data?.FlushPlayerPrefs();
        toast?.Show(message);
        AudioManager.EnsureInstance()?.PlayUiClick();
    }

    private static string FormatRarity(CharacterRarity rarity) => rarity switch
    {
        CharacterRarity.FiveStar => "Legendary",
        CharacterRarity.FourStar => "Epic",
        _ => "Rare"
    };

    private static void SetText(TMP_Text label, string value) { if (label != null) label.text = value; }
}
