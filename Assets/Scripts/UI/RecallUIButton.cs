using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn script này vào Button "ItemTeleportHome" để kích hoạt chức năng Recall
/// </summary>
public class RecallUIButton : MonoBehaviour
{
    [SerializeField] private RecallPortalManager recallManager;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("RecallUIButton must be attached to a UI Button!");
            return;
        }

        if (recallManager == null)
        {
            recallManager = FindObjectOfType<RecallPortalManager>();
        }

        button.onClick.AddListener(OnRecallClicked);
    }

    private void OnRecallClicked()
    {
        if (recallManager != null)
        {
            recallManager.OnRecallButtonPressed();
            Debug.Log("[RecallUIButton] Recall portal opened!");
        }
        else
        {
            Debug.LogWarning("[RecallUIButton] RecallPortalManager not found!");
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnRecallClicked);
        }
    }
}
