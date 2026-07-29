using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    [Header("Tab Buttons")]
    public Button audioButton;
    public Button controlsButton;
    public Button lightingButton;

    [Header("Tab Content")]
    public GameObject audioContent;
    public GameObject controlsContent;
    public GameObject lightingContent;

    [Header("Button Colors")]
    public Color activeButtonColor = new Color(1f, 0.84f, 0f); // Gold/Yellow
    public Color inactiveButtonColor = new Color(0.3f, 0.7f, 0.6f); // Teal/Green

    private static readonly GameControlAction[] ControlOrder =
    {
        GameControlAction.MoveForward,
        GameControlAction.MoveBackward,
        GameControlAction.MoveLeft,
        GameControlAction.MoveRight,
        GameControlAction.Jump,
        GameControlAction.Dash,
        GameControlAction.Run,
        GameControlAction.Attack,
        GameControlAction.Skill1,
        GameControlAction.Skill2,
        GameControlAction.Skill3,
        GameControlAction.Interact,
        GameControlAction.CompanionCommand,
        GameControlAction.CompanionSkill
    };

    private static readonly string[] ControlLabels =
    {
        "MOVE FORWARD",
        "MOVE BACKWARD",
        "MOVE LEFT",
        "MOVE RIGHT",
        "JUMP",
        "DASH",
        "RUN",
        "ATTACK",
        "SKILL 1",
        "SKILL 2",
        "SKILL 3",
        "INTERACT",
        "COMPANION COMMAND",
        "COMPANION SKILL"
    };

    private static readonly Key[] SelectableKeys =
    {
        Key.W, Key.A, Key.S, Key.D,
        Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow,
        Key.Space,
        Key.LeftShift, Key.RightShift,
        Key.LeftCtrl, Key.RightCtrl,
        Key.Q, Key.E, Key.R, Key.F, Key.T, Key.G,
        Key.J, Key.K, Key.L,
        Key.B, Key.C, Key.V, Key.X, Key.Z,
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5
    };

    private Button applyButton;
    private Button resetButton;
    private Slider brightnessSlider;
    private TMP_Text brightnessValueText;
    private TMP_Dropdown bloomDropdown;
    private TMP_Dropdown[] controlDropdowns;
    private AudioSettingsUI audioSettingsUI;
    private CanvasGroup notificationGroup;
    private RectTransform notificationRect;
    private Sequence notificationTween;

    private void Start()
    {
        GameSettingsManager.EnsureInstance();
        ResolvePanelReferences();
        ResolveSettingsControls();
        BindSettingsControls();

        if (audioButton != null)
            audioButton.onClick.AddListener(SelectAudioTab);

        if (controlsButton != null)
            controlsButton.onClick.AddListener(SelectControlsTab);

        if (lightingButton != null)
            lightingButton.onClick.AddListener(SelectLightingTab);

        // Show AUDIO tab by default
        if (audioButton != null && audioContent != null)
            SelectTab(audioButton, audioContent);
    }

    private void ResolvePanelReferences()
    {
        audioButton ??= FindChildByName(transform, "Btn_Audio")?.GetComponent<Button>();
        controlsButton ??= FindChildByName(transform, "Btn_Controls")?.GetComponent<Button>();
        lightingButton ??= FindChildByName(transform, "Btn_Lightings")?.GetComponent<Button>();

        audioContent ??= FindChildByName(transform, "AudioContent")?.gameObject;
        controlsContent ??= FindChildByName(transform, "ControlsContent")?.gameObject;
        lightingContent ??= FindChildByName(transform, "LightingContent")?.gameObject;
    }

    private void SelectAudioTab()
    {
        SelectTab(audioButton, audioContent);
    }

    private void SelectControlsTab()
    {
        SelectTab(controlsButton, controlsContent);
    }

    private void SelectLightingTab()
    {
        SelectTab(lightingButton, lightingContent);
    }

    private void SelectTab(Button button, GameObject content)
    {
        // Deactivate all content
        if (audioContent != null)
            audioContent.SetActive(false);
        if (controlsContent != null)
            controlsContent.SetActive(false);
        if (lightingContent != null)
            lightingContent.SetActive(false);

        // Reset all button colors
        if (audioButton != null)
            SetButtonImageColor(audioButton, false);
        if (controlsButton != null)
            SetButtonImageColor(controlsButton, false);
        if (lightingButton != null)
            SetButtonImageColor(lightingButton, false);

        // Activate selected content and highlight button
        if (content != null)
            content.SetActive(true);
        SetButtonImageColor(button, true);

        // Set button as selected for proper highlight
        if (EventSystem.current != null && button != null)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }

    private void SetButtonImageColor(Button button, bool isActive)
    {
        if (button == null) return;

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = isActive ? activeButtonColor : inactiveButtonColor;
        }
    }

    private void OnDestroy()
    {
        if (audioButton != null)
            audioButton.onClick.RemoveListener(SelectAudioTab);
        if (controlsButton != null)
            controlsButton.onClick.RemoveListener(SelectControlsTab);
        if (lightingButton != null)
            lightingButton.onClick.RemoveListener(SelectLightingTab);
        if (applyButton != null)
            applyButton.onClick.RemoveListener(ApplySettings);
        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetSettings);
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.RemoveListener(PreviewBrightness);
        if (bloomDropdown != null)
            bloomDropdown.onValueChanged.RemoveListener(PreviewBloom);
    }

    private void ResolveSettingsControls()
    {
        Transform searchRoot = audioContent != null && audioContent.transform.parent != null
            ? audioContent.transform.parent
            : transform;

        applyButton = FindChildByName(searchRoot, "Btn_Apply")?.GetComponent<Button>();
        resetButton = FindChildByName(searchRoot, "Btn_Reset")?.GetComponent<Button>();

        if (lightingContent != null)
        {
            Transform brightnessRow = FindChildByName(lightingContent.transform, "BrightnessLight");
            brightnessSlider =
                brightnessRow != null ? brightnessRow.GetComponentInChildren<Slider>(true) : null;
            brightnessValueText = FindTextByName(brightnessRow, "ValueText");

            Transform bloomRow = FindChildByName(lightingContent.transform, "BloomLight");
            bloomDropdown =
                bloomRow != null ? bloomRow.GetComponentInChildren<TMP_Dropdown>(true) : null;
        }

        controlDropdowns = controlsContent != null
            ? controlsContent.GetComponentsInChildren<TMP_Dropdown>(true)
            : System.Array.Empty<TMP_Dropdown>();

        audioSettingsUI = GetComponent<AudioSettingsUI>();
    }

    private void BindSettingsControls()
    {
        if (applyButton != null)
        {
            applyButton.onClick.AddListener(ApplySettings);
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetSettings);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.SetValueWithoutNotify(GameSettingsManager.Brightness);
            brightnessSlider.onValueChanged.AddListener(PreviewBrightness);
            UpdateBrightnessText(GameSettingsManager.Brightness);
        }

        if (bloomDropdown != null)
        {
            bloomDropdown.SetValueWithoutNotify(GameSettingsManager.BloomEnabled ? 0 : 1);
            bloomDropdown.onValueChanged.AddListener(PreviewBloom);
            bloomDropdown.RefreshShownValue();
        }

        int count = Mathf.Min(controlDropdowns.Length, ControlOrder.Length);
        List<string> keyOptions = BuildKeyOptions();

        for (int i = 0; i < count; i++)
        {
            TMP_Dropdown dropdown = controlDropdowns[i];
            CopyDropdownStyle(bloomDropdown, dropdown);
            dropdown.ClearOptions();
            dropdown.AddOptions(keyOptions);

            Key current = GameSettingsManager.GetBinding(ControlOrder[i]);
            dropdown.SetValueWithoutNotify(FindKeyIndex(current));
            dropdown.RefreshShownValue();

            TMP_Text actionLabel = FindTextByName(dropdown.transform.parent, "tmp_action");
            if (actionLabel != null)
            {
                actionLabel.text = ControlLabels[i];
            }
        }
    }

    private static void CopyDropdownStyle(TMP_Dropdown source, TMP_Dropdown target)
    {
        if (source == null || target == null || source == target)
        {
            return;
        }

        target.transition = source.transition;
        target.colors = source.colors;
        target.spriteState = source.spriteState;
        target.animationTriggers = source.animationTriggers;
        target.alphaFadeSpeed = source.alphaFadeSpeed;

        CopyImageVisual(source.GetComponent<Image>(), target.GetComponent<Image>());
        CopyTextVisual(source.captionText, target.captionText);

        Image sourceArrow = FindImageByName(source.transform, "Arrow");
        Image targetArrow = FindImageByName(target.transform, "Arrow");
        CopyImageVisual(sourceArrow, targetArrow);

        CloneDropdownTemplate(source, target);
    }

    private static void CloneDropdownTemplate(TMP_Dropdown source, TMP_Dropdown target)
    {
        if (source.template == null)
        {
            return;
        }

        RectTransform oldTemplate = target.template;
        GameObject cloneObject = Instantiate(
            source.template.gameObject,
            target.transform,
            false);
        cloneObject.name = "Template";
        cloneObject.SetActive(false);

        RectTransform cloneTemplate = cloneObject.GetComponent<RectTransform>();
        Image cloneBackground = cloneObject.GetComponent<Image>();
        if (cloneBackground != null)
        {
            Color opaqueColor = cloneBackground.color;
            opaqueColor.a = 1f;
            cloneBackground.color = opaqueColor;
        }

        ApplyDropdownItemLayout(cloneTemplate, 8f, 12f);

        target.template = cloneTemplate;
        target.itemText = FindEquivalentComponent(
            source.template,
            source.itemText,
            cloneTemplate);
        target.itemImage = FindEquivalentComponent(
            source.template,
            source.itemImage,
            cloneTemplate);

        if (oldTemplate != null && oldTemplate != source.template)
        {
            Destroy(oldTemplate.gameObject);
        }
    }

    private static void ApplyDropdownItemLayout(
        RectTransform template,
        float leftPadding,
        float rightPadding)
    {
        ScrollRect scrollRect = template != null ? template.GetComponent<ScrollRect>() : null;
        RectTransform content = scrollRect != null ? scrollRect.content : null;
        if (content != null)
        {
            content.anchorMin = new Vector2(0f, content.anchorMin.y);
            content.anchorMax = new Vector2(1f, content.anchorMax.y);
            content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
        }

        Toggle itemToggle = template != null
            ? template.GetComponentInChildren<Toggle>(true)
            : null;
        RectTransform item = itemToggle != null
            ? itemToggle.transform as RectTransform
            : null;
        if (item == null)
        {
            return;
        }

        item.anchorMin = new Vector2(0f, item.anchorMin.y);
        item.anchorMax = new Vector2(1f, item.anchorMax.y);
        item.anchoredPosition = new Vector2(
            (leftPadding - rightPadding) * 0.5f,
            item.anchoredPosition.y);
        item.sizeDelta = new Vector2(
            -(leftPadding + rightPadding),
            item.sizeDelta.y);
    }

    private static T FindEquivalentComponent<T>(
        Transform sourceRoot,
        T sourceComponent,
        Transform cloneRoot) where T : Component
    {
        if (sourceRoot == null || sourceComponent == null || cloneRoot == null)
        {
            return null;
        }

        string path = GetRelativePath(sourceRoot, sourceComponent.transform);
        Transform equivalent = string.IsNullOrEmpty(path)
            ? cloneRoot
            : cloneRoot.Find(path);
        return equivalent != null ? equivalent.GetComponent<T>() : null;
    }

    private static string GetRelativePath(Transform root, Transform child)
    {
        if (root == child)
        {
            return string.Empty;
        }

        List<string> parts = new();
        Transform current = child;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        if (current != root)
        {
            return string.Empty;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void CopyImageVisual(Image source, Image target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.material = source.material;
        target.sprite = source.sprite;
        target.color = source.color;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
    }

    private static void CopyTextVisual(TMP_Text source, TMP_Text target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
        target.color = source.color;
        target.fontSize = source.fontSize;
        target.fontStyle = source.fontStyle;
        target.fontWeight = source.fontWeight;
        target.alignment = source.alignment;
        target.enableAutoSizing = source.enableAutoSizing;
        target.fontSizeMin = source.fontSizeMin;
        target.fontSizeMax = source.fontSizeMax;
        target.margin = source.margin;
    }

    private static Image FindImageByName(Transform root, string objectName)
    {
        Transform child = FindChildByName(root, objectName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private void PreviewBrightness(float value)
    {
        UpdateBrightnessText(value);

        // Preview immediately. Apply persists the final value.
        GameSettingsManager.ApplyGraphics(
            value,
            bloomDropdown == null || bloomDropdown.value == 0,
            save: false);
    }

    private void PreviewBloom(int value)
    {
        float brightness =
            brightnessSlider != null ? brightnessSlider.value : GameSettingsManager.Brightness;
        GameSettingsManager.ApplyGraphics(brightness, value == 0, save: false);
    }

    public void ApplySettings()
    {
        float brightness =
            brightnessSlider != null ? brightnessSlider.value : GameSettingsManager.Brightness;
        bool bloomEnabled =
            bloomDropdown == null ? GameSettingsManager.BloomEnabled : bloomDropdown.value == 0;

        GameSettingsManager.ApplyGraphics(brightness, bloomEnabled, save: false);

        int count = Mathf.Min(controlDropdowns.Length, ControlOrder.Length);
        for (int i = 0; i < count; i++)
        {
            int keyIndex = Mathf.Clamp(controlDropdowns[i].value, 0, SelectableKeys.Length - 1);
            GameSettingsManager.SetBinding(ControlOrder[i], SelectableKeys[keyIndex], save: false);
        }

        PlayerPrefs.Save();
        ShowNotification("SETTINGS APPLIED SUCCESSFULLY");
        Debug.Log("[Settings] Đã áp dụng và lưu Audio, Controls, Brightness, Bloom.");
    }

    public void ResetSettings()
    {
        GameSettingsManager.ResetGraphicsAndBindings();
        audioSettingsUI?.ResetToDefaults();

        if (brightnessSlider != null)
        {
            brightnessSlider.SetValueWithoutNotify(GameSettingsManager.DefaultBrightness);
            UpdateBrightnessText(GameSettingsManager.DefaultBrightness);
        }

        if (bloomDropdown != null)
        {
            bloomDropdown.SetValueWithoutNotify(
                GameSettingsManager.DefaultBloomEnabled ? 0 : 1);
            bloomDropdown.RefreshShownValue();
        }

        int count = Mathf.Min(controlDropdowns.Length, ControlOrder.Length);
        for (int i = 0; i < count; i++)
        {
            Key defaultKey = GameSettingsManager.GetDefaultBinding(ControlOrder[i]);
            controlDropdowns[i].SetValueWithoutNotify(FindKeyIndex(defaultKey));
            controlDropdowns[i].RefreshShownValue();
        }

        ShowNotification("DEFAULT SETTINGS RESTORED");
        Debug.Log("[Settings] Đã khôi phục cài đặt mặc định.");
    }

    private void ShowNotification(string message)
    {
        EnsureNotification();
        if (notificationGroup == null || notificationRect == null)
        {
            return;
        }

        TMP_Text messageText =
            notificationGroup.GetComponentInChildren<TMP_Text>(true);
        if (messageText != null)
        {
            messageText.text = message;
        }

        if (notificationTween.isAlive)
        {
            notificationTween.Stop();
        }

        notificationGroup.gameObject.SetActive(true);
        notificationGroup.alpha = 0f;
        notificationGroup.blocksRaycasts = false;
        notificationGroup.interactable = false;
        notificationRect.localScale = new Vector3(0.82f, 0.82f, 1f);

        notificationTween = Sequence.Create(useUnscaledTime: true)
            .Group(Tween.Alpha(
                notificationGroup,
                0f,
                1f,
                0.2f,
                Ease.OutCubic))
            .Group(Tween.Scale(
                notificationRect,
                notificationRect.localScale,
                Vector3.one,
                0.24f,
                Ease.OutBack))
            .ChainDelay(1.35f)
            .Chain(Tween.Alpha(
                notificationGroup,
                1f,
                0f,
                0.25f,
                Ease.InCubic))
            .Group(Tween.Scale(
                notificationRect,
                Vector3.one,
                new Vector3(0.94f, 0.94f, 1f),
                0.25f,
                Ease.InCubic))
            .OnComplete(() => notificationGroup.gameObject.SetActive(false));
    }

    private void EnsureNotification()
    {
        if (notificationGroup != null)
        {
            return;
        }

        Transform panelRoot =
            audioContent != null && audioContent.transform.parent != null
                ? audioContent.transform.parent
                : transform;

        GameObject toast = new(
            "SettingsApplyNotification",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(Outline));
        toast.transform.SetParent(panelRoot, false);
        toast.transform.SetAsLastSibling();

        notificationRect = toast.GetComponent<RectTransform>();
        notificationRect.anchorMin = new Vector2(0.5f, 1f);
        notificationRect.anchorMax = new Vector2(0.5f, 1f);
        notificationRect.pivot = new Vector2(0.5f, 1f);
        notificationRect.anchoredPosition = new Vector2(0f, -92f);
        notificationRect.sizeDelta = new Vector2(540f, 66f);

        Image background = toast.GetComponent<Image>();
        background.color = new Color(0.035f, 0.16f, 0.12f, 0.98f);
        background.raycastTarget = false;

        Outline outline = toast.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 0.84f, 0.42f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject labelObject = new(
            "Message",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(toast.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 8f);
        labelRect.offsetMax = new Vector2(-18f, -8f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_Text applyLabel =
            applyButton != null
                ? applyButton.GetComponentInChildren<TMP_Text>(true)
                : null;
        if (applyLabel != null)
        {
            label.font = applyLabel.font;
            label.fontSharedMaterial = applyLabel.fontSharedMaterial;
        }

        label.text = "SETTINGS APPLIED SUCCESSFULLY";
        label.color = new Color(1f, 0.9f, 0.66f, 1f);
        label.fontSize = 24f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        notificationGroup = toast.GetComponent<CanvasGroup>();
        notificationGroup.alpha = 0f;
        toast.SetActive(false);
    }

    private static List<string> BuildKeyOptions()
    {
        List<string> options = new(SelectableKeys.Length);
        for (int i = 0; i < SelectableKeys.Length; i++)
        {
            options.Add(GetKeyDisplayName(SelectableKeys[i]));
        }

        return options;
    }

    private static int FindKeyIndex(Key key)
    {
        for (int i = 0; i < SelectableKeys.Length; i++)
        {
            if (SelectableKeys[i] == key)
            {
                return i;
            }
        }

        return 0;
    }

    private static string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            Key.LeftShift => "Left Shift",
            Key.RightShift => "Right Shift",
            Key.LeftCtrl => "Left Ctrl",
            Key.RightCtrl => "Right Ctrl",
            Key.UpArrow => "Up Arrow",
            Key.DownArrow => "Down Arrow",
            Key.LeftArrow => "Left Arrow",
            Key.RightArrow => "Right Arrow",
            Key.Digit1 => "1",
            Key.Digit2 => "2",
            Key.Digit3 => "3",
            Key.Digit4 => "4",
            Key.Digit5 => "5",
            _ => key.ToString()
        };
    }

    private void UpdateBrightnessText(float value)
    {
        if (brightnessValueText != null)
        {
            brightnessValueText.text =
                $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }
    }

    private static TMP_Text FindTextByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == objectName)
            {
                return texts[i];
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
            {
                return children[i];
            }
        }

        return null;
    }
}
