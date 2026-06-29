using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gắn trên root prefab GameplayUI_Root. Ẩn panel combat-only ở hub và đảm bảo EventSystem.
/// </summary>
[DisallowMultipleComponent]
public class GameplayUISceneBootstrap : MonoBehaviour
{
    [SerializeField] private string[] hubSceneNames = { "Beacon_Camp", "MainMenu" };

    [Header("Combat-only panels (ẩn ở hub)")]
    [SerializeField] private string[] combatOnlyPanelNames =
    {
        "BossHUDPanel",
        "ZoneResultPanel"
    };

    private void Awake()
    {
        EnsureEventSystem();
        ApplyHubVisibility();
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        AddUiInputModule(eventSystem);
    }

    private void ApplyHubVisibility()
    {
        if (!IsHubScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        for (int i = 0; i < combatOnlyPanelNames.Length; i++)
        {
            string panelName = combatOnlyPanelNames[i];
            if (string.IsNullOrEmpty(panelName))
            {
                continue;
            }

            Transform panel = transform.Find(panelName);
            if (panel == null)
            {
                panel = FindChildRecursive(transform, panelName);
            }

            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }
        }
    }

    private bool IsHubScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || hubSceneNames == null)
        {
            return false;
        }

        for (int i = 0; i < hubSceneNames.Length; i++)
        {
            if (hubSceneNames[i] == sceneName)
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void AddUiInputModule(GameObject eventSystemObject)
    {
        System.Type inputSystemModule = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModule != null && eventSystemObject.GetComponent(inputSystemModule) == null)
        {
            eventSystemObject.AddComponent(inputSystemModule);
            return;
        }

        if (eventSystemObject.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
        {
            eventSystemObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }
}