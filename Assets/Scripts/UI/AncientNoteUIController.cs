using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup parchment cho Ancient Note. UI được dựng runtime trên HUD_Canvas để hoạt
/// động ở mọi gameplay scene mà không phải sửa tay scene/prefab HUD.
/// </summary>
[DisallowMultipleComponent]
public sealed class AncientNoteUIController : MonoBehaviour
{
    private const string AncientMessage =
        "To the one who survived,\n\n" +
        "If this note has found your hands, then the beast has fallen.\n\n" +
        "<b>Seek the Floating Tree.</b>\n" +
        "There, a secret awaits you — one you must learn before taking the next step.\n\n" +
        "Hidden near its roots lies a map that will guide you to the place where the great tyrant must fall.\n\n" +
        "Only then may peace return to this island.";

    private const string DefaultTitle = "✦  ANCIENT NOTE  ✦";
    private const string DefaultSubtitle = "A whisper left behind by the fallen guardian";

    private static AncientNoteUIController instance;
    private static AncientNoteUIController prefabTemplate;

    [Header("Prefab references")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private CanvasGroup parchmentGroup;
    [SerializeField] private CanvasGroup clueGroup;
    [SerializeField] private CanvasGroup mapGroup;
    [SerializeField] private RectTransform parchment;
    [SerializeField] private RectTransform cluePanel;
    [SerializeField] private RectTransform mapPanel;
    [SerializeField] private Image clueImage;
    [SerializeField] private Image mapImage;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text messageBodyText;

    private Sequence transition;
    private Action onAccepted;
    private Action onCancelled;
    private bool isOpen;
    private bool isClosing;
    private float previousTimeScale;
    private CursorLockMode previousLockMode;
    private bool previousCursorVisible;
    private bool initialized;
    private string titleValue;
    private string subtitleValue;
    private string messageValue;

    public static void Show(
        Sprite floatingTreeClue,
        Sprite tyrantMapClue,
        Action onAccepted,
        Action onCancelled = null,
        AudioClip openSfx = null,
        AncientNoteUIController prefab = null,
        string title = null,
        string subtitle = null,
        string message = null)
    {
        if (prefab != null)
        {
            prefabTemplate = prefab;
        }

        EnsureInstance();
        instance.Open(floatingTreeClue, tyrantMapClue, onAccepted, onCancelled, openSfx, title, subtitle, message);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        AncientNoteUIController existing =
            FindFirstObjectByType<AncientNoteUIController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            instance.InitializeIfNeeded();
            return;
        }

        Canvas canvas = FindScreenCanvas();
        if (canvas == null)
        {
            GameObject canvasObject = new(
                "AncientNoteCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (prefabTemplate != null)
        {
            instance = Instantiate(prefabTemplate, canvas.transform, false);
            instance.name = "AncientNotePopup";
            instance.InitializeIfNeeded();
            return;
        }

        GameObject root = new("AncientNotePopup", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        Stretch(rootRect);
        instance = root.AddComponent<AncientNoteUIController>();
    }

    private static Canvas FindScreenCanvas()
    {
        GameObject hud = GameObject.Find("HUD_Canvas");
        Canvas namedCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
        if (namedCanvas != null && namedCanvas.renderMode != RenderMode.WorldSpace)
        {
            return namedCanvas;
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

        rootGroup ??= GetComponent<CanvasGroup>();
        audioSource ??= GetComponent<AudioSource>();
        parchment ??= transform.Find("Parchment") as RectTransform;
        if (parchment != null)
        {
            parchmentGroup ??= parchment.GetComponent<CanvasGroup>();
            cluePanel ??= parchment.Find("FloatingTreeClue") as RectTransform;
            mapPanel ??= parchment.Find("TyrantMapClue") as RectTransform;
        }

        if (cluePanel != null)
        {
            clueGroup ??= cluePanel.GetComponent<CanvasGroup>();
            Transform image = cluePanel.Find("Illustration");
            clueImage ??= image != null ? image.GetComponent<Image>() : null;
        }

        if (mapPanel != null)
        {
            mapGroup ??= mapPanel.GetComponent<CanvasGroup>();
            Transform image = mapPanel.Find("Illustration");
            mapImage ??= image != null ? image.GetComponent<Image>() : null;
        }

        Transform continueTransform = parchment != null ? parchment.Find("ContinueButton") : null;
        Transform closeTransform = parchment != null ? parchment.Find("CloseButton") : null;
        continueButton ??= continueTransform != null ? continueTransform.GetComponent<Button>() : null;
        closeButton ??= closeTransform != null ? closeTransform.GetComponent<Button>() : null;

        if (rootGroup == null || parchment == null || clueImage == null || mapImage == null ||
            continueButton == null || closeButton == null)
        {
            BuildUi();
        }

        FindTextReferences();
        WireButtons();
        initialized = true;
    }

    private void FindTextReferences()
    {
        if (parchment == null)
        {
            return;
        }

        Transform header = parchment.Find("Header");
        if (header != null)
        {
            titleText ??= header.GetChild(0) != null
                ? header.GetChild(0).GetComponent<TMP_Text>()
                : null;
            subtitleText ??= header.childCount > 1
                ? header.GetChild(1).GetComponent<TMP_Text>()
                : null;
        }

        Transform messageSection = parchment.Find("AncientMessage");
        if (messageSection != null)
        {
            messageBodyText ??= messageSection.Find("Text") != null
                ? messageSection.Find("Text").GetComponent<TMP_Text>()
                : null;
        }
    }

    private void ApplyNoteContent()
    {
        if (titleText != null)
        {
            titleText.text = string.IsNullOrEmpty(titleValue) ? DefaultTitle : titleValue;
        }

        if (subtitleText != null)
        {
            subtitleText.text = string.IsNullOrEmpty(subtitleValue) ? DefaultSubtitle : subtitleValue;
        }

        if (messageBodyText != null)
        {
            string resolved = string.IsNullOrEmpty(messageValue) ? AncientMessage : messageValue;
            messageBodyText.text = resolved;
            messageBodyText.fontSize = resolved.Length > 900 ? 19f : 23f;
        }
    }

    private void Update()
    {
        if (isOpen && !isClosing && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseAndAccept();
        }
    }

    private void Open(
        Sprite treeSprite,
        Sprite routeSprite,
        Action accepted,
        Action cancelled,
        AudioClip sfx,
        string title,
        string subtitle,
        string message)
    {
        if (isOpen)
        {
            return;
        }

        onAccepted = accepted;
        onCancelled = cancelled;
        titleValue = title;
        subtitleValue = subtitle;
        messageValue = message;
        isOpen = true;
        isClosing = false;
        clueImage.sprite = treeSprite;
        clueImage.enabled = treeSprite != null;
        mapImage.sprite = routeSprite;
        mapImage.enabled = routeSprite != null;

        ApplyNoteContent();

        previousTimeScale = Time.timeScale;
        previousLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;
        parchmentGroup.alpha = 0f;
        parchment.localScale = Vector3.one * 0.8f;
        parchment.anchoredPosition = new Vector2(0f, -36f);
        parchment.localRotation = Quaternion.Euler(0f, 0f, -2.8f);
        clueGroup.alpha = 0f;
        mapGroup.alpha = 0f;
        cluePanel.anchoredPosition += Vector2.right * 28f;
        mapPanel.anchoredPosition += Vector2.right * 36f;

        StopTransition();
        transition = Sequence.Create(useUnscaledTime: true)
            .Group(Tween.Alpha(rootGroup, 0f, 1f, 0.28f, Ease.OutCubic))
            .Group(Tween.Alpha(parchmentGroup, 0f, 1f, 0.32f, Ease.OutCubic))
            .Group(Tween.Scale(parchment, Vector3.one * 0.8f, Vector3.one, 0.42f, Ease.OutBack))
            .Group(Tween.UIAnchoredPosition(parchment, new Vector2(0f, -36f), Vector2.zero, 0.4f, Ease.OutCubic))
            .Group(Tween.LocalRotation(parchment, new Vector3(0f, 0f, -2.8f), Vector3.zero, 0.46f, Ease.OutBack))
            .Group(Tween.Alpha(clueGroup, 0f, 1f, 0.3f, Ease.OutCubic, startDelay: 0.18f))
            .Group(Tween.UIAnchoredPosition(cluePanel, cluePanel.anchoredPosition, cluePanel.anchoredPosition - Vector2.right * 28f, 0.34f, Ease.OutCubic, startDelay: 0.18f))
            .Group(Tween.Alpha(mapGroup, 0f, 1f, 0.32f, Ease.OutCubic, startDelay: 0.27f))
            .Group(Tween.UIAnchoredPosition(mapPanel, mapPanel.anchoredPosition, mapPanel.anchoredPosition - Vector2.right * 36f, 0.38f, Ease.OutCubic, startDelay: 0.27f));

        if (sfx != null)
        {
            audioSource.clip = sfx;
            audioSource.Play();
        }
    }

    private void CloseAndAccept()
    {
        if (!isOpen || isClosing)
        {
            return;
        }

        isClosing = true;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
        StopTransition();

        transition = Sequence.Create(useUnscaledTime: true)
            .Group(Tween.Alpha(clueGroup, clueGroup.alpha, 0f, 0.16f, Ease.InCubic))
            .Group(Tween.Alpha(mapGroup, mapGroup.alpha, 0f, 0.16f, Ease.InCubic))
            .Group(Tween.Alpha(parchmentGroup, parchmentGroup.alpha, 0f, 0.28f, Ease.InCubic))
            .Group(Tween.Scale(parchment, parchment.localScale, Vector3.one * 0.86f, 0.3f, Ease.InBack))
            .Group(Tween.UIAnchoredPosition(parchment, parchment.anchoredPosition, new Vector2(0f, -28f), 0.28f, Ease.InCubic))
            .Group(Tween.LocalRotation(parchment, parchment.localEulerAngles, new Vector3(0f, 0f, 2f), 0.3f, Ease.InCubic))
            .Group(Tween.Alpha(rootGroup, rootGroup.alpha, 0f, 0.32f, Ease.InCubic))
            .OnComplete(this, static target => target.FinishClose(true));
    }

    private void FinishClose(bool accepted)
    {
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousLockMode;
        Cursor.visible = previousCursorVisible;
        gameObject.SetActive(false);
        isOpen = false;
        isClosing = false;

        Action callback = accepted ? onAccepted : onCancelled;
        onAccepted = null;
        onCancelled = null;
        callback?.Invoke();
    }

    private void BuildUi()
    {
        rootGroup = GetComponent<CanvasGroup>();
        if (rootGroup == null) rootGroup = gameObject.AddComponent<CanvasGroup>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.ignoreListenerPause = true;

        Image overlay = CreateImage("DimOverlay", transform, new Color(0.025f, 0.012f, 0.05f, 0.88f));
        Stretch(overlay.rectTransform);
        Button overlayButton = overlay.gameObject.AddComponent<Button>();
        overlayButton.transition = Selectable.Transition.None;

        parchment = CreateRect("Parchment", transform);
        parchment.anchorMin = new Vector2(0.07f, 0.075f);
        parchment.anchorMax = new Vector2(0.93f, 0.925f);
        parchment.offsetMin = Vector2.zero;
        parchment.offsetMax = Vector2.zero;
        Image parchmentImage = parchment.gameObject.AddComponent<Image>();
        parchmentImage.color = new Color(0.73f, 0.58f, 0.34f, 1f);
        Outline parchmentOutline = parchment.gameObject.AddComponent<Outline>();
        parchmentOutline.effectColor = new Color(0.55f, 0.22f, 0.9f, 0.95f);
        parchmentOutline.effectDistance = new Vector2(4f, -4f);
        parchmentGroup = parchment.gameObject.AddComponent<CanvasGroup>();

        AddBorder(parchment, new Color(0.93f, 0.69f, 0.24f, 0.9f));
        AddHeader();
        AddMessageSection();
        AddClueSections();
        AddButtons();
    }

    private void AddHeader()
    {
        RectTransform header = CreateRect("Header", parchment);
        header.anchorMin = new Vector2(0.04f, 0.855f);
        header.anchorMax = new Vector2(0.96f, 0.965f);
        header.offsetMin = header.offsetMax = Vector2.zero;

        TMP_Text title = CreateText(header, "✦  ANCIENT NOTE  ✦", 38f, new Color(0.24f, 0.08f, 0.28f, 1f), TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        titleText = title;
        TMP_Text subtitle = CreateText(header, "A whisper left behind by the fallen guardian", 17f, new Color(0.36f, 0.18f, 0.25f, 0.92f), TextAlignmentOptions.Center);
        subtitle.rectTransform.anchorMin = new Vector2(0f, 0f);
        subtitle.rectTransform.anchorMax = new Vector2(1f, 0.36f);
        subtitle.rectTransform.offsetMin = subtitle.rectTransform.offsetMax = Vector2.zero;
        subtitleText = subtitle;
        title.rectTransform.anchorMin = new Vector2(0f, 0.3f);
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
    }

    private void AddMessageSection()
    {
        RectTransform section = CreateRect("AncientMessage", parchment);
        section.anchorMin = new Vector2(0.045f, 0.15f);
        section.anchorMax = new Vector2(0.485f, 0.84f);
        section.offsetMin = section.offsetMax = Vector2.zero;
        Image background = section.gameObject.AddComponent<Image>();
        background.color = new Color(0.96f, 0.86f, 0.62f, 0.78f);

        TMP_Text message = CreateText(section, AncientMessage, 23f, new Color(0.16f, 0.09f, 0.09f, 1f), TextAlignmentOptions.TopLeft);
        message.textWrappingMode = TextWrappingModes.Normal;
        message.lineSpacing = 5f;
        message.richText = true;
        message.rectTransform.anchorMin = Vector2.zero;
        message.rectTransform.anchorMax = Vector2.one;
        message.rectTransform.offsetMin = new Vector2(34f, 28f);
        message.rectTransform.offsetMax = new Vector2(-34f, -28f);
        messageBodyText = message;
    }

    private void AddClueSections()
    {
        cluePanel = CreateCluePanel(
            "FloatingTreeClue",
            new Vector2(0.515f, 0.505f),
            new Vector2(0.955f, 0.84f),
            "THE FLOATING TREE",
            out clueImage,
            out clueGroup);

        mapPanel = CreateCluePanel(
            "TyrantMapClue",
            new Vector2(0.515f, 0.15f),
            new Vector2(0.955f, 0.48f),
            "THE TYRANT'S PATH",
            out mapImage,
            out mapGroup);
    }

    private RectTransform CreateCluePanel(
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string caption,
        out Image illustration,
        out CanvasGroup group)
    {
        RectTransform panel = CreateRect(objectName, parchment);
        panel.anchorMin = anchorMin;
        panel.anchorMax = anchorMax;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.13f, 0.055f, 0.19f, 0.96f);
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.94f, 0.69f, 0.25f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
        group = panel.gameObject.AddComponent<CanvasGroup>();

        illustration = CreateImage("Illustration", panel, Color.white);
        illustration.preserveAspect = true;
        illustration.rectTransform.anchorMin = new Vector2(0.025f, 0.14f);
        illustration.rectTransform.anchorMax = new Vector2(0.975f, 0.97f);
        illustration.rectTransform.offsetMin = illustration.rectTransform.offsetMax = Vector2.zero;

        TMP_Text label = CreateText(panel, caption, 18f, new Color(1f, 0.79f, 0.35f, 1f), TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 4f;
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(1f, 0.15f);
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return panel;
    }

    private void AddButtons()
    {
        continueButton = CreateButton("ContinueButton", parchment, "CONTINUE", new Vector2(0.79f, 0.075f));
        closeButton = CreateButton("CloseButton", parchment, "CLOSE", new Vector2(0.91f, 0.075f));
    }

    private void WireButtons()
    {
        continueButton.onClick.RemoveListener(CloseAndAccept);
        closeButton.onClick.RemoveListener(CloseAndAccept);
        continueButton.onClick.AddListener(CloseAndAccept);
        closeButton.onClick.AddListener(CloseAndAccept);
    }

    private static Button CreateButton(string objectName, RectTransform parent, string label, Vector2 anchor)
    {
        RectTransform rect = CreateRect(objectName, parent);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(155f, 48f);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.19f, 0.065f, 0.27f, 1f);
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.96f, 0.72f, 0.3f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.06f, 1.2f, 1f);
        colors.pressedColor = new Color(0.72f, 0.68f, 0.78f, 1f);
        button.colors = colors;

        TMP_Text text = CreateText(rect, label, 18f, new Color(1f, 0.82f, 0.43f, 1f), TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform);
        return button;
    }

    private static void AddBorder(RectTransform parent, Color color)
    {
        const float thickness = 4f;
        Image top = CreateImage("GoldBorderTop", parent, color);
        SetAnchors(top.rectTransform, new Vector2(0.018f, 0.982f), new Vector2(0.982f, 0.982f));
        top.rectTransform.sizeDelta = new Vector2(0f, thickness);
        Image bottom = CreateImage("GoldBorderBottom", parent, color);
        SetAnchors(bottom.rectTransform, new Vector2(0.018f, 0.018f), new Vector2(0.982f, 0.018f));
        bottom.rectTransform.sizeDelta = new Vector2(0f, thickness);
        Image left = CreateImage("GoldBorderLeft", parent, color);
        SetAnchors(left.rectTransform, new Vector2(0.018f, 0.018f), new Vector2(0.018f, 0.982f));
        left.rectTransform.sizeDelta = new Vector2(thickness, 0f);
        Image right = CreateImage("GoldBorderRight", parent, color);
        SetAnchors(right.rectTransform, new Vector2(0.982f, 0.018f), new Vector2(0.982f, 0.982f));
        right.rectTransform.sizeDelta = new Vector2(thickness, 0f);
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.anchoredPosition = Vector2.zero;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject gameObject = new(objectName, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string value, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect("Text", parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void StopTransition()
    {
        if (transition.isAlive)
        {
            transition.Stop();
        }
    }

#if UNITY_EDITOR
    /// <summary>Chỉ dùng bởi editor builder để tạo prefab authoring từ layout runtime.</summary>
    public void EditorBuildPrefabLayout()
    {
        InitializeIfNeeded();
    }
#endif

    private void OnDestroy()
    {
        StopTransition();
        if (instance == this)
        {
            instance = null;
        }

        if (isOpen)
        {
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousLockMode;
            Cursor.visible = previousCursorVisible;
            onCancelled?.Invoke();
        }
    }
}
