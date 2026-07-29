using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    public string newGameSceneName = "Beacon_Camp";
    public string fallbackContinueSceneName = "Beacon_Camp";

    [Header("UI References")]
    public Button continueButton;
    public GameObject settingsPanel;
    public GameObject deleteConfirmPanel;

    private PopupTween settingsTween;

    private void Start()
    {
        if (settingsPanel != null)
        {
            settingsTween = GetOrCreateTween(settingsPanel);
            settingsTween.SetHiddenImmediate();
            WireSettingsCloseButton();
        }

        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);

        RefreshContinueButton();

        AudioManager manager = AudioManager.EnsureInstance();
        manager?.ApplySceneByName("MainMenu");
    }

    private void RefreshContinueButton()
    {
        if (continueButton == null) return;

        continueButton.interactable = true;
    }

    public void ContinueGame()
    {
        if (GameDataManager.Instance == null)
        {
            Debug.LogWarning("[MainMenu] Không có GameDataManager trong scene. Load game mới.");
            SceneTransitionService.Load(newGameSceneName);
            return;
        }

        // Chưa từng chơi lần nào -> bắt đầu màn mới hoàn toàn
        if (!GameDataManager.Instance.HasSave)
        {
            Debug.Log("[MainMenu] Chưa có save -> Start New Game từ Continue.");
            SceneTransitionService.Load(newGameSceneName);
            return;
        }

        // Đã từng chơi -> tiếp tục đúng scene cũ
        string sceneName = GameDataManager.Instance.GetLastSceneName(fallbackContinueSceneName);

        GameDataManager.Instance.MarkLoadFromContinue();

        Debug.Log($"[MainMenu] Có save -> Continue scene: {sceneName}");
        SceneTransitionService.Load(sceneName);
    }

    public void NewGame()
    {
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.DeleteSaveData();
        }

        SceneTransitionService.Load(newGameSceneName);
    }

    public void ShowDeleteConfirmPanel()
    {
        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(true);
        else
            DeleteSaveData();
    }

    public void CancelDeleteSave()
    {
        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);
    }

    public void DeleteSaveData()
    {
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.DeleteSaveData();
        }

        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);

        RefreshContinueButton();
    }

    public void OpenSettings()
    {
        if (settingsPanel == null)
            return;

        GetOrCreateTween(settingsPanel).Show();
    }

    public void CloseSettings()
    {
        if (settingsPanel == null)
            return;

        GetOrCreateTween(settingsPanel).Hide();
    }

    public void QuitGame()
    {
        Debug.Log("[MainMenu] Quit Game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private PopupTween GetOrCreateTween(GameObject popup)
    {
        if (settingsTween == null)
        {
            settingsTween = popup.GetComponent<PopupTween>();
            if (settingsTween == null)
            {
                settingsTween = popup.AddComponent<PopupTween>();
            }
        }

        return settingsTween;
    }

    private void WireSettingsCloseButton()
    {
        Transform closeTransform = FindChildByName(settingsPanel.transform, "Button_Close");
        Button closeButton = closeTransform != null
            ? closeTransform.GetComponent<Button>()
            : null;
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick = new Button.ButtonClickedEvent();
        closeButton.onClick.AddListener(CloseSettings);
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
