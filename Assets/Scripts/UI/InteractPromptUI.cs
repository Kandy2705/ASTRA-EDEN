using TMPro;
using UnityEngine;

/// <summary>
/// Hiện prompt tương tác ("Examine the Ancient Tree [F]") giữa màn hình khi player
/// đứng gần vật tương tác. Component đặt trên một GameObject LUÔN active; chỉ
/// ẩn/hiện panel con (visual) để Update vẫn chạy kể cả khi panel đang tắt.
/// </summary>
[DisallowMultipleComponent]
public class InteractPromptUI : MonoBehaviour
{
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TMP_Text promptText;

    PlayerInteractController playerInteract;

    public void Initialize(GameObject panel, TMP_Text text)
    {
        promptPanel = panel;
        promptText = text;
    }

    void Update()
    {
        if (playerInteract == null)
        {
            CachePlayerInteract();
            if (playerInteract == null)
            {
                SetPromptVisible(false);
                return;
            }
        }

        string prompt = playerInteract.GetCurrentPrompt();
        SetPromptVisible(!string.IsNullOrEmpty(prompt));
        if (promptText != null)
        {
            promptText.text = prompt;
        }
    }

    void CachePlayerInteract()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return;
        }

        playerInteract = player.GetComponentInChildren<PlayerInteractController>(true);
    }

    void SetPromptVisible(bool visible)
    {
        if (promptPanel != null && promptPanel.activeSelf != visible)
        {
            promptPanel.SetActive(visible);
        }
    }
}
