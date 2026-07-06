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

    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

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
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
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
}