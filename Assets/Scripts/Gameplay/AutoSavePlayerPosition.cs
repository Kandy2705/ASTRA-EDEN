using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AutoSavePlayerPosition : MonoBehaviour
{
    public bool saveOnStart = true;
    public bool saveOnDisable = true;
    public bool saveOnApplicationQuit = true;
    public bool saveEveryFewSeconds = true;

    public float startSaveDelay = 1f;
    public float saveInterval = 20f;

    private float timer;
    private bool isQuitting = false;
    private bool suppressNextDisableSave;

    public void SuppressNextDisableSave()
    {
        suppressNextDisableSave = true;
    }

    private IEnumerator Start()
    {
        if (!saveOnStart) yield break;

        yield return new WaitForSeconds(startSaveDelay);
        SaveNow();
    }

    private void Update()
    {
        if (!saveEveryFewSeconds) return;

        timer += Time.deltaTime;

        if (timer >= saveInterval)
        {
            timer = 0f;
            SaveNow();
        }
    }

    public void SaveNow()
    {
        if (GameDataManager.Instance == null)
        {
            Debug.LogWarning("[AutoSavePlayerPosition] Không có GameDataManager.");
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        GameDataManager.Instance.SaveLastPlayerTransform(sceneName, transform);

        // Lưu HP/Stamina/Energy mỗi lần auto-save — portal/camp vẫn giữ đúng máu.
        CharacterHealth health = GetComponent<CharacterHealth>();
        if (health != null && health.RuntimeStats != null)
        {
            var s = health.RuntimeStats;
            GameDataManager.Instance.SavePlayerStats(s.currentHP, s.currentStamina, s.currentEnergy);
        }

        PlayerInventoryService inventory = GetComponent<PlayerInventoryService>();
        if (inventory != null)
        {
            inventory.SaveToGameData();
        }

        GameDataManager.Instance.FlushPlayerPrefs();

        // Debug.Log($"[AutoSavePlayerPosition] Saved scene={sceneName}, pos={transform.position}");
    }

    private void OnDisable()
    {
        if (suppressNextDisableSave)
        {
            suppressNextDisableSave = false;
            return;
        }
        if (!saveOnDisable) return;
        if (isQuitting) return;

        SaveNow();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;

        if (saveOnApplicationQuit)
        {
            SaveNow();
        }
    }
}
