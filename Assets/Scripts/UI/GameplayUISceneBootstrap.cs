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
        WirePlayerStatusHud();
    }

    private void Start()
    {
        // Player có thể spawn sau UI 1 frame (hub / portal).
        WirePlayerStatusHud();
    }

    /// <summary>
    /// HUD_PlayerStatusPanel / CharacterStatsHUD không serialize CharacterHealth của player
    /// (khác scene / prefab). Bind runtime theo tag Player — cần cho Beacon_Camp.
    /// </summary>
    void WirePlayerStatusHud()
    {
        CharacterStatsHUD[] statusHuds = GetComponentsInChildren<CharacterStatsHUD>(true);
        for (int i = 0; i < statusHuds.Length; i++)
        {
            if (statusHuds[i] != null)
            {
                statusHuds[i].TryBindPlayerHealth(force: true);
                statusHuds[i].Refresh();
            }
        }

        // Gold HUD re-find inventory khi vào camp.
        HUDTopStatusController[] topStatus = GetComponentsInChildren<HUDTopStatusController>(true);
        for (int i = 0; i < topStatus.Length; i++)
        {
            if (topStatus[i] != null)
            {
                topStatus[i].ForceRefreshCurrency();
            }
        }
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