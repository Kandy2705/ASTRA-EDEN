using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    [Header("Tab Buttons")]
    public Button audioButton;
    public Button controlsButton;
    public Button lightingButton;

    [Header("Tab Content")]
    public GameObject audioContent;
    public GameObject controlsContent;
    public GameObject lightingContent;

    [Header("Button Colors")]
    public Color activeButtonColor = new Color(1f, 0.84f, 0f); // Gold/Yellow
    public Color inactiveButtonColor = new Color(0.3f, 0.7f, 0.6f); // Teal/Green

    private Button activeButton;

    private void Start()
    {
        if (audioButton != null)
            audioButton.onClick.AddListener(() => SelectTab(audioButton, audioContent));

        if (controlsButton != null)
            controlsButton.onClick.AddListener(() => SelectTab(controlsButton, controlsContent));

        if (lightingButton != null)
            lightingButton.onClick.AddListener(() => SelectTab(lightingButton, lightingContent));

        // Show AUDIO tab by default
        if (audioButton != null && audioContent != null)
            SelectTab(audioButton, audioContent);
    }

    private void SelectTab(Button button, GameObject content)
    {
        // Deactivate all content
        if (audioContent != null)
            audioContent.SetActive(false);
        if (controlsContent != null)
            controlsContent.SetActive(false);
        if (lightingContent != null)
            lightingContent.SetActive(false);

        // Reset all button colors
        if (audioButton != null)
            SetButtonImageColor(audioButton, false);
        if (controlsButton != null)
            SetButtonImageColor(controlsButton, false);
        if (lightingButton != null)
            SetButtonImageColor(lightingButton, false);

        // Activate selected content and highlight button
        if (content != null)
            content.SetActive(true);
        SetButtonImageColor(button, true);

        // Set button as selected for proper highlight
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);

        activeButton = button;
    }

    private void SetButtonImageColor(Button button, bool isActive)
    {
        if (button == null) return;

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = isActive ? activeButtonColor : inactiveButtonColor;
        }
    }

    private void OnDestroy()
    {
        if (audioButton != null)
            audioButton.onClick.RemoveAllListeners();
        if (controlsButton != null)
            controlsButton.onClick.RemoveAllListeners();
        if (lightingButton != null)
            lightingButton.onClick.RemoveAllListeners();
    }
}

