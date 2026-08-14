#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StoreSystemSetup
{
    private const string CharacterFolder = "Assets/_Project/ScriptableObjects/Characters";
    private const string WeaponDataPath = "Assets/_Project/ScriptableObjects/Weapons/SO_Weapon_SeekerSword.asset";
    private const string CatalogFolder = "Assets/_Project/ScriptableObjects/Store";
    private const string WeaponEntryPath = CatalogFolder + "/SO_WeaponShopEntry_SeekerSword.asset";
    private const string CatalogPath = CatalogFolder + "/SO_StoreCatalog.asset";
    private const string FeaturedPath = "Assets/_Project/Prefab/Screens/Featured.prefab";
    private const string CharacterScreenPath = "Assets/_Project/Prefab/Screens/Character.prefab";
    private const string WeaponScreenPath = "Assets/_Project/Prefab/Screens/Weapon.prefab";
    private const string GameplayUiRootPath = "Assets/_Project/Prefab/UI/GameplayUI_Root.prefab";
    private const string BeaconCampPath = "Assets/Scenes/Beacon_Camp.unity";

    [InitializeOnLoadMethod]
    private static void ScheduleCharacterDescriptionBoxMigration()
    {
        EditorApplication.delayCall += EnsureCharacterDescriptionBoxBinding;
    }

    [MenuItem("ASTRA EDEN/Setup/Rebind Character Description Box")]
    public static void RebindCharacterDescriptionBox()
    {
        RebindCharacterDescriptionBoxInternal(force: true);
    }

    private static void EnsureCharacterDescriptionBoxBinding()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureCharacterDescriptionBoxBinding;
            return;
        }
        RebindCharacterDescriptionBoxInternal(force: false);
    }

    private static void RebindCharacterDescriptionBoxInternal(bool force)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CharacterScreenPath);
        try
        {
            CharacterWeaponStoreView view = root.GetComponent<CharacterWeaponStoreView>();
            if (view == null) return;
            SerializedObject viewSo = new SerializedObject(view);
            SerializedProperty combined = viewSo.FindProperty("combinePurchaseInfoInDescription");
            Transform box = FindByName(root.transform, "Description_Box_5");
            TMP_Text currentDescription = viewSo.FindProperty("descriptionText").objectReferenceValue as TMP_Text;
            if (!force && combined.boolValue && box != null && currentDescription != null &&
                currentDescription.transform.IsChildOf(box)) return;

            RemoveAllNamed(root.transform, "StoreDetails");
            Details details = BindCharacterDescriptionBox(root);
            viewSo.FindProperty("nameText").objectReferenceValue = details.name;
            viewSo.FindProperty("typeText").objectReferenceValue = details.type;
            viewSo.FindProperty("rarityText").objectReferenceValue = details.rarity;
            viewSo.FindProperty("descriptionText").objectReferenceValue = details.description;
            viewSo.FindProperty("expText").objectReferenceValue = details.exp;
            viewSo.FindProperty("killsText").objectReferenceValue = details.kills;
            viewSo.FindProperty("priceText").objectReferenceValue = null;
            viewSo.FindProperty("goldText").objectReferenceValue = null;
            viewSo.FindProperty("ownershipText").objectReferenceValue = null;
            viewSo.FindProperty("buyButton").objectReferenceValue = details.buy;
            viewSo.FindProperty("buyButtonText").objectReferenceValue = details.buyText;
            combined.boolValue = true;
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CharacterScreenPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[StoreSystemSetup] Character Store now reuses Right-Content/Description_Box_5.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("ASTRA EDEN/Setup/Build Character + Weapon Store")]
    public static void BuildAll()
    {
        EnsureFolder(CatalogFolder);
        CharacterData[] characters = LoadAllCharacters();
        WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponDataPath);
        CharacterShopEntryDefinition[] characterEntries = GetOrCreateCharacterEntries(characters);
        WeaponShopEntryDefinition weaponEntry = GetOrCreate<WeaponShopEntryDefinition>(WeaponEntryPath);
        StoreCatalogData catalog = GetOrCreate<StoreCatalogData>(CatalogPath);
        ConfigureEntry(weaponEntry, weapon, 25000);
        ConfigureCatalog(catalog, characterEntries, weaponEntry);
        ConfigureSpawnLoadoutHeroes(characters);

        ConfigureTabsOnly(FeaturedPath);
        ConfigureStoreScreen(CharacterScreenPath, StoreContentType.Character, catalog, createFromCharacter: false);
        ConfigureStoreScreen(WeaponScreenPath, StoreContentType.Weapon, catalog, createFromCharacter: true);
        WireGameplayUiRoot();
        WireBeaconCamp();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StoreSystemSetup] Character + Weapon Store assets, screens, tabs and Beacon Camp references are ready.");
    }

    public static void BuildAllBatch()
    {
        BuildAll();
        EditorApplication.Exit(0);
    }

    [MenuItem("ASTRA EDEN/Setup/Refresh Character Store Catalog")]
    public static void RefreshCharacterCatalog()
    {
        EnsureFolder(CatalogFolder);
        CharacterData[] characters = LoadAllCharacters();
        CharacterShopEntryDefinition[] entries = GetOrCreateCharacterEntries(characters);
        StoreCatalogData catalog = GetOrCreate<StoreCatalogData>(CatalogPath);
        WeaponShopEntryDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponShopEntryDefinition>(WeaponEntryPath);
        ConfigureCatalog(catalog, entries, weapon);
        ConfigureSpawnLoadoutHeroes(characters);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[StoreSystemSetup] Character catalog refreshed with {entries.Length} CharacterData assets.");
    }

    public static void RefreshCharacterCatalogBatch()
    {
        RefreshCharacterCatalog();
        EditorApplication.Exit(0);
    }

    private static CharacterData[] LoadAllCharacters()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterData", new[] { CharacterFolder });
        CharacterData[] characters = new CharacterData[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            characters[i] = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guids[i]));
        System.Array.Sort(characters, (left, right) =>
            string.Compare(left != null ? left.HeroId : string.Empty, right != null ? right.HeroId : string.Empty,
                System.StringComparison.Ordinal));
        return characters;
    }

    private static CharacterShopEntryDefinition[] GetOrCreateCharacterEntries(CharacterData[] characters)
    {
        CharacterShopEntryDefinition[] entries = new CharacterShopEntryDefinition[characters.Length];
        for (int i = 0; i < characters.Length; i++)
        {
            CharacterData character = characters[i];
            if (character == null || string.IsNullOrWhiteSpace(character.HeroId)) continue;
            string path = $"{CatalogFolder}/SO_CharacterShopEntry_{character.name.Replace("SO_Character_", string.Empty)}.asset";
            CharacterShopEntryDefinition entry = GetOrCreate<CharacterShopEntryDefinition>(path);
            ConfigureEntry(entry, character);
            entries[i] = entry;
        }
        return System.Array.FindAll(entries, entry => entry != null);
    }

    private static void ConfigureEntry(CharacterShopEntryDefinition entry, CharacterData character)
    {
        SerializedObject so = new SerializedObject(entry);
        so.FindProperty("character").objectReferenceValue = character;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(entry);
    }

    private static void ConfigureEntry(WeaponShopEntryDefinition entry, WeaponData weapon, int price)
    {
        SerializedObject so = new SerializedObject(entry);
        so.FindProperty("weapon").objectReferenceValue = weapon;
        so.FindProperty("goldPrice").intValue = price;
        so.FindProperty("isAvailableInStore").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(entry);
    }

    private static void ConfigureCatalog(StoreCatalogData catalog,
        CharacterShopEntryDefinition[] characterEntries, WeaponShopEntryDefinition weapon)
    {
        SerializedObject so = new SerializedObject(catalog);
        SerializedProperty characters = so.FindProperty("characters");
        characters.arraySize = characterEntries != null ? characterEntries.Length : 0;
        for (int i = 0; i < characters.arraySize; i++)
            characters.GetArrayElementAtIndex(i).objectReferenceValue = characterEntries[i];
        SerializedProperty weapons = so.FindProperty("weapons");
        weapons.arraySize = weapon != null ? 1 : 0;
        if (weapon != null) weapons.GetArrayElementAtIndex(0).objectReferenceValue = weapon;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void ConfigureSpawnLoadoutHeroes(CharacterData[] characters)
    {
        SpawnLoadoutCatalog loadout = AssetDatabase.LoadAssetAtPath<SpawnLoadoutCatalog>(
            "Assets/_Project/ScriptableObjects/Loadout/SO_SpawnLoadoutCatalog.asset");
        if (loadout == null) return;
        SerializedObject so = new SerializedObject(loadout);
        SerializedProperty heroes = so.FindProperty("heroes");
        heroes.arraySize = characters != null ? characters.Length : 0;
        for (int i = 0; i < heroes.arraySize; i++)
            heroes.GetArrayElementAtIndex(i).objectReferenceValue = characters[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(loadout);
    }

    private static void ConfigureTabsOnly(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        AddTabButtons(root);
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void ConfigureStoreScreen(string path, StoreContentType mode, StoreCatalogData catalog, bool createFromCharacter)
    {
        if (createFromCharacter && !File.Exists(path))
        {
            AssetDatabase.CopyAsset(CharacterScreenPath, path);
            AssetDatabase.ImportAsset(path);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        root.name = mode == StoreContentType.Character ? "Character" : "Weapon";
        RemoveAllNamed(root.transform, "StoreRuntime_Root");
        RemoveAllNamed(root.transform, "StoreRuntime_PreviewStage");
        RemoveAllNamed(root.transform, "StoreEntryCard_Template");
        RemoveAllNamed(root.transform, "StoreDetails");
        RemoveAllNamed(root.transform, "StorePreview");
        AddTabButtons(root);

        TMP_FontAsset font = FindFont(root);
        Sprite panelSprite = FindSprite(root);
        ScrollRect scroll = root.GetComponentInChildren<ScrollRect>(true);
        Transform content = scroll != null && scroll.content != null ? scroll.content : root.transform;
        for (int i = 0; i < content.childCount; i++) content.GetChild(i).gameObject.SetActive(false);

        GameObject runtimeRoot = RectObject("StoreRuntime_Root", root.transform, Vector2.zero, Vector2.one);
        runtimeRoot.transform.SetAsLastSibling();
        Image blocker = runtimeRoot.AddComponent<Image>();
        blocker.color = Color.clear;
        blocker.raycastTarget = false;

        StoreEntryCardView cardTemplate = BuildCardTemplate(content, font, panelSprite);
        SpawnLoadoutPreview preview = BuildPreview(root, runtimeRoot.transform);
        Details details;
        bool usesCharacterDescriptionBox = mode == StoreContentType.Character;
        if (usesCharacterDescriptionBox)
        {
            details = BindCharacterDescriptionBox(root);
        }
        else
        {
            Transform detailsParent = FindByName(root.transform, "Right-Content") ?? runtimeRoot.transform;
            for (int i = 0; i < detailsParent.childCount; i++) detailsParent.GetChild(i).gameObject.SetActive(false);
            details = BuildDetails(detailsParent, font, panelSprite);
        }
        StoreConfirmationModal confirmation = BuildConfirmation(runtimeRoot.transform, font, panelSprite);
        HeroUpgradeToast toast = root.GetComponent<HeroUpgradeToast>() ?? root.AddComponent<HeroUpgradeToast>();

        CharacterWeaponStoreView view = root.GetComponent<CharacterWeaponStoreView>() ?? root.AddComponent<CharacterWeaponStoreView>();
        SerializedObject viewSo = new SerializedObject(view);
        viewSo.FindProperty("contentType").enumValueIndex = (int)mode;
        viewSo.FindProperty("catalog").objectReferenceValue = catalog;
        viewSo.FindProperty("contentRoot").objectReferenceValue = content;
        viewSo.FindProperty("cardTemplate").objectReferenceValue = cardTemplate;
        viewSo.FindProperty("preview").objectReferenceValue = preview;
        viewSo.FindProperty("nameText").objectReferenceValue = details.name;
        viewSo.FindProperty("typeText").objectReferenceValue = details.type;
        viewSo.FindProperty("rarityText").objectReferenceValue = details.rarity;
        viewSo.FindProperty("descriptionText").objectReferenceValue = details.description;
        viewSo.FindProperty("expText").objectReferenceValue = details.exp;
        viewSo.FindProperty("killsText").objectReferenceValue = details.kills;
        viewSo.FindProperty("priceText").objectReferenceValue = details.price;
        viewSo.FindProperty("goldText").objectReferenceValue = details.gold;
        viewSo.FindProperty("ownershipText").objectReferenceValue = details.owned;
        viewSo.FindProperty("buyButton").objectReferenceValue = details.buy;
        viewSo.FindProperty("buyButtonText").objectReferenceValue = details.buyText;
        viewSo.FindProperty("confirmation").objectReferenceValue = confirmation;
        viewSo.FindProperty("toast").objectReferenceValue = toast;
        viewSo.FindProperty("combinePurchaseInfoInDescription").boolValue = usesCharacterDescriptionBox;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static StoreEntryCardView BuildCardTemplate(Transform parent, TMP_FontAsset font, Sprite sprite)
    {
        GameObject card = RectObject("StoreEntryCard_Template", parent, Vector2.zero, Vector2.one);
        RectTransform rect = (RectTransform)card.transform;
        rect.sizeDelta = new Vector2(290f, 350f);
        Image background = card.AddComponent<Image>();
        background.sprite = sprite;
        background.color = new Color(0.035f, 0.07f, 0.09f, 0.98f);
        Button button = card.AddComponent<Button>();

        Image portrait = RectObject("Portrait", card.transform, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.94f)).AddComponent<Image>();
        portrait.color = Color.white;
        TMP_Text title = MakeText("Name", card.transform, font, 26, TextAlignmentOptions.Center,
            new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.29f));
        TMP_Text type = MakeText("Type", card.transform, font, 19, TextAlignmentOptions.Center,
            new Vector2(0.04f, 0.09f), new Vector2(0.50f, 0.17f));
        TMP_Text rarity = MakeText("Rarity", card.transform, font, 19, TextAlignmentOptions.Center,
            new Vector2(0.50f, 0.09f), new Vector2(0.96f, 0.17f));
        GameObject ownedBadge = RectObject("OWNED", card.transform, new Vector2(0.58f, 0.83f), new Vector2(0.96f, 0.94f));
        Image ownedBg = ownedBadge.AddComponent<Image>();
        ownedBg.color = new Color(0.88f, 0.55f, 0.15f, 0.95f);
        MakeText("Label", ownedBadge.transform, font, 18, TextAlignmentOptions.Center, Vector2.zero, Vector2.one).text = "OWNED";
        GameObject selected = RectObject("Selected", card.transform, Vector2.zero, Vector2.one);
        Image selectedImage = selected.AddComponent<Image>();
        selectedImage.color = new Color(1f, 0.64f, 0.18f, 0.16f);
        selectedImage.raycastTarget = false;

        StoreEntryCardView view = card.AddComponent<StoreEntryCardView>();
        view.Configure(button, portrait, title, type, rarity, ownedBadge, selected);
        card.SetActive(false);
        return view;
    }

    private static SpawnLoadoutPreview BuildPreview(GameObject root, Transform overlay)
    {
        Transform middle = FindByName(root.transform, "Middle-Content") ?? overlay;
        for (int i = 0; i < middle.childCount; i++) middle.GetChild(i).gameObject.SetActive(false);
        GameObject previewPanel = RectObject("StorePreview", middle, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f));
        RawImage raw = previewPanel.AddComponent<RawImage>();
        raw.color = Color.white;

        GameObject stage = new GameObject("StoreRuntime_PreviewStage");
        stage.layer = 31;
        stage.transform.SetParent(root.transform, false);
        stage.transform.localPosition = new Vector3(10000f, 10000f, 10000f);
        GameObject cameraGo = new GameObject("PreviewCamera", typeof(Camera));
        cameraGo.layer = 31;
        cameraGo.transform.SetParent(stage.transform, false);
        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << 31;
        camera.fieldOfView = 28f;
        GameObject lightGo = new GameObject("KeyLight", typeof(Light));
        lightGo.layer = 31;
        lightGo.transform.SetParent(stage.transform, false);
        lightGo.transform.localRotation = Quaternion.Euler(35f, 25f, 0f);
        Light light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.6f;

        SpawnLoadoutPreview preview = previewPanel.AddComponent<SpawnLoadoutPreview>();
        SerializedObject so = new SerializedObject(preview);
        so.FindProperty("output").objectReferenceValue = raw;
        so.FindProperty("previewRoot").objectReferenceValue = stage.transform;
        so.FindProperty("previewCamera").objectReferenceValue = camera;
        so.FindProperty("autoRotate").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return preview;
    }

    private sealed class Details
    {
        public TMP_Text name, type, rarity, description, exp, kills, price, gold, owned, buyText;
        public Button buy;
    }

    private static Details BindCharacterDescriptionBox(GameObject root)
    {
        Transform box = FindByName(root.transform, "Description_Box_5");
        if (box == null)
        {
            Debug.LogError("[StoreSystemSetup] Character.prefab is missing Right-Content/Description_Box_5.", root);
            return new Details();
        }

        box.gameObject.SetActive(true);
        TMP_Text[] texts = box.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text buyText = FindText(texts, value => string.Equals(value, "Buy", System.StringComparison.OrdinalIgnoreCase));
        Button buyButton = buyText != null ? buyText.GetComponentInParent<Button>(true) : box.GetComponentInChildren<Button>(true);
        return new Details
        {
            name = FindText(texts, value => value == "Ravenous Butcher"),
            type = FindText(texts, value => value.Contains("Hero") && value != "Ravenous Butcher"),
            rarity = FindText(texts, value => value == "Legendary"),
            description = FindText(texts, value => value.Contains("Fragments")),
            exp = FindText(texts, value => value == "3,904,432"),
            kills = FindText(texts, value => value == "300854"),
            buy = buyButton,
            buyText = buyText
        };
    }

    private static TMP_Text FindText(TMP_Text[] texts, System.Predicate<string> predicate)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && predicate(text.text ?? string.Empty)) return text;
        }
        return null;
    }

    private static Details BuildDetails(Transform parent, TMP_FontAsset font, Sprite sprite)
    {
        GameObject panel = RectObject("StoreDetails", parent, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.92f));
        Image bg = panel.AddComponent<Image>();
        bg.sprite = sprite;
        bg.color = new Color(0.025f, 0.055f, 0.07f, 0.97f);
        Details d = new Details();
        d.name = MakeText("SelectedName", panel.transform, font, 36, TextAlignmentOptions.TopLeft, new Vector2(0.07f, 0.82f), new Vector2(0.93f, 0.96f));
        d.type = MakeText("SelectedType", panel.transform, font, 23, TextAlignmentOptions.Left, new Vector2(0.07f, 0.75f), new Vector2(0.93f, 0.83f));
        d.rarity = MakeText("RankRarity", panel.transform, font, 22, TextAlignmentOptions.Left, new Vector2(0.07f, 0.68f), new Vector2(0.93f, 0.76f));
        d.description = MakeText("Description", panel.transform, font, 21, TextAlignmentOptions.TopLeft, new Vector2(0.07f, 0.39f), new Vector2(0.93f, 0.66f));
        d.exp = MakeText("Exp", panel.transform, font, 20, TextAlignmentOptions.Left, new Vector2(0.07f, 0.31f), new Vector2(0.50f, 0.39f));
        d.kills = MakeText("Kills", panel.transform, font, 20, TextAlignmentOptions.Left, new Vector2(0.50f, 0.31f), new Vector2(0.93f, 0.39f));
        d.owned = MakeText("Ownership", panel.transform, font, 22, TextAlignmentOptions.Left, new Vector2(0.07f, 0.23f), new Vector2(0.45f, 0.31f));
        d.gold = MakeText("Gold", panel.transform, font, 22, TextAlignmentOptions.Right, new Vector2(0.45f, 0.23f), new Vector2(0.93f, 0.31f));
        d.price = MakeText("Price", panel.transform, font, 26, TextAlignmentOptions.Center, new Vector2(0.07f, 0.14f), new Vector2(0.93f, 0.23f));

        GameObject buyGo = RectObject("BuyButton", panel.transform, new Vector2(0.07f, 0.03f), new Vector2(0.72f, 0.13f));
        Image buyImage = buyGo.AddComponent<Image>();
        buyImage.sprite = sprite;
        buyImage.color = new Color(0.76f, 0.35f, 0.08f, 1f);
        d.buy = buyGo.AddComponent<Button>();
        d.buyText = MakeText("Label", buyGo.transform, font, 27, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
        d.buyText.text = "BUY";

        GameObject backGo = RectObject("BackButton", panel.transform, new Vector2(0.74f, 0.03f), new Vector2(0.93f, 0.13f));
        Image backImage = backGo.AddComponent<Image>();
        backImage.sprite = sprite;
        backImage.color = new Color(0.12f, 0.17f, 0.2f, 1f);
        Button back = backGo.AddComponent<Button>();
        StoreBackButton backHandler = backGo.AddComponent<StoreBackButton>();
        SerializedObject backSo = new SerializedObject(backHandler);
        backSo.FindProperty("button").objectReferenceValue = back;
        backSo.ApplyModifiedPropertiesWithoutUndo();
        MakeText("Label", backGo.transform, font, 23, TextAlignmentOptions.Center, Vector2.zero, Vector2.one).text = "BACK [B]";
        return d;
    }

    private static StoreConfirmationModal BuildConfirmation(Transform parent, TMP_FontAsset font, Sprite sprite)
    {
        GameObject panel = RectObject("PurchaseConfirmation", parent, new Vector2(0.30f, 0.33f), new Vector2(0.70f, 0.67f));
        Image bg = panel.AddComponent<Image>();
        bg.sprite = sprite;
        bg.color = new Color(0.02f, 0.04f, 0.055f, 0.99f);
        panel.AddComponent<CanvasGroup>();
        panel.AddComponent<PopupTween>();
        TMP_Text message = MakeText("Message", panel.transform, font, 30, TextAlignmentOptions.Center, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.9f));
        Button confirm = MakeButton("Confirm", panel.transform, font, sprite, new Vector2(0.08f, 0.08f), new Vector2(0.48f, 0.32f), "CONFIRM");
        Button cancel = MakeButton("Cancel", panel.transform, font, sprite, new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.32f), "CANCEL");
        StoreConfirmationModal modal = parent.gameObject.AddComponent<StoreConfirmationModal>();
        SerializedObject so = new SerializedObject(modal);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("message").objectReferenceValue = message;
        so.FindProperty("confirmButton").objectReferenceValue = confirm;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.ApplyModifiedPropertiesWithoutUndo();
        return modal;
    }

    private static void AddTabButtons(GameObject root)
    {
        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        AddTab(labels, "Featured", StoreTab.Featured);
        AddTab(labels, "Character", StoreTab.Character);
        AddTab(labels, "Weapon", StoreTab.Weapon);
    }

    private static void AddTab(TMP_Text[] labels, string value, StoreTab tab)
    {
        TMP_Text best = null;
        for (int i = 0; i < labels.Length; i++)
        {
            if (!string.Equals(labels[i].text?.Trim(), value, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (best == null || labels[i].rectTransform.anchoredPosition.y > best.rectTransform.anchoredPosition.y) best = labels[i];
        }
        if (best == null) return;
        Button button = best.GetComponent<Button>() ?? best.gameObject.AddComponent<Button>();
        StoreTabButton tabButton = best.GetComponent<StoreTabButton>() ?? best.gameObject.AddComponent<StoreTabButton>();
        SerializedObject so = new SerializedObject(tabButton);
        so.FindProperty("tab").enumValueIndex = (int)tab;
        so.FindProperty("button").objectReferenceValue = button;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireBeaconCamp()
    {
        if (!File.Exists(BeaconCampPath)) return;
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(BeaconCampPath, OpenSceneMode.Single);
        ShopUIController[] controllers = Object.FindObjectsByType<ShopUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform menuCanvas = FindSceneTransform("Menu_Canvas");
        Transform panels = menuCanvas != null ? FindDirectChild(menuCanvas, "Panels") : null;
        GameObject featured = FindDirectChild(panels, "Featured")?.gameObject;
        GameObject character = FindDirectChild(panels, "Character")?.gameObject;
        GameObject weapon = FindDirectChild(panels, "Weapon")?.gameObject;
        InventoryToggleController menuController = Object.FindFirstObjectByType<InventoryToggleController>(FindObjectsInactive.Include);
        for (int i = 0; i < controllers.Length; i++)
        {
            SerializedObject so = new SerializedObject(controllers[i]);
            so.FindProperty("root").objectReferenceValue = featured;
            so.FindProperty("characterScreen").objectReferenceValue = character;
            so.FindProperty("weaponScreen").objectReferenceValue = weapon;
            so.FindProperty("menuController").objectReferenceValue = menuController;
            so.FindProperty("titleText").objectReferenceValue = FindNamedComponent<TMP_Text>(featured, "ShopTitle");
            so.FindProperty("statusText").objectReferenceValue = FindNamedComponent<TMP_Text>(featured, "ShopGoldStatus");
            so.FindProperty("closeButton").objectReferenceValue = FindNamedComponent<Button>(featured, "Button_CloseFeaturedShop");
            so.FindProperty("entryButtons").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controllers[i]);

            Transform obsoleteStandalone = FindDirectChild(controllers[i].transform, "Featured_BeaconShop");
            if (obsoleteStandalone != null) Object.DestroyImmediate(obsoleteStandalone.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (previous.IsValid() && previous.path != scene.path) EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
    }

    private static void WireGameplayUiRoot()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameplayUiRootPath);
        Transform menuCanvas = FindByName(root.transform, "Menu_Canvas");
        Transform panels = menuCanvas != null ? FindDirectChild(menuCanvas, "Panels") : null;
        if (panels == null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            throw new System.InvalidOperationException("GameplayUI_Root is missing Menu_Canvas/Panels.");
        }

        GameObject weapon = FindDirectChild(panels, "Weapon")?.gameObject;
        if (weapon == null)
        {
            GameObject weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponScreenPath);
            weapon = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, panels);
            weapon.name = "Weapon";
            RectTransform rect = weapon.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
        }

        GameObject featured = FindDirectChild(panels, "Featured")?.gameObject;
        GameObject character = FindDirectChild(panels, "Character")?.gameObject;
        if (featured != null) featured.SetActive(false);
        if (character != null) character.SetActive(false);
        if (weapon != null) weapon.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, GameplayUiRootPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static Transform FindSceneTransform(string name)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindByName(roots[i].transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        return null;
    }

    private static T FindNamedComponent<T>(GameObject root, string name) where T : Component
    {
        if (root == null) return null;
        Transform found = FindByName(root.transform, name);
        return found != null ? found.GetComponent<T>() : null;
    }

    private static TMP_FontAsset FindFont(GameObject root)
    {
        TMP_Text text = root.GetComponentInChildren<TMP_Text>(true);
        return text != null ? text.font : null;
    }

    private static Sprite FindSprite(GameObject root)
    {
        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++) if (images[i].sprite != null) return images[i].sprite;
        return null;
    }

    private static Transform FindByName(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) if (all[i].name == name) return all[i];
        return null;
    }

    private static void RemoveAllNamed(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = all.Length - 1; i >= 0; i--)
        {
            if (all[i] != null && all[i] != root && all[i].name == name)
                Object.DestroyImmediate(all[i].gameObject);
        }
    }

    private static GameObject RectObject(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go;
    }

    private static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font, float size,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max)
    {
        GameObject go = RectObject(name, parent, min, max);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.93f, 0.88f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Button MakeButton(string name, Transform parent, TMP_FontAsset font, Sprite sprite,
        Vector2 min, Vector2 max, string label)
    {
        GameObject go = RectObject(name, parent, min, max);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(0.70f, 0.32f, 0.08f, 1f);
        Button button = go.AddComponent<Button>();
        MakeText("Label", go.transform, font, 24, TextAlignmentOptions.Center, Vector2.zero, Vector2.one).text = label;
        return button;
    }

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
