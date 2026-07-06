using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause menu + nút quay Main Menu trên Menu_Canvas (dùng PopUp_Pause và Sub_Menu có sẵn).
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
    [SerializeField] private GameObject gameplayHudCanvas;

    [SerializeField] private string mainMenuButtonObjectName = "Button_Main Menu";

    [Header("Input")]
    [SerializeField] private bool allowEscapeToggle = true;

    [Header("Options")]
    [SerializeField] private bool pauseTimeWhenOpen = true;
    [SerializeField] private bool saveBeforeExit = true;
    [SerializeField] private string mainMenuButtonLabel = "Main Menu";

    private bool isPauseOpen;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        ResolveReferences();
        SetPauseOpen(false, force: true);
    }

    private void Start()
    {
        WireMainMenuButton();
        WirePausePanelButtons();
    }

    private void Update()
    {
        if (!allowEscapeToggle || !CanUsePauseMenu())
        {
            return;
        }

        if (!TryGetEscapePressed())
        {
            return;
        }

        InventoryToggleController inventory = FindFirstObjectByType<InventoryToggleController>(FindObjectsInactive.Include);
        if (inventory != null && inventory.IsOpen)
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

        isPauseOpen = open;

        if (pausePanelRoot != null)
        {
            if (isPauseOpen)
            {
                EnsureAncestorsActive(pausePanelRoot);
            }

            pausePanelRoot.SetActive(isPauseOpen);
        }

        if (gameplayHudCanvas != null && isPauseOpen)
        {
            gameplayHudCanvas.SetActive(false);
        }
        else if (gameplayHudCanvas != null && !isPauseOpen)
        {
            gameplayHudCanvas.SetActive(true);
        }

        if (pauseTimeWhenOpen)
        {
            if (isPauseOpen)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            }
        }

        if (isPauseOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void ResolveReferences()
    {
        if (pausePanelRoot == null)
        {
            pausePanelRoot = FindChildByName(transform, "PopUp_Pause")?.gameObject;
        }

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

            button.onClick.RemoveAllListeners();
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

    private static bool TryGetEscapePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
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