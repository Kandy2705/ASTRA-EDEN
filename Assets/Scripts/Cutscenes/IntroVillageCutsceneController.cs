using System;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Presentation helper riêng cho TL_Intro_Village. Timeline vẫn điều khiển
/// animation/activation tracks; component này chỉ nội suy camera, actor marker,
/// subtitle và fade theo thời gian của PlayableDirector.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class IntroVillageCutsceneController : MonoBehaviour
{
    [Serializable]
    public struct SubtitleCue
    {
        public double start;
        public double end;
        public string speaker;
        [TextArea(2, 4)] public string text;
    }

    [Serializable]
    public struct CameraCue
    {
        public Transform cameraTransform;
        public Transform lookTarget;
        public double start;
        public double end;
        public Vector3 startPosition;
        public Vector3 endPosition;
    }

    [Serializable]
    public struct ActorMotionCue
    {
        public Transform actorMarker;
        public double start;
        public double end;
        public Vector3 startPosition;
        public Vector3 endPosition;
        public bool faceTravelDirection;
    }

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;

    [Header("Presentation UI")]
    [SerializeField] private CanvasGroup subtitleGroup;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField, Min(0.01f)] private float openingFadeDuration = 1.5f;
    [SerializeField] private double closingFadeStart = 38d;
    [SerializeField] private double closingFadeEnd = 40d;

    [Header("Editable Cues")]
    [SerializeField] private SubtitleCue[] subtitles = Array.Empty<SubtitleCue>();
    [SerializeField] private CameraCue[] cameraCues = Array.Empty<CameraCue>();
    [SerializeField] private ActorMotionCue[] actorMotions = Array.Empty<ActorMotionCue>();

    private void Awake()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }

        if (Application.IsPlaying(gameObject))
        {
            ApplyPresentation(0d);
        }
    }

    private void OnEnable()
    {
        if (director != null)
        {
            director.stopped -= HandleDirectorStopped;
            director.stopped += HandleDirectorStopped;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.stopped -= HandleDirectorStopped;
        }
    }

    private void LateUpdate()
    {
        if (director == null)
        {
            return;
        }

#if UNITY_EDITOR
        // Animation Tracks can be previewed directly in the Timeline window while
        // the game is not running. Actor translation lives on the parent markers,
        // so it must be evaluated here as well or the actors appear to walk in place.
        // A valid playable graph means Timeline is currently evaluating/previewing;
        // outside preview we leave the scene transforms editable for layout work.
        if (!Application.IsPlaying(gameObject) && !director.playableGraph.IsValid())
        {
            return;
        }
#endif

        ApplyPresentation(director.time);
    }

    private void ApplyPresentation(double time)
    {
        UpdateSubtitles(time);
        UpdateFade(time);
        UpdateCameras(time);
        UpdateActors(time);
    }

    private void UpdateSubtitles(double time)
    {
        if (subtitleGroup == null || subtitleText == null)
        {
            return;
        }

        SubtitleCue? activeCue = null;
        for (int i = 0; i < subtitles.Length; i++)
        {
            if (time >= subtitles[i].start && time < subtitles[i].end)
            {
                activeCue = subtitles[i];
                break;
            }
        }

        if (!activeCue.HasValue)
        {
            subtitleGroup.alpha = 0f;
            subtitleText.text = string.Empty;
            return;
        }

        SubtitleCue cue = activeCue.Value;
        subtitleGroup.alpha = 1f;
        subtitleText.text = string.IsNullOrWhiteSpace(cue.speaker)
            ? cue.text
            : $"<color=#FFEDC7><b>{cue.speaker}</b></color>\n{cue.text}";
    }

    private void UpdateFade(double time)
    {
        if (fadeGroup == null)
        {
            return;
        }

        if (time < openingFadeDuration)
        {
            fadeGroup.alpha = 1f - Mathf.Clamp01((float)(time / openingFadeDuration));
        }
        else if (time >= closingFadeStart)
        {
            double duration = Math.Max(0.01d, closingFadeEnd - closingFadeStart);
            fadeGroup.alpha = Mathf.Clamp01((float)((time - closingFadeStart) / duration));
        }
        else
        {
            fadeGroup.alpha = 0f;
        }

        fadeGroup.blocksRaycasts = fadeGroup.alpha > 0.01f;
    }

    private void UpdateCameras(double time)
    {
        for (int i = 0; i < cameraCues.Length; i++)
        {
            CameraCue cue = cameraCues[i];
            if (cue.cameraTransform == null || time < cue.start || time > cue.end)
            {
                continue;
            }

            float progress = NormalizedTime(time, cue.start, cue.end);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 position = Vector3.LerpUnclamped(cue.startPosition, cue.endPosition, eased);
            cue.cameraTransform.position = position;

            if (cue.lookTarget != null)
            {
                Vector3 direction = cue.lookTarget.position - position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    cue.cameraTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }
        }
    }

    private void UpdateActors(double time)
    {
        for (int i = 0; i < actorMotions.Length; i++)
        {
            ActorMotionCue cue = actorMotions[i];
            if (cue.actorMarker == null)
            {
                continue;
            }

            float progress = NormalizedTime(time, cue.start, cue.end);
            cue.actorMarker.position = Vector3.LerpUnclamped(cue.startPosition, cue.endPosition, progress);

            Vector3 direction = cue.endPosition - cue.startPosition;
            if (cue.faceTravelDirection && direction.sqrMagnitude > 0.0001f)
            {
                cue.actorMarker.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }

    private static float NormalizedTime(double time, double start, double end)
    {
        double duration = Math.Max(0.01d, end - start);
        return Mathf.Clamp01((float)((time - start) / duration));
    }

    private void HandleDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (subtitleGroup != null)
        {
            subtitleGroup.alpha = 0f;
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 1f;
            fadeGroup.blocksRaycasts = true;
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        PlayableDirector playableDirector,
        CanvasGroup subtitleCanvasGroup,
        TMP_Text text,
        CanvasGroup fadeCanvasGroup,
        SubtitleCue[] subtitleCueData,
        CameraCue[] cameraCueData,
        ActorMotionCue[] actorMotionData)
    {
        director = playableDirector;
        subtitleGroup = subtitleCanvasGroup;
        subtitleText = text;
        fadeGroup = fadeCanvasGroup;
        subtitles = subtitleCueData ?? Array.Empty<SubtitleCue>();
        cameraCues = cameraCueData ?? Array.Empty<CameraCue>();
        actorMotions = actorMotionData ?? Array.Empty<ActorMotionCue>();
    }
#endif
}
