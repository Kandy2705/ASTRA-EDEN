using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneTransitionService
{
    public const string LoadingSceneName = "Loading";

    public static void Load(
        string targetSceneName,
        bool useLoadingScreen = true,
        bool suppressLoadingAudio = false)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[SceneTransition] targetSceneName trống.");
            return;
        }

        if (useLoadingScreen && IsSceneInBuildSettings(LoadingSceneName))
        {
            AudioManager manager = AudioManager.EnsureInstance();
            if (suppressLoadingAudio)
            {
                manager?.NotifyTransitionToLoadingSilently(targetSceneName);
            }
            else
            {
                manager?.NotifyTransitionToLoading(targetSceneName);
            }

            LoadingScreenController.TargetSceneName = targetSceneName;
            SceneManager.LoadScene(LoadingSceneName);
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }

    public static bool IsSceneInBuildSettings(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
            {
                return true;
            }
        }

        return false;
    }
}
