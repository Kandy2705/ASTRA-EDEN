using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HeroUpgradeToast : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private TMP_Text label;
    private Coroutine routine;

    public void Show(string message)
    {
        EnsureView();
        if (canvasGroup == null || label == null)
        {
            return;
        }

        label.text = message;
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(ShowRoutine());
    }

    private void EnsureView()
    {
        if (canvasGroup != null)
        {
            return;
        }

        GameObject toast = new GameObject(
            "HeroUpgradeNotification",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        toast.transform.SetParent(transform, false);
        toast.transform.SetAsLastSibling();

        RectTransform rect = toast.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -72f);
        rect.sizeDelta = new Vector2(620f, 64f);

        Image background = toast.GetComponent<Image>();
        background.color = new Color(0.055f, 0.047f, 0.04f, 0.96f);

        Outline outline = toast.AddComponent<Outline>();
        outline.effectColor = new Color(0.92f, 0.66f, 0.35f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textObject = new GameObject(
            "Message",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(toast.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 4f);
        textRect.offsetMax = new Vector2(-20f, -4f);

        label = textObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 25f;
        label.color = new Color(1f, 0.88f, 0.66f, 1f);

        canvasGroup = toast.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;
    }

    private IEnumerator ShowRoutine()
    {
        const float fadeDuration = 0.18f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(1.5f);

        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        routine = null;
    }
}
