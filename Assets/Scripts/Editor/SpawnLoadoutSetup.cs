#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SpawnLoadoutSetup
{
    const string HeroPath = "Assets/_Project/ScriptableObjects/Heroes/SO_Hero_SeekerDefault.asset";
    const string WeaponPath = "Assets/_Project/ScriptableObjects/Weapons/SO_Weapon_SeekerSword.asset";
    const string CompatibilityPath = "Assets/_Project/ScriptableObjects/Loadout/SO_HeroWeaponCompatibility.asset";
    const string CatalogPath = "Assets/_Project/ScriptableObjects/Loadout/SO_SpawnLoadoutCatalog.asset";
    const string ScreenPath = "Assets/_Project/Prefab/Screens/SpawnLoadout.prefab";
    const string UiRootPath = "Assets/_Project/Prefab/UI/GameplayUI_Root.prefab";
    const string HubPath = "Assets/Prefabs/Environment/Hub.prefab";
    const string PlayerPath = "Assets/_Project/Prefab/Player.prefab";
    const string SeekerPath = "Assets/Prefabs/Vroids/Seeker Prototype/Seeker Prototype Nu.prefab";

    [MenuItem("ASTRA EDEN/Setup/Build Spawn Loadout System")]
    public static void BuildAll()
    {
        EnsureFolder("Assets/_Project/ScriptableObjects", "Weapons");
        EnsureFolder("Assets/_Project/ScriptableObjects", "Loadout");

        HeroDefinition hero = AssetDatabase.LoadAssetAtPath<HeroDefinition>(HeroPath);
        WeaponData weapon = GetOrCreateAsset<WeaponData>(WeaponPath);
        ConfigureWeapon(weapon);
        HeroWeaponCompatibilityConfig compatibility = GetOrCreateAsset<HeroWeaponCompatibilityConfig>(CompatibilityPath);
        ConfigureCompatibility(compatibility);
        SpawnLoadoutCatalog catalog = GetOrCreateAsset<SpawnLoadoutCatalog>(CatalogPath);
        ConfigureCatalog(catalog, hero, weapon, compatibility);
        ConfigureHero(hero);

        TMP_FontAsset font = FindStyleFont();
        Sprite buttonSprite = FindStyleSprite();
        GameObject screen = BuildScreen(catalog, font, buttonSprite);
        WirePlayerPrefab(PlayerPath, catalog, hero);
        WirePlayerPrefab(SeekerPath, catalog, hero);
        WireUiRoot(screen);
        WireTerminal();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SpawnLoadoutSetup] Created data, prefab, player wiring, UI root instance, and Spawn Station interaction.");
    }

    static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void ConfigureWeapon(WeaponData weapon)
    {
        weapon.weaponId = GameDataManager.DefaultWeaponId;
        weapon.displayName = "Seeker Iron Sword";
        weapon.weaponType = WeaponType.Sword;
        weapon.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Environment/Hovl Studio/Package Magic sword/Prefabs/MagicSword_Iron.prefab");
        weapon.basicAttackDamageBonusPercent = 0.10f;
        weapon.skillDamageBonusPercent = 0.35f;
        weapon.useBuiltInVisual = true;
        weapon.socket = WeaponSocket.RightHand;
        weapon.localPosition = new Vector3(0.0601f, -0.0072f, 0.0087f);
        weapon.localScale = Vector3.one * 0.4f;
        EditorUtility.SetDirty(weapon);
    }

    static void ConfigureHero(HeroDefinition hero)
    {
        if (hero == null) return;
        SerializedObject so = new SerializedObject(hero);
        so.FindProperty("defaultWeaponId").stringValue = GameDataManager.DefaultWeaponId;
        so.FindProperty("overrideTypeWeaponCompatibility").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hero);
    }

    static void ConfigureCompatibility(HeroWeaponCompatibilityConfig config)
    {
        SerializedObject so = new SerializedObject(config);
        SerializedProperty rules = so.FindProperty("typeRules");
        rules.arraySize = 5;
        SetRule(rules.GetArrayElementAtIndex(0), HeroType.Infantry, WeaponType.Sword, WeaponType.Greatsword, WeaponType.Axe);
        SetRule(rules.GetArrayElementAtIndex(1), HeroType.Ranged, WeaponType.Bow);
        SetRule(rules.GetArrayElementAtIndex(2), HeroType.Riders, WeaponType.Sword, WeaponType.Bow, WeaponType.Spear);
        SetRule(rules.GetArrayElementAtIndex(3), HeroType.Tank, WeaponType.Greatsword, WeaponType.Axe);
        SetRule(rules.GetArrayElementAtIndex(4), HeroType.Master, WeaponType.Staff, WeaponType.Wand);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
    }

    static void SetRule(SerializedProperty rule, HeroType heroType, params WeaponType[] weaponTypes)
    {
        rule.FindPropertyRelative("heroType").enumValueIndex = (int)heroType;
        SerializedProperty allowed = rule.FindPropertyRelative("allowedWeaponTypes");
        allowed.arraySize = weaponTypes.Length;
        for (int i = 0; i < weaponTypes.Length; i++) allowed.GetArrayElementAtIndex(i).enumValueIndex = (int)weaponTypes[i];
    }

    static void ConfigureCatalog(SpawnLoadoutCatalog catalog, HeroDefinition hero, WeaponData weapon, HeroWeaponCompatibilityConfig compatibility)
    {
        SerializedObject so = new SerializedObject(catalog);
        SerializedProperty heroes = so.FindProperty("heroes");
        heroes.arraySize = hero == null ? 0 : 1;
        if (hero != null) heroes.GetArrayElementAtIndex(0).objectReferenceValue = hero;
        SerializedProperty weapons = so.FindProperty("weapons");
        weapons.arraySize = 1;
        weapons.GetArrayElementAtIndex(0).objectReferenceValue = weapon;
        so.FindProperty("compatibility").objectReferenceValue = compatibility;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    static GameObject BuildScreen(SpawnLoadoutCatalog catalog, TMP_FontAsset font, Sprite buttonSprite)
    {
        GameObject root = Ui("SpawnLoadout", null, Vector2.zero, Vector2.one);
        root.SetActive(false);
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.015f, 0.025f, 0.04f, 0.97f);
        root.AddComponent<CanvasGroup>();
        PopupTween popupTween = root.AddComponent<PopupTween>();
        SerializedObject tweenSo = new SerializedObject(popupTween);
        tweenSo.FindProperty("hiddenScale").floatValue = 0.98f;
        tweenSo.ApplyModifiedPropertiesWithoutUndo();
        SpawnLoadoutView view = root.AddComponent<SpawnLoadoutView>();

        TMP_Text title = Text("SPAWN STATION  /  HERO + WEAPON LOADOUT", root.transform, font, 34, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.02f, 0.91f), new Vector2(0.98f, 0.99f));

        GameObject left = Panel("HeroPanel", root.transform, new Vector2(0.02f, 0.10f), new Vector2(0.28f, 0.90f));
        Transform heroFilters = Horizontal("HeroCategories", left.transform, 6f);
        SetRect((RectTransform)heroFilters, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.98f));
        Transform heroGrid = Vertical("OwnedHeroGrid", left.transform, 8f);
        SetRect((RectTransform)heroGrid, new Vector2(0.04f, 0.38f), new Vector2(0.96f, 0.80f));
        TMP_Text heroName = Text("Selected Hero", left.transform, font, 27, TextAlignmentOptions.Left);
        SetRect(heroName.rectTransform, new Vector2(0.05f, 0.29f), new Vector2(0.95f, 0.37f));
        TMP_Text heroType = Text("Type", left.transform, font, 20, TextAlignmentOptions.Left);
        SetRect(heroType.rectTransform, new Vector2(0.05f, 0.23f), new Vector2(0.95f, 0.30f));
        TMP_Text heroStats = Text("Stats", left.transform, font, 19, TextAlignmentOptions.TopLeft);
        SetRect(heroStats.rectTransform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.23f));

        GameObject center = Panel("PreviewPanel", root.transform, new Vector2(0.29f, 0.10f), new Vector2(0.67f, 0.90f));
        RawImage previewImage = Ui("HeroWeaponPreview", center.transform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f)).AddComponent<RawImage>();
        previewImage.color = Color.white;
        SpawnLoadoutPreview preview = center.AddComponent<SpawnLoadoutPreview>();
        GameObject stage = new GameObject("PreviewStage");
        stage.layer = 31;
        stage.transform.SetParent(root.transform, false);
        stage.transform.localPosition = new Vector3(10000f, 10000f, 10000f);
        GameObject cameraGo = new GameObject("PreviewCamera", typeof(Camera));
        cameraGo.layer = 31;
        cameraGo.transform.SetParent(stage.transform, false);
        cameraGo.transform.localPosition = new Vector3(0f, 1.1f, -4.2f);
        cameraGo.transform.localRotation = Quaternion.identity;
        Camera previewCamera = cameraGo.GetComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.cullingMask = 1 << 31;
        previewCamera.fieldOfView = 28f;
        GameObject lightGo = new GameObject("KeyLight", typeof(Light));
        lightGo.layer = 31;
        lightGo.transform.SetParent(stage.transform, false);
        lightGo.transform.localPosition = new Vector3(-2f, 3f, -2f);
        lightGo.transform.localRotation = Quaternion.Euler(35f, 25f, 0f);
        Light light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.6f;

        SerializedObject previewSo = new SerializedObject(preview);
        previewSo.FindProperty("output").objectReferenceValue = previewImage;
        previewSo.FindProperty("previewRoot").objectReferenceValue = stage.transform;
        previewSo.FindProperty("previewCamera").objectReferenceValue = previewCamera;
        previewSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject right = Panel("WeaponPanel", root.transform, new Vector2(0.68f, 0.10f), new Vector2(0.98f, 0.90f));
        Transform weaponFilters = Horizontal("WeaponFilters", right.transform, 5f);
        SetRect((RectTransform)weaponFilters, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.98f));
        Transform weaponGrid = Vertical("CompatibleOwnedWeapons", right.transform, 8f);
        SetRect((RectTransform)weaponGrid, new Vector2(0.04f, 0.47f), new Vector2(0.96f, 0.79f));
        TMP_Text weaponName = Text("Selected Weapon", right.transform, font, 27, TextAlignmentOptions.Left);
        SetRect(weaponName.rectTransform, new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.46f));
        TMP_Text weaponType = Text("Type", right.transform, font, 20, TextAlignmentOptions.Left);
        SetRect(weaponType.rectTransform, new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.39f));
        TMP_Text weaponStats = Text("Stats", right.transform, font, 19, TextAlignmentOptions.TopLeft);
        SetRect(weaponStats.rectTransform, new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.32f));
        TMP_Text validation = Text("Ready", right.transform, font, 17, TextAlignmentOptions.Center);
        SetRect(validation.rectTransform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.14f));

        Button template = Button("CardTemplate", root.transform, font, buttonSprite, "Card");
        template.gameObject.SetActive(false);
        Button confirm = Button("Confirm", right.transform, font, buttonSprite, "CONFIRM");
        SetRect((RectTransform)confirm.transform, new Vector2(0.52f, 0.01f), new Vector2(0.96f, 0.08f));
        Button back = Button("Back", right.transform, font, buttonSprite, "BACK [B]");
        SetRect((RectTransform)back.transform, new Vector2(0.05f, 0.01f), new Vector2(0.48f, 0.08f));

        SerializedObject viewSo = new SerializedObject(view);
        viewSo.FindProperty("catalog").objectReferenceValue = catalog;
        viewSo.FindProperty("heroCategoryContainer").objectReferenceValue = heroFilters;
        viewSo.FindProperty("heroGrid").objectReferenceValue = heroGrid;
        viewSo.FindProperty("weaponFilterContainer").objectReferenceValue = weaponFilters;
        viewSo.FindProperty("weaponGrid").objectReferenceValue = weaponGrid;
        viewSo.FindProperty("buttonTemplate").objectReferenceValue = template;
        viewSo.FindProperty("heroNameText").objectReferenceValue = heroName;
        viewSo.FindProperty("heroTypeText").objectReferenceValue = heroType;
        viewSo.FindProperty("heroStatsText").objectReferenceValue = heroStats;
        viewSo.FindProperty("weaponNameText").objectReferenceValue = weaponName;
        viewSo.FindProperty("weaponTypeText").objectReferenceValue = weaponType;
        viewSo.FindProperty("weaponStatsText").objectReferenceValue = weaponStats;
        viewSo.FindProperty("validationText").objectReferenceValue = validation;
        viewSo.FindProperty("confirmButton").objectReferenceValue = confirm;
        viewSo.FindProperty("backButton").objectReferenceValue = back;
        viewSo.FindProperty("preview").objectReferenceValue = preview;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    static void WirePlayerPrefab(string path, SpawnLoadoutCatalog catalog, HeroDefinition hero)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        PlayerLoadoutRuntime runtime = root.GetComponent<PlayerLoadoutRuntime>() ?? root.AddComponent<PlayerLoadoutRuntime>();
        Transform right = Find(root.transform, "J_Bip_R_Hand");
        Transform left = Find(root.transform, "J_Bip_L_Hand");
        Transform builtIn = Find(root.transform, "MagicSword_Iron");
        SerializedObject so = new SerializedObject(runtime);
        so.FindProperty("catalog").objectReferenceValue = catalog;
        so.FindProperty("heroDefinition").objectReferenceValue = hero;
        so.FindProperty("rightHandSocket").objectReferenceValue = right;
        so.FindProperty("leftHandSocket").objectReferenceValue = left;
        so.FindProperty("backSocket").objectReferenceValue = root.transform;
        so.FindProperty("builtInWeaponVisual").objectReferenceValue = builtIn != null ? builtIn.gameObject : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void WireUiRoot(GameObject screenPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(UiRootPath);
        Transform panels = Find(root.transform, "Panels");
        Transform existing = panels != null ? panels.Find("SpawnLoadout") : null;
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(screenPrefab, panels);
        instance.name = "SpawnLoadout";
        instance.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void WireTerminal()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HubPath);
        Transform terminal = Find(root.transform, "SM_Terminal_3_embedded");
        if (terminal != null && terminal.GetComponent<SpawnStationInteractable>() == null)
            terminal.gameObject.AddComponent<SpawnStationInteractable>();
        PrefabUtility.SaveAsPrefabAsset(root, HubPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static TMP_FontAsset FindStyleFont()
    {
        GameObject hero = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Screens/Hero.prefab");
        TMP_Text text = hero != null ? hero.GetComponentInChildren<TMP_Text>(true) : null;
        return text != null ? text.font : null;
    }

    static Sprite FindStyleSprite()
    {
        GameObject hero = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Screens/Hero.prefab");
        Image[] images = hero != null ? hero.GetComponentsInChildren<Image>(true) : null;
        if (images != null) for (int i = 0; i < images.Length; i++) if (images[i].sprite != null) return images[i].sprite;
        return null;
    }

    static GameObject Ui(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        SetRect((RectTransform)go.transform, min, max);
        return go;
    }

    static GameObject Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = Ui(name, parent, min, max);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.055f, 0.08f, 0.11f, 0.96f);
        return go;
    }

    static Transform Horizontal(string name, Transform parent, float spacing)
    {
        GameObject go = Ui(name, parent, Vector2.zero, Vector2.one);
        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        return go.transform;
    }

    static Transform Vertical(string name, Transform parent, float spacing)
    {
        GameObject go = Ui(name, parent, Vector2.zero, Vector2.one);
        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return go.transform;
    }

    static Button Button(string name, Transform parent, TMP_FontAsset font, Sprite sprite, string label)
    {
        GameObject go = Ui(name, parent, Vector2.zero, Vector2.one);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = new Color(0.14f, 0.18f, 0.23f, 0.95f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        LayoutElement element = go.AddComponent<LayoutElement>();
        element.minHeight = 48f;
        element.preferredHeight = 54f;
        TMP_Text text = Text(label, go.transform, font, 16, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one);
        return button;
    }

    static TMP_Text Text(string value, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
    {
        GameObject go = Ui(value.Replace(' ', '_'), parent, Vector2.zero, Vector2.one);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = new Color(0.88f, 0.94f, 1f, 1f);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = Find(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
