using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD objective nhỏ, luôn hiển thị objective cốt truyện hiện tại.</summary>
[DisallowMultipleComponent]
public sealed class ObjectiveHUDController : MonoBehaviour
{
    private static ObjectiveHUDController instance;
    private static ObjectiveHUDController prefabTemplate;

    [Header("Prefab references")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text objectiveText;

    private Sequence transition;
    private bool initialized;

    public static void RegisterPrefab(ObjectiveHUDController prefab)
    {
        if (prefab != null)
        {
            prefabTemplate = prefab;
        }
    }

    public static void ShowObjective(string objective)
    {
        EnsureInstance();
        instance.SetObjective(objective);
    }

    public static void EnsureVisibleFromSave()
    {
        string objective = ZoneObjectiveManager.Instance != null
            ? ZoneObjectiveManager.Instance.CurrentObjective
            : GameDataManager.Instance != null
                ? GameDataManager.Instance.CurrentObjective
                : string.Empty;

        if (!string.IsNullOrWhiteSpace(objective))
        {
            ShowObjective(objective);
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        ObjectiveHUDController existing =
            FindFirstObjectByType<ObjectiveHUDController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            instance.InitializeIfNeeded();
            return;
        }

        Canvas canvas = FindHudCanvas();
        if (canvas == null)
        {
            GameObject canvasObject = new("ObjectiveCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (prefabTemplate != null)
        {
            instance = Instantiate(prefabTemplate, canvas.transform, false);
            instance.name = "CurrentObjectiveHUD";
            instance.InitializeIfNeeded();
            return;
        }

        GameObject root = new("CurrentObjectiveHUD", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-42f, -470f);
        rect.sizeDelta = new Vector2(440f, 98f);
        instance = root.AddComponent<ObjectiveHUDController>();
    }

    private static Canvas FindHudCanvas()
    {
        GameObject hud = GameObject.Find("HUD_Canvas");
        Canvas canvas = hud != null ? hud.GetComponent<Canvas>() : null;
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
        {
            return canvas;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].renderMode != RenderMode.WorldSpace)
            {
                return canvases[i];
            }
        }
        return null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        group ??= GetComponent<CanvasGroup>();
        panel ??= transform as RectTransform;
        if (objectiveText == null)
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (!labels[i].text.Contains("CURRENT OBJECTIVE"))
                {
                    objectiveText = labels[i];
                    break;
                }
            }
        }

        if (group == null || panel == null || objectiveText == null)
        {
            BuildUi();
        }

        panel.anchoredPosition = new Vector2(-42f, -470f);
        initialized = true;
    }

    private void BuildUi()
    {
        group ??= GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        panel ??= transform as RectTransform;

        Image background = GetComponent<Image>();
        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
            background.color = new Color(0.055f, 0.018f, 0.09f, 0.9f);
        }

        Outline outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.89f, 0.62f, 0.22f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        GameObject accentObject = new("MagicAccent", typeof(RectTransform), typeof(Image));
        RectTransform accent = accentObject.GetComponent<RectTransform>();
        accent.SetParent(transform, false);
        accent.anchorMin = new Vector2(0f, 0f);
        accent.anchorMax = new Vector2(0f, 1f);
        accent.pivot = new Vector2(0f, 0.5f);
        accent.sizeDelta = new Vector2(7f, 0f);
        accent.anchoredPosition = Vector2.zero;
        accentObject.GetComponent<Image>().color = new Color(0.62f, 0.27f, 1f, 1f);

        TMP_Text title = CreateText("CURRENT OBJECTIVE", 14f, new Color(0.91f, 0.68f, 0.29f, 1f));
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 3f;
        title.rectTransform.anchorMin = new Vector2(0f, 0.58f);
        title.rectTransform.anchorMax = new Vector2(1f, 0.93f);
        title.rectTransform.offsetMin = new Vector2(24f, 0f);
        title.rectTransform.offsetMax = new Vector2(-16f, 0f);

        objectiveText = CreateText(string.Empty, 23f, new Color(0.96f, 0.92f, 0.84f, 1f));
        objectiveText.fontStyle = FontStyles.Bold;
        objectiveText.rectTransform.anchorMin = new Vector2(0f, 0.08f);
        objectiveText.rectTransform.anchorMax = new Vector2(1f, 0.63f);
        objectiveText.rectTransform.offsetMin = new Vector2(24f, 0f);
        objectiveText.rectTransform.offsetMax = new Vector2(-16f, 0f);
    }

    private TMP_Text CreateText(string value, float fontSize, Color color)
    {
        GameObject child = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        TMP_Text text = child.GetComponent<TMP_Text>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    private void SetObjective(string objective)
    {
        if (string.IsNullOrWhiteSpace(objective))
        {
            gameObject.SetActive(false);
            return;
        }

        objectiveText.text = objective;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (transition.isAlive) transition.Stop();
        group.alpha = 0f;
        panel.anchoredPosition = new Vector2(30f, -470f);
        transition = Sequence.Create(useUnscaledTime: true)
            .Group(Tween.Alpha(group, 0f, 1f, 0.32f, Ease.OutCubic))
            .Group(Tween.UIAnchoredPosition(panel, new Vector2(30f, -470f), new Vector2(-42f, -470f), 0.42f, Ease.OutBack));
    }

    private void OnDestroy()
    {
        if (transition.isAlive) transition.Stop();
        if (instance == this) instance = null;
    }
}
