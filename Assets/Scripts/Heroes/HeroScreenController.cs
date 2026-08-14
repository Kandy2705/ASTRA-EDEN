using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HeroScreenController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private SpawnLoadoutCatalog heroCatalog;
    [Tooltip("Legacy fallback used only when no shared Spawn Loadout catalog is assigned.")]
    [SerializeField] private List<CharacterData> heroDefinitions = new List<CharacterData>();

    [Header("Existing Hero.prefab Roots")]
    [SerializeField] private Transform heroGrid;
    [SerializeField] private Transform categoryRoot;
    [SerializeField] private Transform statsRoot;
    [SerializeField] private Transform middleContent;
    [SerializeField] private Transform rightContent;

    private readonly List<CardBinding> cards = new List<CardBinding>();
    private readonly List<CategoryBinding> categories = new List<CategoryBinding>();
    private readonly Dictionary<HeroStatType, StatBinding> stats =
        new Dictionary<HeroStatType, StatBinding>();

    private HeroType selectedCategory = HeroType.Infantry;
    private CharacterData selectedHero;
    private HeroStatType? selectedStat;
    private GameDataManager data;
    private HeroUpgradeToast toast;
    private bool cached;
    private bool suppressProgressRefresh;
    private Coroutine upgradeAnimation;

    private TMP_Text middleHeroName;
    private TMP_Text previewText;
    private TMP_Text pointsLabel;
    private TMP_Text pointsValue;
    private TMP_Text upgradeLevelLabel;
    private TMP_Text upgradeLevelValue;
    private Button upgradeButton;
    private TMP_Text detailHeroName;
    private TMP_Text detailCategory;
    private TMP_Text detailDescription;

    private sealed class CardBinding
    {
        public GameObject root;
        public Button button;
        public Graphic selectionGraphic;
        public Color normalColor;
        public TMP_Text nameLabel;
        public Image portrait;
        public string heroId;
    }

    private sealed class CategoryBinding
    {
        public HeroType type;
        public Button button;
        public TMP_Text label;
        public Color normalColor;
    }

    private sealed class StatBinding
    {
        public HeroStatType type;
        public GameObject root;
        public Button button;
        public TMP_Text label;
        public TMP_Text value;
        public Slider bar;
        public Color normalLabelColor;
    }

    private void Awake()
    {
        CacheView();
    }

    private void OnEnable()
    {
        CacheView();
        BindData();
        RefreshHeroGrid();
        RefreshSelectedHero();
    }

    private void OnDisable()
    {
        upgradeAnimation = null;
        UnbindData();
    }

    private void CacheView()
    {
        if (cached)
        {
            return;
        }

        cached = true;
        CacheCards();
        CacheCategories();
        CacheStats();
        CacheMiddlePanel();
        CacheDetailsPanel();

        toast = GetComponent<HeroUpgradeToast>();
        if (toast == null)
        {
            toast = gameObject.AddComponent<HeroUpgradeToast>();
        }
    }

    private void BindData()
    {
        GameDataManager current = GameDataManager.Instance;
        if (data == current)
        {
            return;
        }

        UnbindData();
        data = current;
        if (data != null)
        {
            data.HeroProgressChanged += HandleProgressChanged;
            data.HeroOwnershipChanged += HandleOwnershipChanged;
        }
    }

    private void UnbindData()
    {
        if (data == null)
        {
            return;
        }

        data.HeroProgressChanged -= HandleProgressChanged;
        data.HeroOwnershipChanged -= HandleOwnershipChanged;
        data = null;
    }

    private void HandleProgressChanged()
    {
        if (!suppressProgressRefresh)
        {
            RefreshSelectedHero();
        }
    }

    private void HandleOwnershipChanged()
    {
        RefreshHeroGrid();
        RefreshSelectedHero();
    }

    private void CacheCards()
    {
        if (heroGrid == null)
        {
            Debug.LogError("[HeroScreen] Hero grid reference is missing.", this);
            return;
        }

        for (int i = 0; i < heroGrid.childCount; i++)
        {
            cards.Add(CreateCardBinding(heroGrid.GetChild(i).gameObject));
        }
    }

    private CardBinding CreateCardBinding(GameObject root)
    {
        Button button = root.GetComponent<Button>();
        if (button == null)
        {
            button = root.AddComponent<Button>();
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        Image portrait = FindNamedImage(images, "portrait", "character", "icon");
        if (portrait == null && images.Length > 0)
        {
            portrait = images[images.Length - 1];
        }

        Graphic graphic = root.GetComponent<Graphic>();
        if (graphic == null && images.Length > 0)
        {
            graphic = images[0];
        }

        button.targetGraphic = graphic;
        TMP_Text nameLabel = FindNamedText(root.GetComponentsInChildren<TMP_Text>(true), "name", "title");
        return new CardBinding
        {
            root = root,
            button = button,
            selectionGraphic = graphic,
            normalColor = graphic != null ? graphic.color : Color.white,
            nameLabel = nameLabel,
            portrait = portrait
        };
    }

    private void CacheCategories()
    {
        if (categoryRoot == null)
        {
            Debug.LogError("[HeroScreen] Category root reference is missing.", this);
            return;
        }

        TMP_Text[] labels = categoryRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (!TryParseHeroType(labels[i].text, out HeroType type))
            {
                continue;
            }

            TMP_Text label = labels[i];
            if (type == HeroType.Infantry)
            {
                label.text = "Infantry";
            }

            Button button = label.GetComponent<Button>();
            if (button == null)
            {
                button = label.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = label;
            HeroType capturedType = type;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectCategory(capturedType));
            categories.Add(new CategoryBinding
            {
                type = type,
                button = button,
                label = label,
                normalColor = label.color
            });
        }
    }

    private void CacheStats()
    {
        if (statsRoot == null)
        {
            Debug.LogError("[HeroScreen] Stats root reference is missing.", this);
            return;
        }

        TMP_Text[] labels = statsRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (!TryParseStat(labels[i].text, out HeroStatType statType) || stats.ContainsKey(statType))
            {
                continue;
            }

            Transform row = FindStatRow(labels[i].transform);
            if (row == null)
            {
                continue;
            }

            TMP_Text value = null;
            TMP_Text[] rowTexts = row.GetComponentsInChildren<TMP_Text>(true);
            for (int j = 0; j < rowTexts.Length; j++)
            {
                if (rowTexts[j] != labels[i])
                {
                    value = rowTexts[j];
                }
            }

            Slider bar = row.GetComponentInChildren<Slider>(true);
            if (bar != null)
            {
                bar.interactable = false;
            }

            Button button = row.GetComponent<Button>();
            if (button == null)
            {
                button = row.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = labels[i];
            HeroStatType capturedType = statType;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectStat(capturedType));
            stats.Add(statType, new StatBinding
            {
                type = statType,
                root = row.gameObject,
                button = button,
                label = labels[i],
                value = value,
                bar = bar,
                normalLabelColor = labels[i].color
            });
        }
    }

    private Transform FindStatRow(Transform label)
    {
        Transform current = label;
        while (current != null && current != statsRoot)
        {
            if (current.GetComponentInChildren<Slider>(true) != null)
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private void CacheMiddlePanel()
    {
        if (middleContent == null)
        {
            Debug.LogError("[HeroScreen] Middle content reference is missing.", this);
            return;
        }

        TMP_Text[] texts = middleContent.GetComponentsInChildren<TMP_Text>(true);
        middleHeroName = FindTextByValue(texts, "Ravenous Butcher");
        previewText = FindTextContaining(texts, "Buy this hero fragments");
        pointsLabel = FindTextByValue(texts, "Exp");
        pointsValue = FindTextByValue(texts, "3,904,432");
        upgradeLevelLabel = FindTextByValue(texts, "Kills");
        upgradeLevelValue = FindTextByValue(texts, "300854");

        Button[] buttons = middleContent.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null && string.Equals(label.text.Trim(), "Upgrade", StringComparison.OrdinalIgnoreCase))
            {
                upgradeButton = buttons[i];
                break;
            }
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(UpgradeSelectedStat);
        }
        else
        {
            Debug.LogWarning("[HeroScreen] Existing Upgrade button was not found in Middle-Content.", this);
        }
    }

    private void CacheDetailsPanel()
    {
        if (rightContent == null)
        {
            Debug.LogError("[HeroScreen] Right content reference is missing.", this);
            return;
        }

        TMP_Text[] texts = rightContent.GetComponentsInChildren<TMP_Text>(true);
        detailHeroName = FindTextByValue(texts, "Ravenous Butcher");
        detailCategory = FindTextContaining(texts, "Hero");
        detailDescription = FindTextContaining(texts, "A brutal frontline warrior");
    }

    private void SelectCategory(HeroType type)
    {
        if (selectedCategory == type)
        {
            return;
        }

        selectedCategory = type;
        RefreshHeroGrid();
        RefreshSelectedHero();
    }

    private void RefreshHeroGrid()
    {
        BindData();
        List<CharacterData> visible = new List<CharacterData>();
        IReadOnlyList<CharacterData> definitions = GetHeroDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            CharacterData definition = definitions[i];
            if (definition != null && definition.HeroType == selectedCategory &&
                data != null && data.IsHeroOwned(definition.HeroId))
            {
                visible.Add(definition);
            }
        }

        EnsureCardCapacity(visible.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            bool active = i < visible.Count;
            cards[i].root.SetActive(active);
            if (active)
            {
                BindCard(cards[i], visible[i]);
            }
        }

        if (selectedHero == null || !visible.Contains(selectedHero))
        {
            selectedHero = visible.Count > 0 ? visible[0] : null;
            selectedStat = null;
        }

        RefreshCardSelection();
        RefreshCategorySelection();
    }

    private void EnsureCardCapacity(int count)
    {
        if (cards.Count == 0 || heroGrid == null)
        {
            return;
        }

        GameObject template = cards[0].root;
        while (cards.Count < count)
        {
            GameObject clone = Instantiate(template, heroGrid, false);
            clone.name = "HeroCard_Runtime";
            cards.Add(CreateCardBinding(clone));
        }
    }

    private void BindCard(CardBinding card, CharacterData definition)
    {
        card.heroId = definition.HeroId;
        if (card.nameLabel != null)
        {
            card.nameLabel.text = definition.DisplayName;
        }

        if (card.portrait != null)
        {
            card.portrait.sprite = definition.Icon;
            card.portrait.enabled = definition.Icon != null;
            card.portrait.preserveAspect = true;
        }

        card.button.onClick.RemoveAllListeners();
        string capturedHeroId = definition.HeroId;
        card.button.onClick.AddListener(() => SelectHero(capturedHeroId));
    }

    private void SelectHero(string heroId)
    {
        selectedHero = null;
        IReadOnlyList<CharacterData> definitions = GetHeroDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            CharacterData definition = definitions[i];
            if (definition != null && string.Equals(definition.HeroId, heroId, StringComparison.Ordinal))
            {
                selectedHero = definition;
                break;
            }
        }
        selectedStat = null;
        RefreshCardSelection();
        RefreshSelectedHero();
    }

    private IReadOnlyList<CharacterData> GetHeroDefinitions()
    {
        return heroCatalog != null ? heroCatalog.Heroes : heroDefinitions;
    }

    private void SelectStat(HeroStatType statType)
    {
        selectedStat = statType;
        RefreshSelectedHero();
    }

    private void RefreshSelectedHero()
    {
        BindData();
        RefreshStats();
        RefreshPreview();
        RefreshDetails();
        RefreshStatSelection();
    }

    private void RefreshStats()
    {
        foreach (KeyValuePair<HeroStatType, StatBinding> pair in stats)
        {
            float value = selectedHero != null && data != null
                ? data.GetHeroFinalStat(selectedHero, pair.Key)
                : 0f;
            if (pair.Value.value != null)
            {
                pair.Value.value.text = FormatStatValue(pair.Key, value);
            }

            if (pair.Value.bar != null)
            {
                float maximum = selectedHero != null
                    ? selectedHero.GetDisplayMaximum(pair.Key)
                    : 1f;
                pair.Value.bar.SetValueWithoutNotify(Mathf.Clamp01(value / Mathf.Max(1f, maximum)));
            }
        }
    }

    private void RefreshPreview()
    {
        int points = data != null ? data.AvailableHeroUpgradePoints : 0;
        if (middleHeroName != null)
        {
            middleHeroName.text = selectedHero != null ? selectedHero.DisplayName : "No owned Hero";
        }

        if (pointsLabel != null)
        {
            pointsLabel.text = "Available Points";
        }

        if (pointsValue != null)
        {
            pointsValue.text = points.ToString();
        }

        if (upgradeLevelLabel != null)
        {
            upgradeLevelLabel.text = "Stat Upgrade";
        }

        bool validSelection = selectedHero != null && selectedStat.HasValue && data != null;
        if (validSelection)
        {
            HeroStatType statType = selectedStat.Value;
            int level = data.GetHeroUpgradeLevel(selectedHero.HeroId, statType);
            float current = data.GetHeroFinalStat(selectedHero, statType);
            float next = current + selectedHero.GetUpgradeAmount(statType);
            if (previewText != null)
            {
                previewText.text =
                    $"{GetStatLabel(statType)}\n" +
                    $"{FormatStatValue(statType, current)}  →  {FormatStatValue(statType, next)}\n" +
                    $"Upgrade Lv. {level}  →  Lv. {level + 1}\n" +
                    "Cost: 1 Upgrade Point";
            }

            if (upgradeLevelValue != null)
            {
                upgradeLevelValue.text = $"Lv. {level}";
            }
        }
        else
        {
            if (previewText != null)
            {
                previewText.text = "Select an owned Hero and one stat to preview the upgrade.\nCost: 1 Upgrade Point";
            }

            if (upgradeLevelValue != null)
            {
                upgradeLevelValue.text = "—";
            }
        }

        if (upgradeButton != null)
        {
            upgradeButton.interactable = validSelection && points > 0;
        }
    }

    private void RefreshDetails()
    {
        if (detailHeroName != null)
        {
            detailHeroName.text = selectedHero != null ? selectedHero.DisplayName : "No Hero Selected";
        }

        if (detailCategory != null)
        {
            detailCategory.text = selectedHero != null
                ? $"{FormatRarity(selectedHero.Rarity)} - {selectedHero.HeroType} Hero"
                : string.Empty;
        }

        if (detailDescription != null)
        {
            detailDescription.text = selectedHero != null ? selectedHero.Description : string.Empty;
        }
    }

    private void UpgradeSelectedStat()
    {
        if (selectedHero == null || !selectedStat.HasValue || data == null ||
            data.AvailableHeroUpgradePoints <= 0)
        {
            return;
        }

        HeroStatType statType = selectedStat.Value;
        float previousValue = data.GetHeroFinalStat(selectedHero, statType);
        float previousBar = previousValue /
                            Mathf.Max(1f, selectedHero.GetDisplayMaximum(statType));

        bool upgraded;
        int newLevel;
        suppressProgressRefresh = true;
        try
        {
            upgraded = data.TryUpgradeHeroStat(selectedHero.HeroId, statType, out newLevel);
        }
        finally
        {
            suppressProgressRefresh = false;
        }

        if (!upgraded)
        {
            RefreshSelectedHero();
            return;
        }

        float newValue = data.GetHeroFinalStat(selectedHero, statType);
        RefreshPreview();
        RefreshDetails();
        RefreshStatSelection();
        if (upgradeAnimation != null)
        {
            StopCoroutine(upgradeAnimation);
            RefreshStats();
            RefreshStatSelection();
        }
        upgradeAnimation = StartCoroutine(
            AnimateUpgrade(statType, previousValue, newValue, previousBar));
        toast?.Show($"{GetStatLabel(statType)} upgraded to Lv. {newLevel}");
        AudioManager.EnsureInstance()?.PlayUiClick();
    }

    private IEnumerator AnimateUpgrade(
        HeroStatType statType,
        float fromValue,
        float toValue,
        float fromBar)
    {
        if (!stats.TryGetValue(statType, out StatBinding binding))
        {
            yield break;
        }

        float toBar = selectedHero != null
            ? toValue / Mathf.Max(1f, selectedHero.GetDisplayMaximum(statType))
            : fromBar;
        Color baseColor = binding.normalLabelColor;
        Color flashColor = new Color(1f, 0.86f, 0.35f, 1f);
        const float duration = 0.42f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (binding.value != null)
            {
                binding.value.text = FormatStatValue(statType, Mathf.Lerp(fromValue, toValue, eased));
            }

            if (binding.bar != null)
            {
                binding.bar.SetValueWithoutNotify(
                    Mathf.Lerp(Mathf.Clamp01(fromBar), Mathf.Clamp01(toBar), eased));
            }

            if (binding.label != null)
            {
                binding.label.color = Color.Lerp(flashColor, baseColor, t);
            }

            yield return null;
        }

        RefreshStats();
        RefreshStatSelection();
        upgradeAnimation = null;
    }

    private void RefreshCardSelection()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            CardBinding card = cards[i];
            if (card.selectionGraphic == null)
            {
                continue;
            }

            bool selected = selectedHero != null &&
                            string.Equals(card.heroId, selectedHero.HeroId, StringComparison.Ordinal);
            card.selectionGraphic.color = selected
                ? new Color(1f, 0.78f, 0.42f, 1f)
                : card.normalColor;
        }
    }

    private void RefreshCategorySelection()
    {
        for (int i = 0; i < categories.Count; i++)
        {
            categories[i].label.color = categories[i].type == selectedCategory
                ? new Color(1f, 0.78f, 0.42f, 1f)
                : categories[i].normalColor;
        }
    }

    private void RefreshStatSelection()
    {
        foreach (KeyValuePair<HeroStatType, StatBinding> pair in stats)
        {
            pair.Value.label.color = selectedStat.HasValue && selectedStat.Value == pair.Key
                ? new Color(1f, 0.78f, 0.42f, 1f)
                : pair.Value.normalLabelColor;
        }
    }

    private static bool TryParseHeroType(string value, out HeroType type)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized == "infantry" || normalized == "infantary")
        {
            type = HeroType.Infantry;
            return true;
        }

        return Enum.TryParse(value?.Trim(), true, out type);
    }

    private static bool TryParseStat(string value, out HeroStatType statType)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized == "speed" || normalized == "move speed" || normalized == "movespeed")
        {
            statType = HeroStatType.MoveSpeed;
            return true;
        }

        return Enum.TryParse(value?.Trim(), true, out statType);
    }

    private static string GetStatLabel(HeroStatType type)
    {
        return type == HeroStatType.MoveSpeed ? "Speed" : type.ToString();
    }

    private static string FormatStatValue(HeroStatType type, float value)
    {
        return type == HeroStatType.MoveSpeed ? value.ToString("0.0") : value.ToString("0");
    }

    private static string FormatRarity(CharacterRarity rarity)
    {
        switch (rarity)
        {
            case CharacterRarity.ThreeStar: return "Three Star";
            case CharacterRarity.FourStar: return "Four Star";
            case CharacterRarity.FiveStar: return "Five Star";
            default: return rarity.ToString();
        }
    }

    private static TMP_Text FindTextByValue(TMP_Text[] texts, string value)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            if (string.Equals(texts[i].text.Trim(), value, StringComparison.OrdinalIgnoreCase))
            {
                return texts[i];
            }
        }

        return null;
    }

    private static TMP_Text FindTextContaining(TMP_Text[] texts, string value)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return texts[i];
            }
        }

        return null;
    }

    private static TMP_Text FindNamedText(TMP_Text[] texts, params string[] terms)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            string name = texts[i].name.ToLowerInvariant();
            for (int j = 0; j < terms.Length; j++)
            {
                if (name.Contains(terms[j]))
                {
                    return texts[i];
                }
            }
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private static Image FindNamedImage(Image[] images, params string[] terms)
    {
        for (int i = 0; i < images.Length; i++)
        {
            string name = images[i].name.ToLowerInvariant();
            for (int j = 0; j < terms.Length; j++)
            {
                if (name.Contains(terms[j]))
                {
                    return images[i];
                }
            }
        }

        return null;
    }
}
