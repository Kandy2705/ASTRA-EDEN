using System;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Tween fade + scale dùng unscaled time, nên vẫn chạy khi gameplay đang pause.
/// </summary>
[DisallowMultipleComponent]
public sealed class PopupTween : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.3f;
    [SerializeField, Range(0.5f, 1f)] private float hiddenScale = 0.84f;

    private CanvasGroup canvasGroup;
    private RectTransform scaleTarget;
    private Sequence tweenSequence;
    private Vector3 shownScale = Vector3.one;

    private void Awake()
    {
        CacheReferences();
    }

    public void Show()
    {
        CacheReferences();
        StopCurrentTween();

        gameObject.SetActive(true);
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        tweenSequence = Sequence.Create(useUnscaledTime: true)
            .Group(Tween.Alpha(
                canvasGroup,
                canvasGroup.alpha,
                1f,
                duration,
                Ease.OutCubic))
            .Group(Tween.Scale(
                scaleTarget,
                scaleTarget.localScale,
                shownScale,
                duration,
                Ease.OutBack));
    }

    public void Hide(Action onComplete = null)
    {
        if (!gameObject.activeSelf)
        {
            SetHiddenImmediate();
            onComplete?.Invoke();
            return;
        }

        CacheReferences();
        StopCurrentTween();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        tweenSequence = Sequence.Create(useUnscaledTime: true)
            .Group(Tween.Alpha(
                canvasGroup,
                canvasGroup.alpha,
                0f,
                duration,
                Ease.InCubic))
            .Group(Tween.Scale(
                scaleTarget,
                scaleTarget.localScale,
                shownScale * hiddenScale,
                duration,
                Ease.InBack))
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    public void SetHiddenImmediate()
    {
        CacheReferences();
        StopCurrentTween();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        scaleTarget.localScale = shownScale * hiddenScale;
        gameObject.SetActive(false);
    }

    private void CacheReferences()
    {
        scaleTarget ??= FindScaleTarget(transform);
        canvasGroup ??= GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (scaleTarget != null && scaleTarget.localScale.sqrMagnitude > 0.001f)
        {
            Vector3 currentScale = scaleTarget.localScale;
            if (canvasGroup.alpha > 0.99f)
            {
                shownScale = currentScale;
            }
        }
    }

    private static RectTransform FindScaleTarget(Transform root)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "SettingBoder")
            {
                return children[i] as RectTransform;
            }
        }

        return root as RectTransform;
    }

    private void StopCurrentTween()
    {
        if (!tweenSequence.isAlive)
        {
            return;
        }

        tweenSequence.Stop();
    }
}
