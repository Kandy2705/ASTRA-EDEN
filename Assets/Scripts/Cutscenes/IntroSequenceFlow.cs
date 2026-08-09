using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Owns only the opening-intro progression flag and scene routing. Gameplay
/// save data remains in GameDataManager and is never erased by the demo reset.
/// </summary>
public static class IntroSequenceFlow
{
    private const string IntroWatchedKey = "ASTRA_OPENING_INTRO_WATCHED";

    public const string FirstIntroScene = "CutScene 1";
    public const string GameplayScene = "World_Eden7";

    private static bool sequenceActive;

    public static bool HasWatchedIntro => PlayerPrefs.GetInt(IntroWatchedKey, 0) == 1;
    public static bool ShouldPlayIntro => !HasWatchedIntro;
    public static bool IsSequenceActive => sequenceActive;

    public static void BeginIntroSequence()
    {
        sequenceActive = true;
        Time.timeScale = 1f;
        Debug.Log("[IntroSequence] Bắt đầu chuỗi CutScene 1 → 2 → 3 → 4.");
        SceneTransitionService.Load(
            FirstIntroScene,
            useLoadingScreen: true,
            suppressLoadingAudio: true);
    }

    public static void MarkIntroCompleted()
    {
        PlayerPrefs.SetInt(IntroWatchedKey, 1);
        PlayerPrefs.Save();
        sequenceActive = false;
        Time.timeScale = 1f;
        Debug.Log("[IntroSequence] Đã xem xong 4 opening cutscene.");
    }

    public static void SkipIntroToGameplay()
    {
        if (!sequenceActive)
        {
            return;
        }

        MarkIntroCompleted();
        Debug.Log("[IntroSequence] Người chơi Skip Intro -> Loading -> World_Eden7.");
        SceneTransitionService.Load(
            GameplayScene,
            useLoadingScreen: true,
            suppressLoadingAudio: true);
    }

    public static void ResetIntroForDemo()
    {
        PlayerPrefs.DeleteKey(IntroWatchedKey);
        PlayerPrefs.Save();
        sequenceActive = false;
        Debug.Log("[IntroSequence] Demo reset: trạng thái intro = CHƯA XEM. Save gameplay được giữ nguyên.");
    }

    public static void CancelActiveSequence()
    {
        sequenceActive = false;
    }

    public static bool TryGetNextIntroScene(string currentScene, out string nextScene)
    {
        switch (currentScene)
        {
            case "CutScene 1":
                nextScene = "CutScene 2";
                return true;
            case "CutScene 2":
                nextScene = "CutScene 3";
                return true;
            case "CutScene 3":
                nextScene = "CutScene 4";
                return true;
            default:
                nextScene = string.Empty;
                return false;
        }
    }
}

/// <summary>
/// Installs a tiny end-of-Timeline bridge only while the opening sequence was
/// started from Main Menu. Opening individual cutscene scenes in Editor remains
/// safe and does not automatically jump to the next scene.
/// </summary>
internal static class IntroSequenceRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IntroSequenceFlow.IsSequenceActive)
        {
            return;
        }

        if (IsOpeningIntroScene(scene.name))
        {
            IntroSequenceSkipUI.Install();
        }

        if (!IntroSequenceFlow.TryGetNextIntroScene(scene.name, out string nextScene))
        {
            return;
        }

        PlayableDirector director = Object.FindFirstObjectByType<PlayableDirector>();
        if (director == null)
        {
            Debug.LogError($"[IntroSequence] Scene '{scene.name}' không tìm thấy PlayableDirector.");
            return;
        }

        IntroSequenceSceneBridge bridge = director.GetComponent<IntroSequenceSceneBridge>();
        if (bridge == null)
        {
            bridge = director.gameObject.AddComponent<IntroSequenceSceneBridge>();
        }

        bridge.Configure(director, nextScene);
    }

    private static bool IsOpeningIntroScene(string sceneName)
    {
        return sceneName == "CutScene 1" ||
               sceneName == "CutScene 2" ||
               sceneName == "CutScene 3" ||
               sceneName == "CutScene 4";
    }
}

internal sealed class IntroSequenceSkipUI : MonoBehaviour
{
    private void LateUpdate()
    {
        // Gameplay CursorManager may run after scene load and lock the cursor.
        // While the cinematic Skip button exists it must always remain clickable.
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }
    }

    public static void Install()
    {
        if (Object.FindFirstObjectByType<IntroSequenceSkipUI>() != null)
        {
            return;
        }

        EnsureEventSystem();

        GameObject root = new("OpeningIntroSkipUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(IntroSequenceSkipUI));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject buttonObject = new("Button_SkipOpeningIntro", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
        buttonObject.transform.SetParent(root.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-32f, -28f);
        buttonRect.sizeDelta = new Vector2(210f, 54f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.035f, 0.02f, 0.06f, 0.9f);

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.93f, 0.68f, 0.24f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.88f, 0.58f, 1f);
        colors.pressedColor = new Color(0.78f, 0.58f, 0.28f, 1f);
        button.colors = colors;

        GameObject textObject = new("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 5f);
        textRect.offsetMax = new Vector2(-10f, -5f);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.9f, 0.67f, 1f);
        label.text = "SKIP INTRO";
        label.raycastTarget = false;

        IntroSequenceSkipUI skipUI = root.GetComponent<IntroSequenceSkipUI>();
        button.onClick.AddListener(skipUI.Skip);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log($"[IntroSequence] Đã tạo nút SKIP INTRO trong {SceneManager.GetActiveScene().name}.");
    }

    private void Skip()
    {
        Button button = GetComponentInChildren<Button>();
        if (button != null)
        {
            button.interactable = false;
        }

        IntroSequenceFlow.SkipIntroToGameplay();
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new("EventSystem", typeof(EventSystem));
        System.Type inputSystemModule = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModule != null)
        {
            eventSystemObject.AddComponent(inputSystemModule);
        }
        else
        {
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }
    }
}

[DisallowMultipleComponent]
internal sealed class IntroSequenceSceneBridge : MonoBehaviour
{
    private PlayableDirector director;
    private string nextSceneName;
    private bool transitionRequested;

    public void Configure(PlayableDirector playableDirector, string nextScene)
    {
        director = playableDirector;
        nextSceneName = nextScene;
        transitionRequested = false;

        // Cutscenes must continue even if a gameplay/pause object left the
        // global timescale at zero before entering the scene.
        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        director.stopped -= HandleDirectorStopped;
        director.stopped += HandleDirectorStopped;

        if (director.state != PlayState.Playing)
        {
            director.Play();
        }
    }

    private void OnDestroy()
    {
        if (director != null)
        {
            director.stopped -= HandleDirectorStopped;
        }
    }

    private void LateUpdate()
    {
        if (director == null || transitionRequested || !IntroSequenceFlow.IsSequenceActive)
        {
            return;
        }

        double duration = director.duration;
        if (!double.IsInfinity(duration) && duration > 0d && director.time >= duration - 0.03d)
        {
            LoadNextScene();
        }
    }

    private void HandleDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector.duration <= 0d || stoppedDirector.time + 0.15d < stoppedDirector.duration)
        {
            return;
        }

        LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (transitionRequested || string.IsNullOrWhiteSpace(nextSceneName))
        {
            return;
        }

        transitionRequested = true;
        Debug.Log($"[IntroSequence] {SceneManager.GetActiveScene().name} hoàn tất → {nextSceneName}.");
        // Every cutscene already fades fully to black at its end and the next
        // one fades back in. Load it directly so no Loading screen/profile is
        // inserted between cinematic chapters.
        SceneTransitionService.Load(nextSceneName, useLoadingScreen: false);
    }
}
