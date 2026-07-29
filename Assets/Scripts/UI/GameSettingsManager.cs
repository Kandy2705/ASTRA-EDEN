using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public enum GameControlAction
{
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    Jump,
    Dash,
    Run,
    Attack,
    Skill1,
    Skill2,
    Skill3,
    Interact,
    CompanionCommand,
    CompanionSkill
}

[DefaultExecutionOrder(-1000)]
public sealed class GameSettingsManager : MonoBehaviour
{
    private const string BrightnessPref = "ASTRA_SETTINGS_BRIGHTNESS";
    private const string BloomPref = "ASTRA_SETTINGS_BLOOM";
    private const string BindingPrefPrefix = "ASTRA_BINDING_";

    public const float DefaultBrightness = 1f;
    public const bool DefaultBloomEnabled = true;

    private static readonly Dictionary<GameControlAction, Key> DefaultBindings = new()
    {
        { GameControlAction.MoveForward, Key.W },
        { GameControlAction.MoveBackward, Key.S },
        { GameControlAction.MoveLeft, Key.A },
        { GameControlAction.MoveRight, Key.D },
        { GameControlAction.Jump, Key.Space },
        { GameControlAction.Dash, Key.LeftCtrl },
        { GameControlAction.Run, Key.LeftShift },
        { GameControlAction.Attack, Key.J },
        { GameControlAction.Skill1, Key.Q },
        { GameControlAction.Skill2, Key.E },
        { GameControlAction.Skill3, Key.R },
        { GameControlAction.Interact, Key.F },
        { GameControlAction.CompanionCommand, Key.T },
        { GameControlAction.CompanionSkill, Key.G }
    };

    private static readonly Dictionary<GameControlAction, Key> BindingCache = new();

    private static GameSettingsManager instance;

    private Volume settingsVolume;
    private VolumeProfile settingsProfile;
    private Bloom bloom;
    private Canvas overlayCanvas;
    private Image overlayImage;

    public static float Brightness =>
        Mathf.Clamp01(PlayerPrefs.GetFloat(BrightnessPref, DefaultBrightness));

    public static bool BloomEnabled =>
        PlayerPrefs.GetInt(BloomPref, DefaultBloomEnabled ? 1 : 0) == 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static GameSettingsManager EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameSettingsManager existing =
            FindFirstObjectByType<GameSettingsManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject host = new("GameSettingsManager");
        return host.AddComponent<GameSettingsManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildGraphicsOverrides();
        ApplyGraphicsSettings();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;

        if (settingsProfile != null)
        {
            Destroy(settingsProfile);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyGraphicsSettings();
    }

    private void BuildGraphicsOverrides()
    {
        settingsProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        settingsProfile.name = "ASTRA Runtime Settings";

        bloom = settingsProfile.Add<Bloom>(true);
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1f;
        bloom.intensity.overrideState = true;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.5f;

        settingsVolume = gameObject.AddComponent<Volume>();
        settingsVolume.isGlobal = true;
        settingsVolume.priority = 1000f;
        settingsVolume.sharedProfile = settingsProfile;

        GameObject canvasGO = new("BrightnessOverlay");
        canvasGO.transform.SetParent(transform);
        overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject imageGO = new("OverlayImage");
        imageGO.transform.SetParent(canvasGO.transform, false);
        overlayImage = imageGO.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0f);
        overlayImage.raycastTarget = false;

        RectTransform rt = overlayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static void ApplyGraphics(float brightness, bool bloomEnabled, bool save = true)
    {
        PlayerPrefs.SetFloat(BrightnessPref, Mathf.Clamp01(brightness));
        PlayerPrefs.SetInt(BloomPref, bloomEnabled ? 1 : 0);

        if (save)
        {
            PlayerPrefs.Save();
        }

        EnsureInstance().ApplyGraphicsSettings();
    }

    public void ApplyGraphicsSettings()
    {
        if (bloom == null)
        {
            return;
        }

        // Screen darkening overlay — không ảnh hưởng môi trường 3D
        if (overlayImage == null && overlayCanvas == null)
        {
            RebuildOverlay();
        }

        if (overlayImage != null)
        {
            float b = Mathf.Clamp01(Brightness);
            float alpha = Mathf.Lerp(0.7f, 0f, b);
            overlayImage.color = new Color(0f, 0f, 0f, alpha);
        }

        bool bloomEnabled = BloomEnabled;
        bloom.active = bloomEnabled;
        bloom.intensity.value = bloomEnabled ? 0.25f : 0f;

        Camera[] cameras =
            FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                cameraData.renderPostProcessing = true;
            }
        }
    }

    private void RebuildOverlay()
    {
        GameObject canvasGO = new("BrightnessOverlay");
        canvasGO.transform.SetParent(transform);
        overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject imageGO = new("OverlayImage");
        imageGO.transform.SetParent(canvasGO.transform, false);
        overlayImage = imageGO.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0f);
        overlayImage.raycastTarget = false;

        RectTransform rt = overlayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static Key GetBinding(GameControlAction action)
    {
        if (BindingCache.TryGetValue(action, out Key cached))
        {
            return cached;
        }

        Key fallback = GetDefaultBinding(action);
        int stored = PlayerPrefs.GetInt(BindingPrefPrefix + action, (int)fallback);
        Key loaded = Enum.IsDefined(typeof(Key), stored) ? (Key)stored : fallback;
        BindingCache[action] = loaded;
        return loaded;
    }

    public static Key GetDefaultBinding(GameControlAction action)
    {
        return DefaultBindings.TryGetValue(action, out Key key) ? key : Key.None;
    }

    public static void SetBinding(GameControlAction action, Key key, bool save = true)
    {
        if (key == Key.None)
        {
            key = GetDefaultBinding(action);
        }

        BindingCache[action] = key;
        PlayerPrefs.SetInt(BindingPrefPrefix + action, (int)key);

        if (save)
        {
            PlayerPrefs.Save();
        }
    }

    public static void ResetGraphicsAndBindings()
    {
        PlayerPrefs.SetFloat(BrightnessPref, DefaultBrightness);
        PlayerPrefs.SetInt(BloomPref, DefaultBloomEnabled ? 1 : 0);

        foreach (KeyValuePair<GameControlAction, Key> binding in DefaultBindings)
        {
            BindingCache[binding.Key] = binding.Value;
            PlayerPrefs.SetInt(BindingPrefPrefix + binding.Key, (int)binding.Value);
        }

        PlayerPrefs.Save();
        EnsureInstance().ApplyGraphicsSettings();
    }
}
