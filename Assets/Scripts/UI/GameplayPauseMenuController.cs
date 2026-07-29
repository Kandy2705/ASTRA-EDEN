using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause menu dùng SettingsPanel prefab, mở/đóng bằng phím P.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-150)]
public class GameplayPauseMenuController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string[] disabledSceneNames = { "MainMenu", "Loading" };

    [Header("UI (auto-resolve nếu trống)")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private GameObject pausePanelRoot;
    [SerializeField] private GameObject settingsPanelPrefab;
    [SerializeField] private GameObject gameplayHudCanvas;

    [SerializeField] private string mainMenuButtonObjectName = "Button_Main Menu";

    [Header("Input")]
    [SerializeField] private bool allowPauseToggle = true;

    [Header("Options")]
    [SerializeField] private bool pauseTimeWhenOpen = true;
    [SerializeField] private bool saveBeforeExit = true;
    [SerializeField] private string mainMenuButtonLabel = "Main Menu";

    private bool isPauseOpen;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private PopupTween pauseTween;
    private GameObject legacyPausePanel;

    private void Awake()
    {
        ResolveReferences();
        CreateSettingsPausePanel();
        SetPauseOpen(false, force: true);
    }

    private void Start()
    {
        WireMainMenuButton();
        WirePausePanelButtons();
    }

    private void Update()
    {
        if (!allowPauseToggle || !CanUsePauseMenu())
        {
            return;
        }

        if (!TryGetPausePressed())
        {
            return;
        }

        InventoryToggleController inventory =
            FindFirstObjectByType<InventoryToggleController>(FindObjectsInactive.Include);
        if (!isPauseOpen && inventory != null && inventory.IsOpen)
        {
            return;
        }

        TogglePauseMenu();
    }

    public void TogglePauseMenu()
    {
        SetPauseOpen(!isPauseOpen);
    }

    public void OpenPauseMenu()
    {
        SetPauseOpen(true);
    }

    public void ClosePauseMenu()
    {
        SetPauseOpen(false);
    }

    public void ResumeGame()
    {
        ClosePauseMenu();
    }

    public void ReturnToMainMenu()
    {
        if (saveBeforeExit)
        {
            SavePlayerState();
        }

        Time.timeScale = 1f;
        ClosePauseMenu();

        Debug.Log("[GameplayPauseMenu] Quay về Main Menu.");
        SceneTransitionService.Load(mainMenuSceneName);
    }

    private void SetPauseOpen(bool open, bool force = false)
    {
        if (!force && isPauseOpen == open)
        {
            return;
        }

        if (open && !CanUsePauseMenu())
        {
            return;
        }

        if (open)
        {
            isPauseOpen = true;

            if (pausePanelRoot != null)
            {
                EnsureAncestorsActive(pausePanelRoot);
                pauseTween ??= GetOrCreateTween(pausePanelRoot);
                pauseTween.Show();
            }

            if (gameplayHudCanvas != null)
            {
                gameplayHudCanvas.SetActive(false);
            }

            if (pauseTimeWhenOpen)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        isPauseOpen = false;
        if (force)
        {
            pauseTween?.SetHiddenImmediate();
            if (pausePanelRoot != null && pauseTween == null)
            {
                pausePanelRoot.SetActive(false);
            }

            if (gameplayHudCanvas != null)
            {
                gameplayHudCanvas.SetActive(true);
            }

            return;
        }

        if (pausePanelRoot == null)
        {
            FinishClose();
            return;
        }

        pauseTween ??= GetOrCreateTween(pausePanelRoot);
        pauseTween.Hide(FinishClose);
    }

    private void ResolveReferences()
    {
        if (pausePanelRoot == null)
        {
            pausePanelRoot = FindChildByName(transform, "PopUp_Pause")?.gameObject;
        }

        legacyPausePanel = pausePanelRoot;

        if (gameplayHudCanvas == null)
        {
            Transform gameplayUiRoot = transform.parent;
            if (gameplayUiRoot != null)
            {
                Transform hud = gameplayUiRoot.Find("HUD_Canvas");
                if (hud != null)
                {
                    gameplayHudCanvas = hud.gameObject;
                }
            }
        }
    }

    private void WireMainMenuButton()
    {
        if (!CanUsePauseMenu())
        {
            return;
        }

        if (mainMenuButton == null)
        {
            mainMenuButton = FindMainMenuButton();
        }

        if (mainMenuButton == null)
        {
            Debug.LogWarning(
                $"[GameplayPauseMenu] Không tìm thấy '{mainMenuButtonObjectName}' trong GameplayUI_Root — kéo Button vào Main Menu Button trên Menu_Canvas.");
            return;
        }

        if (!mainMenuButton.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[GameplayPauseMenu] '{mainMenuButton.name}' đang inactive — bật object/parent để bấm được.");
        }

        if (!mainMenuButton.interactable)
        {
            mainMenuButton.interactable = true;
        }

        mainMenuButton.onClick.RemoveAllListeners();
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        Debug.Log($"[GameplayPauseMenu] Đã wire nút '{mainMenuButton.name}' -> ReturnToMainMenu.");
    }

    private Button FindMainMenuButton()
    {
        Transform searchRoot = transform.parent != null ? transform.parent : transform;

        Transform buttonTransform = FindChildByName(searchRoot, mainMenuButtonObjectName);
        if (buttonTransform == null && mainMenuButtonObjectName != "Button_MainMenu")
        {
            buttonTransform = FindChildByName(searchRoot, "Button_MainMenu");
        }

        return buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
    }

    private void WirePausePanelButtons()
    {
        if (pausePanelRoot == null)
        {
            return;
        }

        WireButtonsByLabel(pausePanelRoot.transform, new[] { "Quit", "Exit" }, ReturnToMainMenu, mainMenuButtonLabel);
        WireButtonsByLabel(pausePanelRoot.transform, new[] { "Continue", "Back", "Resume" }, ResumeGame, null);

        Transform closeTransform = FindChildByName(pausePanelRoot.transform, "Button_Close");
        Button closeButton = closeTransform != null ? closeTransform.GetComponent<Button>() : null;
        if (closeButton != null)
        {
            closeButton.onClick = new Button.ButtonClickedEvent();
            closeButton.onClick.AddListener(ResumeGame);
        }
    }

    private static void WireButtonsByLabel(Transform root, string[] labels, UnityEngine.Events.UnityAction action, string relabel)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || !MatchesLabel(text.text, labels))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(relabel))
            {
                text.text = relabel;
            }

            Button button = text.GetComponentInParent<Button>();
            if (button == null)
            {
                continue;
            }

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }
    }

    private static bool MatchesLabel(string value, string[] labels)
    {
        if (string.IsNullOrWhiteSpace(value) || labels == null)
        {
            return false;
        }

        string trimmed = value.Trim();
        for (int i = 0; i < labels.Length; i++)
        {
            if (string.Equals(trimmed, labels[i], System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void SavePlayerState()
    {
        if (GameDataManager.Instance == null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            GameDataManager.Instance.SaveLastPlayerTransform(SceneManager.GetActiveScene().name, player.transform);

            CharacterHealth health = player.GetComponent<CharacterHealth>();
            if (health != null && health.RuntimeStats != null)
            {
                var stats = health.RuntimeStats;
                GameDataManager.Instance.SavePlayerStats(stats.currentHP, stats.currentStamina, stats.currentEnergy);
            }
        }

        GameDataManager.Instance.FlushPlayerPrefs();
    }

    private bool CanUsePauseMenu()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName) || disabledSceneNames == null)
        {
            return true;
        }

        for (int i = 0; i < disabledSceneNames.Length; i++)
        {
            if (disabledSceneNames[i] == sceneName)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetPausePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.pKey.wasPressedThisFrame;
    }

    private void CreateSettingsPausePanel()
    {
        if (settingsPanelPrefab == null)
        {
            Debug.LogWarning(
                "[GameplayPauseMenu] Chưa gán SettingsPanel prefab, dùng PopUp_Pause cũ.");
            pauseTween = pausePanelRoot != null ? GetOrCreateTween(pausePanelRoot) : null;
            pauseTween?.SetHiddenImmediate();
            return;
        }

        if (legacyPausePanel != null)
        {
            legacyPausePanel.SetActive(false);
        }

        Transform pauseParent = CreatePauseCanvas();
        pausePanelRoot = Instantiate(settingsPanelPrefab, pauseParent, false);
        pausePanelRoot.name = settingsPanelPrefab.name;
        StretchToParent(pausePanelRoot.transform as RectTransform);

        if (pausePanelRoot.GetComponent<SettingsPanelController>() == null)
        {
            pausePanelRoot.AddComponent<SettingsPanelController>();
        }

        if (pausePanelRoot.GetComponent<AudioSettingsUI>() == null)
        {
            pausePanelRoot.AddComponent<AudioSettingsUI>();
        }

        pauseTween = GetOrCreateTween(pausePanelRoot);
        pauseTween.SetHiddenImmediate();
    }

    private Transform CreatePauseCanvas()
    {
        GameObject canvasObject = new(
            "PauseSettings_Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Transform parent = transform.parent;
        canvasObject.transform.SetParent(parent, false);
        canvasObject.layer = LayerMask.NameToLayer("UI");

        Canvas sourceCanvas = GetComponent<Canvas>();
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sourceCanvas != null ? sourceCanvas.sortingOrder + 1 : 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        return canvasObject.transform;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
    }

    private static PopupTween GetOrCreateTween(GameObject popup)
    {
        PopupTween tween = popup.GetComponent<PopupTween>();
        return tween != null ? tween : popup.AddComponent<PopupTween>();
    }

    private void FinishClose()
    {
        if (isPauseOpen)
        {
            return;
        }

        if (gameplayHudCanvas != null)
        {
            gameplayHudCanvas.SetActive(true);
        }

        if (pauseTimeWhenOpen)
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }

    private static void EnsureAncestorsActive(GameObject target)
    {
        Transform parent = target.transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
            {
                parent.gameObject.SetActive(true);
            }

            parent = parent.parent;
        }
    }

    private static Transform FindChildByName(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform nested = FindChildByName(parent.GetChild(i), childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
