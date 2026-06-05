using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class ScenePortal : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Tên scene cần load. VD: World_Eden7")]
    public string targetSceneName = "World_Eden7";

    [Tooltip("Tag của Player để kích hoạt portal")]
    public string playerTag = "Player";

    [Header("Behaviour")]
    [Tooltip("Số giây chờ trước khi load scene")]
    [Min(0f)] public float delayBeforeLoad = 0f;

    [Tooltip("Bật để in log debug ra Console")]
    public bool showDebugLog = true;

    private bool isLoading = false;

    private void Reset()
    {
        // Tự động bật Is Trigger khi gắn script lần đầu
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isLoading) return;
        if (!other.CompareTag(playerTag)) return;

        if (showDebugLog)
            Debug.Log($"[ScenePortal] Player chạm portal '{name}' -> chuẩn bị load '{targetSceneName}'");

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"[ScenePortal] Chưa nhập 'targetSceneName' trên portal '{name}'. Hãy điền tên scene trong Inspector và thêm scene vào Build Settings.");
            return;
        }

        SavePlayerState();

        isLoading = true;
        StartCoroutine(LoadSceneRoutine());
    }

    private void SavePlayerState()
    {
        if (GameDataManager.Instance == null) return;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null) return;

        GameDataManager.Instance.SaveScenePosition(
            SceneManager.GetActiveScene().name,
            player.transform.position
        );

        CharacterHealth health = player.GetComponent<CharacterHealth>();
        if (health != null && health.RuntimeStats != null)
        {
            var s = health.RuntimeStats;
            GameDataManager.Instance.SavePlayerStats(s.currentHP, s.currentStamina, s.currentEnergy);
        }
    }

    private IEnumerator LoadSceneRoutine()
    {
        if (delayBeforeLoad > 0f)
        {
            if (showDebugLog)
                Debug.Log($"[ScenePortal] Chờ {delayBeforeLoad}s trước khi load...");
            yield return new WaitForSeconds(delayBeforeLoad);
        }

        if (showDebugLog)
            Debug.Log($"[ScenePortal] Đang load scene '{targetSceneName}'");

        SceneManager.LoadScene(targetSceneName);
    }
}
