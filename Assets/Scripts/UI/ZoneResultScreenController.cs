using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ZoneResultScreenController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private string returnSceneName = "Beacon_Camp";

    bool isOpen;

    void Awake()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    public void Show(ZoneObjectiveManager objective)
    {
        if (objective == null || root == null)
        {
            return;
        }

        isOpen = true;
        root.SetActive(true);

        if (titleText != null)
        {
            titleText.text = "Zone Cleared!";
        }

        if (summaryText != null)
        {
            summaryText.text =
                $"Enemies defeated: {objective.EnemyKills}\n" +
                $"Resources gathered: {objective.ResourceGathers}\n" +
                $"Mini-boss: {(objective.MiniBossDefeated ? "Defeated" : "—")}\n" +
                $"Bonus reward granted.";
        }

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void OnClickReturnToCamp()
    {
        Time.timeScale = 1f;
        isOpen = false;

        if (root != null)
        {
            root.SetActive(false);
        }

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SaveLastPlayerTransform(SceneManager.GetActiveScene().name, GameObject.FindGameObjectWithTag("Player")?.transform);
        }

        SceneManager.LoadScene(returnSceneName);
    }

    public void OnClickContinueExplore()
    {
        Time.timeScale = 1f;
        isOpen = false;

        if (root != null)
        {
            root.SetActive(false);
        }

        RestoreGameplayCursor();
    }

    static void RestoreGameplayCursor()
    {
        CameraController camera = Object.FindFirstObjectByType<CameraController>();
        if (camera != null)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}