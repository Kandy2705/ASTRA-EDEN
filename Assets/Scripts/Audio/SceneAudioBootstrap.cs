using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public class SceneAudioBootstrap : MonoBehaviour
{
    [SerializeField] private SceneAudioCatalog catalogOverride;
    [SerializeField] private SceneAudioProfile profileOverride;
    [SerializeField] private bool applyOnStart = true;

    void Awake()
    {
        AudioManager manager = AudioManager.EnsureInstance();
        if (manager == null)
        {
            return;
        }

        if (catalogOverride != null)
        {
            manager.AssignCatalog(catalogOverride);
        }
    }

    void Start()
    {
        if (!applyOnStart)
        {
            return;
        }

        AudioManager manager = AudioManager.Instance;
        if (manager == null)
        {
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == SceneTransitionService.LoadingSceneName)
        {
            return;
        }

        if (profileOverride != null)
        {
            manager.ApplyProfile(profileOverride, force: true);
            return;
        }

        manager.ApplySceneByName(sceneName, force: true);
    }
}