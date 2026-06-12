using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý màn hình loading - cập nhật progress bar khi load scene.
/// Dùng static variable để nhận tên scene từ ScenePortalFade.
/// </summary>
public class LoadingScreenController : MonoBehaviour
{
    [SerializeField] private Image loadingBarFill;
    [SerializeField] private TMP_Text loadingPercentText;
    [SerializeField] private float minLoadingDuration = 1.5f; // Thời gian tối thiểu để hiển thị loading (không load xong quá nhanh)

    // Static để ScenePortalFade truyền tên scene cần load
    public static string TargetSceneName { get; set; } = "";

    private AsyncOperation asyncLoad;
    private float startTime;

    private void OnEnable()
    {
        // Chờ scene Loading setup xong rồi mới start load target scene
        StartCoroutine(WaitAndLoad());
    }

    private IEnumerator WaitAndLoad()
    {
        // Chờ 1 frame để scene xong setup
        yield return new WaitForEndOfFrame();

        if (!string.IsNullOrWhiteSpace(TargetSceneName))
        {
            StartCoroutine(LoadSceneAsync(TargetSceneName));
        }
        else
        {
            Debug.LogWarning("[LoadingScreenController] Chưa set TargetSceneName!");
        }
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        startTime = Time.time;

        asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        float displayedProgress = 0f;

        while (displayedProgress < 1f)
        {
            // Unity async progress chỉ chạy tới 0.9 trước khi activate
            float realProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            // Ép loading chạy tối thiểu theo thời gian
            float timeProgress = Mathf.Clamp01((Time.time - startTime) / minLoadingDuration);

            // Lấy progress nhỏ hơn để thanh không chạy 100% quá sớm
            float targetProgress = Mathf.Min(realProgress, timeProgress);

            // Cho thanh chạy mượt từ từ
            displayedProgress = Mathf.MoveTowards(
                displayedProgress,
                targetProgress,
                Time.deltaTime * 0.6f
            );

            UpdateProgressBar(displayedProgress);

            // Khi scene đã load xong và thời gian tối thiểu đã đủ thì cho lên 100%
            if (asyncLoad.progress >= 0.9f && timeProgress >= 1f)
            {
                break;
            }

            yield return null;
        }

        // Chạy mượt từ vị trí hiện tại lên 100%
        while (displayedProgress < 1f)
        {
            displayedProgress = Mathf.MoveTowards(
                displayedProgress,
                1f,
                Time.deltaTime * 1.2f
            );

            UpdateProgressBar(displayedProgress);
            yield return null;
        }

        yield return new WaitForSeconds(0.25f);

        asyncLoad.allowSceneActivation = true;

        TargetSceneName = "";
    }
    private void UpdateProgressBar(float progress)
    {
        float displayProgress = Mathf.Clamp01(progress);

        if (loadingBarFill != null)
        {
            loadingBarFill.fillAmount = displayProgress;
        }

        if (loadingPercentText != null)
        {
            loadingPercentText.text = $"Loading {Mathf.RoundToInt(displayProgress * 100)}%";
        }
    }
}
