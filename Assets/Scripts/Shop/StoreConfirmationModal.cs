using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StoreConfirmationModal : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action confirmAction;
    private bool resolving;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
    }

    public void Show(string prompt, Action onConfirmed)
    {
        resolving = false;
        confirmAction = onConfirmed;
        if (message != null) message.text = prompt;
        if (panel != null)
        {
            PopupTween tween = panel.GetComponent<PopupTween>();
            if (tween != null) tween.Show();
            else panel.SetActive(true);
        }
    }

    public void Cancel()
    {
        if (resolving) return;
        confirmAction = null;
        Hide();
    }

    private void Confirm()
    {
        if (resolving || confirmAction == null) return;
        resolving = true;
        Action action = confirmAction;
        confirmAction = null;
        Hide();
        action.Invoke();
        resolving = false;
    }

    private void Hide()
    {
        if (panel == null) return;
        PopupTween tween = panel.GetComponent<PopupTween>();
        if (tween != null) tween.Hide();
        else panel.SetActive(false);
    }

    private void HideImmediate()
    {
        if (panel == null) return;
        PopupTween tween = panel.GetComponent<PopupTween>();
        if (tween != null) tween.SetHiddenImmediate();
        else panel.SetActive(false);
    }
}
