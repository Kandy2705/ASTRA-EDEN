using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SpawnLoadoutView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private SpawnLoadoutCatalog catalog;

    [Header("Lists")]
    [SerializeField] private Transform heroCategoryContainer;
    [SerializeField] private Transform heroGrid;
    [SerializeField] private Transform weaponFilterContainer;
    [SerializeField] private Transform weaponGrid;
    [SerializeField] private Button buttonTemplate;
    [Tooltip("Optional prefab for cards created inside OwnedHeroGrid.")]
    [SerializeField] private Button ownedHeroCardPrefab;
    [Tooltip("Optional prefab for cards created inside OwnedWeaponGrid.")]
    [SerializeField] private Button ownedWeaponCardPrefab;
    [Tooltip("Optional prefab for Infantry/Ranged/Riders/Tank/Master filter buttons.")]
    [SerializeField] private Button heroFilterButtonPrefab;
    [Tooltip("Optional prefab for All/Sword/GreatSword/Axe/Bow/Staff/Wand filter buttons.")]
    [SerializeField] private Button weaponFilterButtonPrefab;

    [Header("Details")]
    [SerializeField] private TMP_Text heroNameText;
    [SerializeField] private TMP_Text heroTypeText;
    [SerializeField] private TMP_Text heroStatsText;
    [SerializeField] private TMP_Text weaponNameText;
    [SerializeField] private TMP_Text weaponTypeText;
    [SerializeField] private TMP_Text weaponStatsText;
    [SerializeField] private TMP_Text validationText;

    [Header("Actions")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;
    [SerializeField] private SpawnLoadoutPreview preview;

    private readonly List<Button> dynamicButtons = new List<Button>();
    private GameDataManager data;
    private CharacterData selectedHero;
    private WeaponData selectedWeapon;
    private HeroType selectedHeroType;
    private WeaponType? selectedWeaponType;

    public event Action CloseRequested;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (backButton != null) backButton.onClick.AddListener(CancelCandidate);
        HideSceneTemplate(buttonTemplate);
        HideSceneTemplate(ownedHeroCardPrefab);
        HideSceneTemplate(ownedWeaponCardPrefab);
        HideSceneTemplate(heroFilterButtonPrefab);
        HideSceneTemplate(weaponFilterButtonPrefab);
    }

    private void OnEnable()
    {
        data = GameDataManager.Instance;
        Subscribe();
        ResetCandidateToCurrent();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearDynamicButtons();
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        if (backButton != null) backButton.onClick.RemoveListener(CancelCandidate);
    }

    public void ResetCandidateToCurrent()
    {
        data = GameDataManager.Instance;
        if (catalog == null || data == null) return;

        selectedHero = catalog.ResolveHero(data.CurrentHeroId);
        if (selectedHero == null)
        {
            IReadOnlyList<CharacterData> heroes = catalog.Heroes;
            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i] != null && data.IsHeroOwned(heroes[i].HeroId))
                {
                    selectedHero = heroes[i];
                    break;
                }
            }
        }

        selectedHeroType = selectedHero != null ? selectedHero.HeroType : HeroType.Infantry;
        selectedWeapon = catalog.ResolveValidWeapon(selectedHero, data.CurrentWeaponId, data);
        selectedWeaponType = null;
        RefreshAll();
    }

    public void CancelCandidate()
    {
        ResetCandidateToCurrent();
        CloseRequested?.Invoke();
    }

    private void Subscribe()
    {
        if (data == null) return;
        data.HeroOwnershipChanged -= HandleCollectionChanged;
        data.WeaponOwnershipChanged -= HandleCollectionChanged;
        data.HeroOwnershipChanged += HandleCollectionChanged;
        data.WeaponOwnershipChanged += HandleCollectionChanged;
    }

    private void Unsubscribe()
    {
        if (data == null) return;
        data.HeroOwnershipChanged -= HandleCollectionChanged;
        data.WeaponOwnershipChanged -= HandleCollectionChanged;
    }

    private void HandleCollectionChanged() => RefreshAll();

    private void RefreshAll()
    {
        ClearDynamicButtons();
        BuildHeroFilters();
        BuildHeroGrid();
        BuildWeaponFilters();
        BuildWeaponGrid();
        RefreshDetails();
    }

    private void BuildHeroFilters()
    {
        foreach (HeroType type in Enum.GetValues(typeof(HeroType)))
        {
            HeroType captured = type;
            CreateFilterButton(heroCategoryContainer, heroFilterButtonPrefab, type.ToString(), selectedHeroType == type, () =>
            {
                selectedHeroType = captured;
                RefreshAll();
            });
        }
    }

    private void BuildHeroGrid()
    {
        if (catalog == null || data == null) return;
        IReadOnlyList<CharacterData> heroes = catalog.Heroes;
        for (int i = 0; i < heroes.Count; i++)
        {
            CharacterData hero = heroes[i];
            if (hero == null || hero.HeroType != selectedHeroType || !data.IsHeroOwned(hero.HeroId)) continue;
            CharacterData captured = hero;
            CreatePrefabButton(heroGrid, ownedHeroCardPrefab, hero.DisplayName, hero == selectedHero, () => SelectHero(captured));
        }
    }

    private void BuildWeaponFilters()
    {
        CreateFilterButton(weaponFilterContainer, weaponFilterButtonPrefab, "All", !selectedWeaponType.HasValue, () =>
        {
            selectedWeaponType = null;
            RefreshAll();
        });

        WeaponType[] supported =
        {
            WeaponType.Sword, WeaponType.Greatsword, WeaponType.Axe,
            WeaponType.Bow, WeaponType.Staff, WeaponType.Wand
        };
        for (int i = 0; i < supported.Length; i++)
        {
            WeaponType captured = supported[i];
            CreateFilterButton(weaponFilterContainer, weaponFilterButtonPrefab, GetWeaponTypeLabel(captured), selectedWeaponType == captured, () =>
            {
                selectedWeaponType = captured;
                RefreshAll();
            });
        }
    }

    private void BuildWeaponGrid()
    {
        if (catalog == null || data == null || selectedHero == null) return;
        IReadOnlyList<WeaponData> weapons = catalog.Weapons;
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponData weapon = weapons[i];
            if (!catalog.IsAvailableForHero(selectedHero, weapon, data) ||
                (selectedWeaponType.HasValue && weapon.weaponType != selectedWeaponType.Value)) continue;
            WeaponData captured = weapon;
            CreatePrefabButton(weaponGrid, ownedWeaponCardPrefab, weapon.displayName, weapon == selectedWeapon, () => SelectWeapon(captured));
        }
    }

    private void SelectHero(CharacterData hero)
    {
        selectedHero = hero;
        if (!catalog.IsAvailableForHero(selectedHero, selectedWeapon, data))
        {
            selectedWeapon = catalog.ResolveValidWeapon(selectedHero, null, data);
        }
        RefreshAll();
    }

    private void SelectWeapon(WeaponData weapon)
    {
        selectedWeapon = weapon;
        RefreshAll();
    }

    private void RefreshDetails()
    {
        SetText(heroNameText, selectedHero != null ? selectedHero.DisplayName : "No owned Hero");
        SetText(heroTypeText, selectedHero != null ? selectedHero.HeroType.ToString() : "-");
        if (selectedHero != null && data != null)
        {
            SetText(heroStatsText,
                $"HP  {data.GetHeroFinalStat(selectedHero, HeroStatType.Health):0}\n" +
                $"ATK  {data.GetHeroFinalStat(selectedHero, HeroStatType.Damage):0.#}\n" +
                $"DEF  {data.GetHeroFinalStat(selectedHero, HeroStatType.Defense):0.#}\n" +
                $"SPD  {data.GetHeroFinalStat(selectedHero, HeroStatType.MoveSpeed):0.#}\n" +
                $"MANA {data.GetHeroFinalStat(selectedHero, HeroStatType.Mana):0}");
        }
        else SetText(heroStatsText, "-");

        SetText(weaponNameText, selectedWeapon != null ? selectedWeapon.displayName : "Select a compatible Weapon");
        SetText(weaponTypeText, selectedWeapon != null ? GetWeaponTypeLabel(selectedWeapon.weaponType) : "-");
        int level = selectedWeapon != null && data != null ? data.GetWeaponUpgradeLevel(selectedWeapon.weaponId) : 0;
        SetText(weaponStatsText, selectedWeapon == null ? "-" :
            $"Basic Attack  +{selectedWeapon.GetBasicAttackBonusPercent(level):P0}\n" +
            $"Skill Damage  +{selectedWeapon.GetSkillDamageBonusPercent(level):P0}\n" +
            $"Upgrade Lv. {level}");

        bool valid = selectedHero != null && selectedWeapon != null && data != null &&
            data.IsHeroOwned(selectedHero.HeroId) && catalog.IsAvailableForHero(selectedHero, selectedWeapon, data);
        if (confirmButton != null) confirmButton.interactable = valid;
        SetText(validationText, valid ? "Ready to deploy" : "Choose an owned compatible loadout");
        if (preview != null) preview.Show(selectedHero, selectedWeapon);
    }

    private void Confirm()
    {
        PlayerLoadoutRuntime runtime = PlayerLoadoutRuntime.Active;
        if (runtime == null || !runtime.ConfirmLoadout(selectedHero, selectedWeapon))
        {
            SetText(validationText, "Unable to apply this loadout");
            return;
        }

        AudioManager.EnsureInstance()?.PlayUiClick();
        CloseRequested?.Invoke();
    }

    private Button CreateFilterButton(
        Transform parent,
        Button filterPrefab,
        string label,
        bool selected,
        UnityEngine.Events.UnityAction action)
    {
        return CreatePrefabButton(parent, filterPrefab, label, selected, action);
    }

    private Button CreatePrefabButton(
        Transform parent,
        Button prefab,
        string label,
        bool selected,
        UnityEngine.Events.UnityAction action)
    {
        Button source = prefab != null ? prefab : buttonTemplate;
        if (parent == null || source == null) return null;

        Button button = Instantiate(source, parent, false);
        button.gameObject.SetActive(true);
        button.name = $"Button_{label}";
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
        button.interactable = !selected;
        button.onClick.AddListener(action);
        dynamicButtons.Add(button);
        return button;
    }

    private static void HideSceneTemplate(Button template)
    {
        if (template != null && template.gameObject.scene.IsValid()) template.gameObject.SetActive(false);
    }

    private void ClearDynamicButtons()
    {
        for (int i = 0; i < dynamicButtons.Count; i++)
            if (dynamicButtons[i] != null) Destroy(dynamicButtons[i].gameObject);
        dynamicButtons.Clear();
    }

    private static string GetWeaponTypeLabel(WeaponType type) => type == WeaponType.Greatsword ? "GreatSword" : type.ToString();
    private static void SetText(TMP_Text target, string value) { if (target != null) target.text = value; }
}
