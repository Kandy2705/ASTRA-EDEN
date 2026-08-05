using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gắn trên root prefab GameplayUI_Root. Ẩn panel combat-only ở hub và đảm bảo EventSystem.
/// </summary>
[DisallowMultipleComponent]
public class GameplayUISceneBootstrap : MonoBehaviour
{
    [SerializeField] private string[] hubSceneNames = { "Beacon_Camp", "MainMenu" };

    [Header("Combat-only panels (ẩn ở hub)")]
    [SerializeField]
    private string[] combatOnlyPanelNames =
    {
        "BossHUDPanel",
        "ZoneResultPanel"
    };

    private Button debugIconButton;
    private GameObject playerDebugPanel;
    private TMP_InputField damageInput;
    private TMP_Text debugStatusText;
    private Slider timeSpeedSlider;
    private TMP_Text timeSpeedValueText;
    private PopupTween debugPanelTween;
    private CharacterHealth debugPlayerHealth;
    private PlayerCombatController debugPlayerCombat;
    private float nextDebugRefreshTime;
    private bool debugUiInitialized;
    private GameObject deathRecoveryNotice;
    private TMP_Text deathRecoveryNoticeText;
    private PopupTween deathRecoveryNoticeTween;
    private Coroutine deathRecoveryNoticeRoutine;

    private void Awake()
    {
        EnsureEventSystem();
        ApplyHubVisibility();
        WirePlayerStatusHud();
        EnsurePlayerDebugUi();
    }

    private void Start()
    {
        // Player có thể spawn sau UI 1 frame (hub / portal).
        WirePlayerStatusHud();
        EnsurePlayerDebugUi();
    }

    private void Update()
    {
        if (playerDebugPanel == null ||
            !playerDebugPanel.activeSelf ||
            Time.unscaledTime < nextDebugRefreshTime)
        {
            return;
        }

        nextDebugRefreshTime = Time.unscaledTime + 0.2f;
        RefreshDebugStatus();
    }

    /// <summary>
    /// HUD_PlayerStatusPanel / CharacterStatsHUD không serialize CharacterHealth của player
    /// (khác scene / prefab). Bind runtime theo tag Player — cần cho Beacon_Camp.
    /// </summary>
    void WirePlayerStatusHud()
    {
        CharacterStatsHUD[] statusHuds = GetComponentsInChildren<CharacterStatsHUD>(true);
        for (int i = 0; i < statusHuds.Length; i++)
        {
            if (statusHuds[i] != null)
            {
                statusHuds[i].TryBindPlayerHealth(force: true);
                statusHuds[i].Refresh();
            }
        }

        // Gold HUD re-find inventory khi vào camp.
        HUDTopStatusController[] topStatus = GetComponentsInChildren<HUDTopStatusController>(true);
        for (int i = 0; i < topStatus.Length; i++)
        {
            if (topStatus[i] != null)
            {
                topStatus[i].ForceRefreshCurrency();
            }
        }
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        AddUiInputModule(eventSystem);
    }

    private void ApplyHubVisibility()
    {
        if (!IsHubScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        for (int i = 0; i < combatOnlyPanelNames.Length; i++)
        {
            string panelName = combatOnlyPanelNames[i];
            if (string.IsNullOrEmpty(panelName))
            {
                continue;
            }

            Transform panel = transform.Find(panelName);
            if (panel == null)
            {
                panel = FindChildRecursive(transform, panelName);
            }

            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }
        }
    }

    private bool IsHubScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || hubSceneNames == null)
        {
            return false;
        }

        for (int i = 0; i < hubSceneNames.Length; i++)
        {
            if (hubSceneNames[i] == sceneName)
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void AddUiInputModule(GameObject eventSystemObject)
    {
        System.Type inputSystemModule = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModule != null && eventSystemObject.GetComponent(inputSystemModule) == null)
        {
            eventSystemObject.AddComponent(inputSystemModule);
            return;
        }

        if (eventSystemObject.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
        {
            eventSystemObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    private void EnsurePlayerDebugUi()
    {
        if (debugUiInitialized)
        {
            return;
        }

        Transform timeIcon = FindChildRecursive(transform, "IMG_TimeIcon");
        Transform hudCanvas = FindChildRecursive(transform, "HUD_Canvas");
        if (timeIcon == null || hudCanvas == null)
        {
            return;
        }

        debugIconButton = timeIcon.GetComponent<Button>();
        if (debugIconButton == null)
        {
            debugIconButton = timeIcon.gameObject.AddComponent<Button>();
        }

        debugIconButton.targetGraphic = timeIcon.GetComponent<Graphic>();
        debugIconButton.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = debugIconButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.86f, 0.42f, 1f);
        colors.pressedColor = new Color(0.88f, 0.66f, 0.2f, 1f);
        debugIconButton.colors = colors;
        debugIconButton.onClick.AddListener(TogglePlayerDebugPanel);

        BuildPlayerDebugPanel(hudCanvas);
        debugUiInitialized = playerDebugPanel != null;
    }

    private void BuildPlayerDebugPanel(Transform canvasTransform)
    {
        RectTransform panelRect = CreateUiObject(
            "PlayerDebugPanel",
            canvasTransform,
            new Vector2(460f, 400f),
            Vector2.zero);
        playerDebugPanel = panelRect.gameObject;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);

        Image background = playerDebugPanel.AddComponent<Image>();
        background.color = new Color(0.025f, 0.055f, 0.085f, 0.97f);

        Outline outline = playerDebugPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.92f, 0.67f, 0.2f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        CreateLabel(
            panelRect,
            "PLAYER DEBUG",
            new Vector2(0f, 132f),
            new Vector2(360f, 42f),
            26f,
            new Color(1f, 0.78f, 0.3f, 1f),
            TextAlignmentOptions.Center);

        Button closeButton = CreateButton(
            panelRect,
            "Button_CloseDebug",
            "×",
            new Vector2(202f, 137f),
            new Vector2(40f, 40f),
            new Color(0.34f, 0.12f, 0.1f, 1f));
        closeButton.onClick.AddListener(ClosePlayerDebugPanel);

        CreateLabel(
            panelRect,
            "Damage của Player",
            new Vector2(-112f, 76f),
            new Vector2(200f, 34f),
            20f,
            Color.white,
            TextAlignmentOptions.MidlineLeft);

        damageInput = CreateDamageInput(
            panelRect,
            new Vector2(43f, 76f),
            new Vector2(120f, 42f));

        Button applyDamageButton = CreateButton(
            panelRect,
            "Button_ApplyDamage",
            "SET DAMAGE",
            new Vector2(148f, 76f),
            new Vector2(120f, 42f),
            new Color(0.18f, 0.38f, 0.46f, 1f));
        applyDamageButton.onClick.AddListener(ApplyPlayerDamageValue);

        CreateLabel(
            panelRect,
            "Máu của Player",
            new Vector2(0f, 22f),
            new Vector2(390f, 34f),
            20f,
            Color.white,
            TextAlignmentOptions.Center);

        Button fullHealthButton = CreateButton(
            panelRect,
            "Button_DebugFullHealth",
            "MÁU ĐẦY",
            new Vector2(-102f, -28f),
            new Vector2(180f, 46f),
            new Color(0.12f, 0.42f, 0.24f, 1f));
        fullHealthButton.onClick.AddListener(SetPlayerFullHealth);

        Button lowHealthButton = CreateButton(
            panelRect,
            "Button_DebugLowHealth",
            "SẮP HẾT MÁU",
            new Vector2(102f, -28f),
            new Vector2(180f, 46f),
            new Color(0.52f, 0.18f, 0.12f, 1f));
        lowHealthButton.onClick.AddListener(SetPlayerLowHealth);

        CreateLabel(
            panelRect,
            "Tốc độ thời gian",
            new Vector2(-138f, -92f),
            new Vector2(190f, 26f),
            16f,
            Color.white,
            TextAlignmentOptions.MidlineLeft);

        timeSpeedSlider = CreateSlider(
            panelRect,
            "Slider_TimeSpeed",
            new Vector2(32f, -92f),
            new Vector2(216f, 24f),
            1f,
            120f,
            1f);
        timeSpeedSlider.onValueChanged.AddListener(OnTimeSpeedChanged);

        timeSpeedValueText = CreateLabel(
            panelRect,
            "1x",
            new Vector2(188f, -92f),
            new Vector2(70f, 24f),
            16f,
            new Color(1f, 0.78f, 0.3f, 1f),
            TextAlignmentOptions.Center);

        debugStatusText = CreateLabel(
            panelRect,
            "Đang tìm Player...",
            new Vector2(0f, -165f),
            new Vector2(410f, 44f),
            18f,
            new Color(0.82f, 0.9f, 0.94f, 1f),
            TextAlignmentOptions.Center);

        CreateLabel(
            panelRect,
            "Bấm lại icon mặt trời để đóng",
            new Vector2(0f, -210f),
            new Vector2(410f, 28f),
            15f,
            new Color(0.62f, 0.68f, 0.72f, 1f),
            TextAlignmentOptions.Center);

        debugPanelTween = playerDebugPanel.AddComponent<PopupTween>();
        debugPanelTween.SetHiddenImmediate();
    }

    public void ShowDeathRecoveryNotice(int droppedStackCount, float bagLifetimeSeconds)
    {
        EnsureDeathRecoveryNotice();
        if (deathRecoveryNotice == null || deathRecoveryNoticeText == null)
        {
            return;
        }

        int lifetimeMinutes = Mathf.Max(1, Mathf.RoundToInt(bagLifetimeSeconds / 60f));
        deathRecoveryNoticeText.text = droppedStackCount > 0
            ? "BẠN ĐÃ GỤC NGÃ\n" +
              "Bạn sẽ hồi sinh tại điểm đầu màn. Túi đồ đang nằm ở vị trí chết — " +
              $"hãy quay lại nhặt trong {lifetimeMinutes} phút, nếu không đồ sẽ biến mất!"
            : "BẠN ĐÃ GỤC NGÃ\n" +
              "Bạn sẽ hồi sinh tại điểm đầu màn. Túi hiện không có vật phẩm có thể rơi.";

        deathRecoveryNotice.transform.SetAsLastSibling();
        deathRecoveryNoticeTween.Show();

        if (deathRecoveryNoticeRoutine != null)
        {
            StopCoroutine(deathRecoveryNoticeRoutine);
        }
        deathRecoveryNoticeRoutine = StartCoroutine(HideDeathRecoveryNoticeRoutine());
    }

    void EnsureDeathRecoveryNotice()
    {
        if (deathRecoveryNotice != null)
        {
            return;
        }

        Transform hudCanvas = FindChildRecursive(transform, "HUD_Canvas");
        if (hudCanvas == null)
        {
            return;
        }

        RectTransform panel = CreateUiObject(
            "DeathRecoveryNotice",
            hudCanvas,
            new Vector2(820f, 154f),
            new Vector2(0f, -165f));
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        deathRecoveryNotice = panel.gameObject;

        Image background = deathRecoveryNotice.AddComponent<Image>();
        background.color = new Color(0.055f, 0.025f, 0.018f, 0.94f);
        background.raycastTarget = false;

        Outline outline = deathRecoveryNotice.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.55f, 0.16f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        deathRecoveryNoticeText = CreateLabel(
            panel,
            string.Empty,
            Vector2.zero,
            new Vector2(770f, 124f),
            23f,
            new Color(1f, 0.9f, 0.72f, 1f),
            TextAlignmentOptions.Center);
        deathRecoveryNoticeText.fontStyle = FontStyles.Bold;

        deathRecoveryNoticeTween = deathRecoveryNotice.AddComponent<PopupTween>();
        deathRecoveryNoticeTween.SetHiddenImmediate();
    }

    IEnumerator HideDeathRecoveryNoticeRoutine()
    {
        yield return new WaitForSecondsRealtime(9f);
        deathRecoveryNoticeTween?.Hide();
        deathRecoveryNoticeRoutine = null;
    }

    private void TogglePlayerDebugPanel()
    {
        if (playerDebugPanel == null)
        {
            EnsurePlayerDebugUi();
            return;
        }

        if (playerDebugPanel.activeSelf)
        {
            ClosePlayerDebugPanel();
            return;
        }

        ResolveDebugPlayer();
        if (debugPlayerCombat != null && damageInput != null)
        {
            damageInput.text =
                debugPlayerCombat.AttackDamage.ToString("0.##", CultureInfo.InvariantCulture);
        }

        playerDebugPanel.transform.SetAsLastSibling();
        RefreshDebugStatus();
        debugPanelTween.Show();
    }

    private void ClosePlayerDebugPanel()
    {
        debugPanelTween?.Hide();
    }

    private void ApplyPlayerDamageValue()
    {
        ResolveDebugPlayer();
        if (debugPlayerCombat == null || damageInput == null)
        {
            SetDebugMessage("Không tìm thấy Player.");
            return;
        }

        string raw = damageInput.text.Trim().Replace(',', '.');
        if (!float.TryParse(
                raw,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float damage))
        {
            SetDebugMessage("Damage không hợp lệ.");
            return;
        }

        damage = Mathf.Clamp(damage, 0.1f, 999999f);
        debugPlayerCombat.SetAttackDamageForDebug(damage);
        damageInput.text = damage.ToString("0.##", CultureInfo.InvariantCulture);
        RefreshDebugStatus();
    }

    private void SetPlayerFullHealth()
    {
        ResolveDebugPlayer();
        if (debugPlayerHealth == null || debugPlayerHealth.RuntimeStats == null)
        {
            SetDebugMessage("Không tìm thấy máu Player.");
            return;
        }

        debugPlayerHealth.SetCurrentHealthForDebug(
            debugPlayerHealth.RuntimeStats.maxHP);
        ReviveDebugPlayerIfNeeded();
        RefreshDebugStatus();
    }

    private void SetPlayerLowHealth()
    {
        ResolveDebugPlayer();
        if (debugPlayerHealth == null || debugPlayerHealth.RuntimeStats == null)
        {
            SetDebugMessage("Không tìm thấy máu Player.");
            return;
        }

        float lowHealth =
            Mathf.Max(1f, debugPlayerHealth.RuntimeStats.maxHP * 0.05f);
        debugPlayerHealth.SetCurrentHealthForDebug(lowHealth);
        ReviveDebugPlayerIfNeeded();
        RefreshDebugStatus();
    }

    private void OnTimeSpeedChanged(float multiplier)
    {
        if (timeSpeedValueText != null)
        {
            timeSpeedValueText.text = multiplier >= 1f
                ? $"{multiplier:0}x"
                : $"{multiplier:0.0}x";
        }

        HUDTopStatusController topStatus =
            GetComponentInChildren<HUDTopStatusController>(true);
        if (topStatus != null)
        {
            topStatus.SetTimeMultiplier(multiplier);
        }
    }

    private void ReviveDebugPlayerIfNeeded()
    {
        if (debugPlayerHealth == null)
        {
            return;
        }

        PlayerDeathController deathController =
            debugPlayerHealth.GetComponentInParent<PlayerDeathController>();
        deathController?.ReviveForDebug();
    }

    private void ResolveDebugPlayer()
    {
        if (debugPlayerHealth != null && debugPlayerCombat != null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return;
        }

        debugPlayerHealth = player.GetComponentInChildren<CharacterHealth>(true);
        debugPlayerCombat =
            player.GetComponentInChildren<PlayerCombatController>(true);
    }

    private void RefreshDebugStatus()
    {
        ResolveDebugPlayer();
        if (debugPlayerHealth == null ||
            debugPlayerHealth.RuntimeStats == null ||
            debugPlayerCombat == null)
        {
            SetDebugMessage("Không tìm thấy Player.");
            return;
        }

        CharacterRuntimeStats stats = debugPlayerHealth.RuntimeStats;
        SetDebugMessage(
            $"HP {Mathf.CeilToInt(stats.currentHP)} / " +
            $"{Mathf.CeilToInt(stats.maxHP)}    |    " +
            $"Damage {debugPlayerCombat.AttackDamage:0.##}");
    }

    private void SetDebugMessage(string message)
    {
        if (debugStatusText != null)
        {
            debugStatusText.text = message;
        }
    }

    private static TMP_InputField CreateDamageInput(
        RectTransform parent,
        Vector2 position,
        Vector2 size)
    {
        RectTransform root = CreateUiObject(
            "Input_Damage",
            parent,
            size,
            position);
        Image background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.07f, 0.11f, 0.14f, 1f);

        Outline outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.66f, 0.5f, 0.2f, 1f);
        outline.effectDistance = new Vector2(1f, -1f);

        RectTransform viewport = CreateUiObject(
            "Text Area",
            root,
            Vector2.zero,
            Vector2.zero);
        StretchRect(viewport, 10f, 10f, 4f, 4f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TMP_Text inputText = CreateLabel(
            viewport,
            string.Empty,
            Vector2.zero,
            Vector2.zero,
            20f,
            Color.white,
            TextAlignmentOptions.MidlineLeft);
        StretchRect(inputText.rectTransform, 0f, 0f, 0f, 0f);

        TMP_Text placeholder = CreateLabel(
            viewport,
            "20",
            Vector2.zero,
            Vector2.zero,
            20f,
            new Color(1f, 1f, 1f, 0.35f),
            TextAlignmentOptions.MidlineLeft);
        placeholder.fontStyle = FontStyles.Italic;
        StretchRect(placeholder.rectTransform, 0f, 0f, 0f, 0f);

        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = viewport;
        input.textComponent = inputText;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.pointSize = 20f;
        return input;
    }

    private static Button CreateButton(
        RectTransform parent,
        string objectName,
        string label,
        Vector2 position,
        Vector2 size,
        Color normalColor)
    {
        RectTransform rect = CreateUiObject(objectName, parent, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = normalColor;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        button.colors = colors;

        TMP_Text text = CreateLabel(
            rect,
            label,
            Vector2.zero,
            Vector2.zero,
            18f,
            Color.white,
            TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        StretchRect(text.rectTransform, 4f, 4f, 2f, 2f);
        return button;
    }

    private static Slider CreateSlider(
        RectTransform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        float minValue,
        float maxValue,
        float value)
    {
        RectTransform root = CreateUiObject(objectName, parent, size, position);
        Image background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.07f, 0.11f, 0.14f, 1f);

        RectTransform fillArea = CreateUiObject(
            "Fill Area",
            root,
            Vector2.zero,
            Vector2.zero);
        fillArea.anchorMin = new Vector2(0f, 0.25f);
        fillArea.anchorMax = new Vector2(1f, 0.75f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        RectTransform fill = CreateUiObject(
            "Fill",
            fillArea,
            Vector2.zero,
            Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.92f, 0.67f, 0.2f, 1f);

        RectTransform handleArea = CreateUiObject(
            "Handle Slide Area",
            root,
            Vector2.zero,
            Vector2.zero);
        StretchRect(handleArea, 0f, 0f, 0f, 0f);

        RectTransform handle = CreateUiObject(
            "Handle",
            handleArea,
            new Vector2(18f, 18f),
            Vector2.zero);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.SetValueWithoutNotify(value);
        return slider;
    }

    private static TMP_Text CreateLabel(
        RectTransform parent,
        string text,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateUiObject(
            "Text",
            parent,
            size,
            position);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateUiObject(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 position)
    {
        GameObject gameObject = new GameObject(
            objectName,
            typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static void StretchRect(
        RectTransform rect,
        float left,
        float right,
        float top,
        float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
