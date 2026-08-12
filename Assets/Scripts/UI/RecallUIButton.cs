using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn script này vào Button "ItemTeleportHome" để kích hoạt chức năng Recall
/// </summary>
[RequireComponent(typeof(Button))]
public class RecallUIButton : MonoBehaviour
{
    [SerializeField] private RecallPortalManager recallManager;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        ResolveRecallManager();
    }

    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        // Prefab UI có thể disable/enable hoặc tồn tại qua scene. Gỡ trước khi
        // thêm lại để listener không bị mất và cũng không bị nhân đôi.
        button.onClick.RemoveListener(OnRecallClicked);
        button.onClick.AddListener(OnRecallClicked);
        ResolveRecallManager();
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnRecallClicked);
        }
    }

    private void OnRecallClicked()
    {
        ResolveRecallManager();
        if (recallManager == null)
        {
            Debug.LogWarning(
                "[RecallUIButton] Không tìm thấy RecallPortalManager trong scene hiện tại.",
                this);
            return;
        }

        if (recallManager.OnRecallButtonPressed())
        {
            Debug.Log("[RecallUIButton] Đã mở cổng dịch chuyển về Beacon_Camp.", this);
        }
    }

    private void ResolveRecallManager()
    {
        if (recallManager == null)
        {
            recallManager = FindFirstObjectByType<RecallPortalManager>(FindObjectsInactive.Include);
        }
    }
}
