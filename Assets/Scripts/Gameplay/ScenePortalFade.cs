using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phiên bản nâng cấp của ScenePortal: có fade đen màn hình trước khi load scene.
/// Cần một Canvas + Image đen full màn hình gán vào fadeImage.
/// </summary>
// [RequireComponent(typeof(Collider))]
public class ScenePortalFade : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Tên scene cần load. VD: World_Eden7")]
    public string targetSceneName = "World_Eden7";

    [Tooltip("Tag của Player để kích hoạt portal")]
    public string playerTag = "Player";

    [Header("Behaviour")]
    [Tooltip("Số giây chờ thêm sau khi fade xong rồi mới load")]
    [Min(0f)] public float delayBeforeLoad = 0f;

    [Tooltip("Thời gian fade từ trong suốt -> đen")]
    [Min(0f)] public float fadeDuration = 1.0f;

    [Tooltip("Bật để in log debug ra Console")]
    public bool showDebugLog = true;

    [Header("Fade UI")]
    [Tooltip("Image đen full màn hình dùng để fade. Để trống script sẽ tự tạo.")]
    public Image fadeImage;

    private bool isLoading = false;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        if (fadeImage == null)
            fadeImage = CreateRuntimeFadeImage();

        if (fadeImage != null)
        {
            var c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
            fadeImage.raycastTarget = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isLoading) return;
        if (!other.CompareTag(playerTag)) return;

        if (showDebugLog)
            Debug.Log($"[ScenePortalFade] Player chạm portal '{name}' -> chuẩn bị fade & load '{targetSceneName}'");

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"[ScenePortalFade] Chưa nhập 'targetSceneName' trên portal '{name}'. Điền tên scene trong Inspector và thêm scene vào Build Settings.");
            return;
        }

        SavePlayerState();

        isLoading = true;
        StartCoroutine(FadeAndLoadRoutine());
    }

    public void LoadSceneByButton()
    {
        if (isLoading) return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"[ScenePortalFade] Chưa nhập 'targetSceneName' trên '{name}'.");
            return;
        }

        if (showDebugLog)
            Debug.Log($"[ScenePortalFade] Button được bấm -> fade & load '{targetSceneName}'");

        SavePlayerState();

        isLoading = true;
        StartCoroutine(FadeAndLoadRoutine());
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

    private IEnumerator FadeAndLoadRoutine()
    {
        // Fade đen
        if (fadeImage != null && fadeDuration > 0f)
        {
            float t = 0f;
            Color c = fadeImage.color;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                c.a = Mathf.Clamp01(t / fadeDuration);
                fadeImage.color = c;
                yield return null;
            }
            c.a = 1f;
            fadeImage.color = c;
        }

        if (delayBeforeLoad > 0f)
            yield return new WaitForSeconds(delayBeforeLoad);

        if (showDebugLog)
            Debug.Log($"[ScenePortalFade] Đang load scene '{targetSceneName}'");

        SceneManager.LoadScene(targetSceneName);
    }

    /// <summary>
    /// Tự tạo 1 Canvas + Image đen full màn hình nếu chưa có.
    /// </summary>
    private Image CreateRuntimeFadeImage()
    {
        var canvasGO = new GameObject("ScenePortal_FadeCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var imgGO = new GameObject("FadeImage");
        imgGO.transform.SetParent(canvasGO.transform, false);
        var img = imgGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return img;
    }
}
