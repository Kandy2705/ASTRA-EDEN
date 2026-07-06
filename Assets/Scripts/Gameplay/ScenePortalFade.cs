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

    [Tooltip("Số giây chờ trước khi tắt movement của player")]
    [Min(0f)] public float delayBeforeStopMovement = 0.5f;

    [Tooltip("Bật nếu muốn khi load scene mới thì player được đưa về vị trí đã lưu của scene đó.")]
    public bool restoreSavedPositionOnLoad = false;

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

        if (restoreSavedPositionOnLoad && GameDataManager.Instance != null)
        {
            GameDataManager.Instance.MarkLoadFromContinue();

            if (showDebugLog)
                Debug.Log("[ScenePortalFade] Đã bật cờ Restore/Continue bằng Trigger.");
        }

        isLoading = true;
        StartCoroutine(FadeAndLoadRoutine(other.gameObject));
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

        if (restoreSavedPositionOnLoad && GameDataManager.Instance != null)
        {
            GameDataManager.Instance.MarkLoadFromContinue();

            if (showDebugLog)
                Debug.Log("[ScenePortalFade] Đã bật cờ Restore/Continue bằng Button.");
        }

        isLoading = true;
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        StartCoroutine(FadeAndLoadRoutine(player));
    }

    public void LoadSceneByButtonRestorePosition()
    {
        if (isLoading) return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"[ScenePortalFade] Chưa nhập 'targetSceneName' trên '{name}'.");
            return;
        }

        if (showDebugLog)
            Debug.Log($"[ScenePortalFade] Button được bấm -> restore position & load '{targetSceneName}'");

        SavePlayerState();

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.MarkLoadFromContinue();
            Debug.Log("[ScenePortalFade] Đã bật cờ Restore/Continue.");
        }

        isLoading = true;
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        StartCoroutine(FadeAndLoadRoutine(player));
    }

    private void SavePlayerState()
    {
        if (GameDataManager.Instance == null) return;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null) return;

        GameDataManager.Instance.SaveLastPlayerTransform(
            SceneManager.GetActiveScene().name,
            player.transform
        );

        CharacterHealth health = player.GetComponent<CharacterHealth>();
        if (health != null && health.RuntimeStats != null)
        {
            var s = health.RuntimeStats;
            GameDataManager.Instance.SavePlayerStats(s.currentHP, s.currentStamina, s.currentEnergy);
        }
    }

    private void DisablePlayerMovement(GameObject player)
    {
        if (showDebugLog)
            Debug.Log($"[ScenePortalFade] Tắt movement và animation của player...");

        PlayerInputReader inputReader = player.GetComponent<PlayerInputReader>();
        if (inputReader != null)
        {
            inputReader.enabled = false;
            if (showDebugLog)
                Debug.Log("[ScenePortalFade] Đã tắt PlayerInputReader");
        }

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.enabled = false;
            if (showDebugLog)
                Debug.Log("[ScenePortalFade] Đã tắt PlayerController");
        }

        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
            if (showDebugLog)
                Debug.Log("[ScenePortalFade] Đã tắt CharacterController");
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            if (showDebugLog)
                Debug.Log("[ScenePortalFade] Đã reset velocity");
        }

        // Tắt animation
        Animator animator = player.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
            if (showDebugLog)
                Debug.Log("[ScenePortalFade] Đã tắt Animator");
        }
    }

    private IEnumerator FadeAndLoadRoutine(GameObject player)
    {
        // Tắt animation và movement ngay lập tức
        DisablePlayerMovement(player);

        if (showDebugLog)
            Debug.Log($"[ScenePortalFade] Chuyển sang Loading screen & load '{targetSceneName}'");

        yield return null; // Chờ 1 frame để player stop animation

        SceneTransitionService.Load(targetSceneName);
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
