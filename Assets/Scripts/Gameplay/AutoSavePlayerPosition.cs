using UnityEngine;
using UnityEngine.SceneManagement;

public class AutoSavePlayerPosition : MonoBehaviour
{
    public bool saveOnDisable = true;
    public bool saveOnApplicationQuit = true;

    private bool isQuitting = false;

    public void SaveNow()
    {
        if (GameDataManager.Instance == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        GameDataManager.Instance.SaveLastPlayerTransform(sceneName, transform);
    }

    private void OnDisable()
    {
        if (!saveOnDisable) return;
        if (isQuitting) return;

        SaveNow();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;

        if (saveOnApplicationQuit)
            SaveNow();
    }
}