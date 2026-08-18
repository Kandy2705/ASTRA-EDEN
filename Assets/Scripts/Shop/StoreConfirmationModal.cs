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
    private bool listenersAdded;

    private void Awake()
    {
        ResolveReferences();
        BindListeners();
        HideImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindListeners();
        HideImmediate();
    }

    private void OnDestroy()
    {
        UnbindListeners();
    }

    private void ResolveReferences()
    {
        if (panel == null)
        {
            Transform found = transform.Find("PurchaseConfirmation");
            if (found == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child != null && child.name == "PurchaseConfirmation")
                    {
                        found = child;
                        break;
                    }
                }
            }
            if (found != null) panel = found.gameObject;
        }

        if (panel != null)
        {
            if (message == null)
            {
                Transform msgObj = panel.transform.Find("Message");
                message = msgObj != null ? msgObj.GetComponent<TMP_Text>() : panel.GetComponentInChildren<TMP_Text>(true);
            }
            if (confirmButton == null)
            {
                Transform confirmObj = panel.transform.Find("Confirm");
                if (confirmObj != null) confirmButton = confirmObj.GetComponent<Button>();
            }
            if (cancelButton == null)
            {
                Transform cancelObj = panel.transform.Find("Cancel");
                if (cancelObj != null) cancelButton = cancelObj.GetComponent<Button>();
            }
        }
    }

    private void BindListeners()
    {
        if (listenersAdded) return;
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        listenersAdded = confirmButton != null || cancelButton != null;
    }

    private void UnbindListeners()
    {
        if (!listenersAdded) return;
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
        listenersAdded = false;
    }

    public void Show(string prompt, Action onConfirmed)
    {
        ResolveReferences();
        BindListeners();
        resolving = false;
        confirmAction = onConfirmed;
        if (message != null) message.text = prompt;
        if (panel != null)
        {
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }

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
        ResolveReferences();
        if (panel == null) return;

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        PopupTween tween = panel.GetComponent<PopupTween>();
        if (tween != null) tween.Hide();
        else panel.SetActive(false);
    }

    public void HideImmediate()
    {
        ResolveReferences();
        if (panel == null) return;

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        PopupTween tween = panel.GetComponent<PopupTween>();
        if (tween != null) tween.SetHiddenImmediate();
        else panel.SetActive(false);
    }
}
