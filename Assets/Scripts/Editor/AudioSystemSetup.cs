#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AudioSystemSetup
{
    const string AudioFolder = "Assets/_Project/ScriptableObjects/Audio";
    const string CatalogPath = AudioFolder + "/SO_SceneAudioCatalog.asset";
    const string ResourcesCatalogPath = "Assets/Resources/ASTRA/SO_SceneAudioCatalog.asset";

    [MenuItem("ASTRA EDEN/Audio/1. Create Scene Audio Assets")]
    public static void CreateSceneAudioAssets()
    {
        EnsureFolder(AudioFolder);

        SceneAudioProfile mainMenu = CreateOrLoadProfile("SO_SceneAudio_MainMenu", "MainMenu");
        SceneAudioProfile loading = CreateOrLoadProfile("SO_SceneAudio_Loading", "Loading");
        SceneAudioProfile beacon = CreateOrLoadProfile("SO_SceneAudio_Beacon_Camp", "Beacon_Camp");
        SceneAudioProfile world = CreateOrLoadProfile("SO_SceneAudio_World_Eden7", "World_Eden7");

        SceneAudioCatalog catalog = AssetDatabase.LoadAssetAtPath<SceneAudioCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SceneAudioCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        SerializedObject so = new SerializedObject(catalog);
        so.FindProperty("loadingProfile").objectReferenceValue = loading;
        SerializedProperty list = so.FindProperty("sceneProfiles");
        list.ClearArray();
        AddProfile(list, mainMenu);
        AddProfile(list, beacon);
        AddProfile(list, world);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(catalog);
        SyncResourcesCatalog(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("[AudioSetup] Đã tạo catalog + profiles. Gán AudioClip vào từng SO_SceneAudio_*.");
    }

    [MenuItem("ASTRA EDEN/Audio/3. Sync Resources Catalog")]
    public static void SyncResourcesCatalogMenu()
    {
        SceneAudioCatalog catalog = AssetDatabase.LoadAssetAtPath<SceneAudioCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError("[AudioSetup] Không tìm thấy catalog tại _Project/ScriptableObjects/Audio.");
            return;
        }

        SyncResourcesCatalog(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("[AudioSetup] Đã đồng bộ Resources/ASTRA/SO_SceneAudioCatalog từ _Project.");
    }

    static void SyncResourcesCatalog(SceneAudioCatalog source)
    {
        EnsureFolder("Assets/Resources/ASTRA");

        SceneAudioCatalog target = AssetDatabase.LoadAssetAtPath<SceneAudioCatalog>(ResourcesCatalogPath);
        if (target == null)
        {
            target = ScriptableObject.CreateInstance<SceneAudioCatalog>();
            AssetDatabase.CreateAsset(target, ResourcesCatalogPath);
        }

        SerializedObject sourceSo = new SerializedObject(source);
        SerializedObject targetSo = new SerializedObject(target);
        targetSo.FindProperty("loadingProfile").objectReferenceValue =
            sourceSo.FindProperty("loadingProfile").objectReferenceValue;

        SerializedProperty sourceList = sourceSo.FindProperty("sceneProfiles");
        SerializedProperty targetList = targetSo.FindProperty("sceneProfiles");
        targetList.ClearArray();
        for (int i = 0; i < sourceList.arraySize; i++)
        {
            int index = targetList.arraySize;
            targetList.InsertArrayElementAtIndex(index);
            targetList.GetArrayElementAtIndex(index).objectReferenceValue =
                sourceList.GetArrayElementAtIndex(i).objectReferenceValue;
        }

        targetSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    [MenuItem("ASTRA EDEN/Audio/2. Install Audio Bootstrap In Scenes")]
    public static void InstallAudioBootstrapInScenes()
    {
        CreateSceneAudioAssets();

        SceneAudioCatalog catalog = AssetDatabase.LoadAssetAtPath<SceneAudioCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError("[AudioSetup] Chưa có catalog.");
            return;
        }

        InstallInScene("Assets/Scenes/MainMenu.unity", catalog);
        InstallInScene("Assets/Scenes/Loading.unity", catalog);
        InstallInScene("Assets/Scenes/World_Eden7.unity", catalog);
        InstallInScene("Assets/Scenes/Beacon_Camp.unity", catalog);
        InstallBeachZoneInWorld();

        AssetDatabase.SaveAssets();
        Debug.Log("[AudioSetup] Đã cài SceneAudioBootstrap + AudioSettingsUI (MainMenu).");
    }

    [MenuItem("ASTRA EDEN/Audio/Run ALL (Create + Install)")]
    public static void RunAll()
    {
        InstallAudioBootstrapInScenes();
    }

    static void InstallInScene(string scenePath, SceneAudioCatalog catalog)
    {
        if (!File.Exists(scenePath))
        {
            Debug.LogWarning($"[AudioSetup] Không tìm thấy scene: {scenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        SceneAudioBootstrap bootstrap = Object.FindFirstObjectByType<SceneAudioBootstrap>(FindObjectsInactive.Include);
        if (bootstrap == null)
        {
            GameObject host = new GameObject("SceneAudioBootstrap");
            bootstrap = host.AddComponent<SceneAudioBootstrap>();
        }

        SerializedObject bootstrapSo = new SerializedObject(bootstrap);
        bootstrapSo.FindProperty("catalogOverride").objectReferenceValue = catalog;
        bootstrapSo.FindProperty("profileOverride").objectReferenceValue = null;
        bootstrapSo.FindProperty("applyOnStart").boolValue = true;
        bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

        if (scenePath.Contains("MainMenu"))
        {
            InstallAudioSettingsUi();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void InstallAudioSettingsUi()
    {
        SettingsPanelController settings = Object.FindFirstObjectByType<SettingsPanelController>(FindObjectsInactive.Include);
        if (settings == null)
        {
            Debug.LogWarning("[AudioSetup] Không tìm thấy SettingsPanelController trong MainMenu.");
            return;
        }

        AudioSettingsUI audioUi = settings.GetComponent<AudioSettingsUI>();
        if (audioUi == null)
        {
            audioUi = settings.gameObject.AddComponent<AudioSettingsUI>();
        }

        Slider[] sliders = settings.GetComponentsInChildren<Slider>(true);
        if (sliders.Length < 3)
        {
            Debug.LogWarning("[AudioSetup] AudioContent cần ít nhất 3 Slider.");
            return;
        }

        SerializedObject uiSo = new SerializedObject(audioUi);
        uiSo.FindProperty("masterSlider").objectReferenceValue = sliders[0];
        uiSo.FindProperty("musicSlider").objectReferenceValue = sliders[1];
        uiSo.FindProperty("sfxSlider").objectReferenceValue = sliders.Length > 2 ? sliders[2] : null;
        uiSo.FindProperty("ambientSlider").objectReferenceValue = sliders.Length > 3 ? sliders[3] : null;
        uiSo.FindProperty("beachSlider").objectReferenceValue = sliders.Length > 4 ? sliders[4] : null;
        uiSo.ApplyModifiedPropertiesWithoutUndo();
    }

    static void InstallBeachZoneInWorld()
    {
        const string scenePath = "Assets/Scenes/World_Eden7.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        BeachAudioZone existing = Object.FindFirstObjectByType<BeachAudioZone>(FindObjectsInactive.Include);
        if (existing != null)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }

        GameObject zone = new GameObject("BeachAudioZone_IntroBeach");
        zone.transform.position = new Vector3(0f, 0f, 0f);
        BoxCollider box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(120f, 30f, 120f);
        zone.AddComponent<BeachAudioZone>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AudioSetup] Đã tạo BeachAudioZone_IntroBeach — chỉnh position/size theo bãi biển thật trong World_Eden7.");
    }

    static SceneAudioProfile CreateOrLoadProfile(string assetName, string sceneName)
    {
        string path = $"{AudioFolder}/{assetName}.asset";
        SceneAudioProfile profile = AssetDatabase.LoadAssetAtPath<SceneAudioProfile>(path);
        if (profile != null)
        {
            profile.sceneName = sceneName;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        profile = ScriptableObject.CreateInstance<SceneAudioProfile>();
        profile.sceneName = sceneName;
        profile.enterCrossfadeDuration = sceneName == "Loading" ? 1.25f : 2f;
        profile.ambientVolume = sceneName == "World_Eden7" ? 0.65f : 0.5f;
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    static void AddProfile(SerializedProperty list, SceneAudioProfile profile)
    {
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = profile;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif