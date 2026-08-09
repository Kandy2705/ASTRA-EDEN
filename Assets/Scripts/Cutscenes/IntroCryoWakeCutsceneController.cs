using System;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Presentation controller used only by TL_Intro_CryoWake. Timeline owns actor
/// animation and camera activation; this component evaluates editable motion,
/// camera, door, subtitle, power-shutdown and scene-transition cues.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class IntroCryoWakeCutsceneController : MonoBehaviour
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
        public bool useManualRotation;
        public Vector3 startEulerAngles;
        public Vector3 endEulerAngles;
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
        public bool useManualRotation;
        public Vector3 startEulerAngles;
        public Vector3 endEulerAngles;
    }

    [Serializable]
    public struct AudioCue
    {
        public string label;
        public double start;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;

    [Header("Presentation UI")]
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private CanvasGroup subtitleGroup;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private double titleStart = 0.65d;
    [SerializeField] private double titleEnd = 3.35d;
    [SerializeField] private double openingFadeStart = 3.2d;
    [SerializeField] private double openingFadeEnd = 5d;
    [SerializeField] private double closingFadeStart = 44d;
    [SerializeField] private double closingFadeEnd = 46d;

    [Header("Cryopod Power / Door")]
    [SerializeField] private Transform doorPivot;
    [SerializeField] private Vector3 doorClosedLocalPosition;
    [SerializeField] private Vector3 doorOpenLocalPosition;
    [SerializeField] private Vector3 doorClosedLocalEulerAngles;
    [SerializeField] private Vector3 doorOpenLocalEulerAngles = new(-72f, 0f, 0f);
    [SerializeField] private double doorOpenStart = 20.6d;
    [SerializeField] private double doorOpenEnd = 22.8d;
    [SerializeField] private Light interiorLight;
    [SerializeField] private Light statusLight;
    [SerializeField] private double shutdownStart = 19.8d;
    [SerializeField] private double shutdownEnd = 22.1d;
    [SerializeField, Min(0f)] private float interiorLightIntensity = 2.2f;
    [SerializeField, Min(0f)] private float statusLightIntensity = 2.8f;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource machineAmbienceSource;
    [SerializeField] private AudioSource oneShotAudioSource;
    [SerializeField] private AudioCue[] audioCues = Array.Empty<AudioCue>();

    [Header("Editable Cues")]
    [SerializeField] private SubtitleCue[] subtitles = Array.Empty<SubtitleCue>();
    [SerializeField] private CameraCue[] cameraCues = Array.Empty<CameraCue>();
    [SerializeField] private ActorMotionCue[] actorMotions = Array.Empty<ActorMotionCue>();

    [Header("Gameplay Transition")]
    [SerializeField] private bool loadNextSceneOnComplete = true;
    [SerializeField] private bool useLoadingScreen = true;
    [SerializeField] private string nextSceneName = "World_Eden7";

    private double previousTime = -1d;
    private bool[] playedAudioCues = Array.Empty<bool>();
    private bool transitionRequested;
    private bool reachedClosingFade;

    private void Awake()
    {
        ResolveDirector();
        EnsureAudioState();
        transitionRequested = false;
        reachedClosingFade = false;
        if (Application.IsPlaying(gameObject))
        {
            ApplyPresentation(0d);
            StartMachineAmbienceIfConfigured();
        }
    }

    private void OnEnable()
    {
        ResolveDirector();
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
        ResolveDirector();
        if (director == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.IsPlaying(gameObject) && !director.playableGraph.IsValid())
        {
            return;
        }
#endif

        ApplyPresentation(director.time);
        if (Application.IsPlaying(gameObject) && director.time >= closingFadeEnd - 0.02d)
        {
            RequestGameplayTransition();
        }
    }

    private void ResolveDirector()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }

    private void ApplyPresentation(double time)
    {
        if (Application.IsPlaying(gameObject) && time >= closingFadeEnd - 0.15d)
        {
            reachedClosingFade = true;
        }

        UpdateTitle(time);
        UpdateSubtitles(time);
        UpdateFade(time);
        UpdateCameras(time);
        UpdateActors(time);
        UpdateDoor(time);
        UpdateCryopodPower(time);
        UpdateAudio(time);
        previousTime = time;
    }

    private void UpdateTitle(double time)
    {
        if (titleGroup == null || titleText == null)
        {
            return;
        }

        bool visible = time >= titleStart && time < titleEnd;
        titleGroup.alpha = visible ? 1f : 0f;
        titleText.text = visible ? "TEN YEARS LATER" : string.Empty;
    }

    private void UpdateSubtitles(double time)
    {
        if (subtitleGroup == null || subtitleText == null)
        {
            return;
        }

        for (int i = 0; i < subtitles.Length; i++)
        {
            SubtitleCue cue = subtitles[i];
            if (time < cue.start || time >= cue.end)
            {
                continue;
            }

            subtitleGroup.alpha = 1f;
            subtitleText.text = string.IsNullOrWhiteSpace(cue.speaker)
                ? cue.text
                : $"<color=#FFEDC7><b>{cue.speaker}</b></color>\n{cue.text}";
            return;
        }

        subtitleGroup.alpha = 0f;
        subtitleText.text = string.Empty;
    }

    private void UpdateFade(double time)
    {
        if (fadeGroup == null)
        {
            return;
        }

        if (time < openingFadeStart)
        {
            fadeGroup.alpha = 1f;
        }
        else if (time < openingFadeEnd)
        {
            double duration = Math.Max(0.01d, openingFadeEnd - openingFadeStart);
            fadeGroup.alpha = 1f - Mathf.Clamp01((float)((time - openingFadeStart) / duration));
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

            float eased = Mathf.SmoothStep(0f, 1f, NormalizedTime(time, cue.start, cue.end));
            Vector3 position = Vector3.LerpUnclamped(cue.startPosition, cue.endPosition, eased);
            cue.cameraTransform.position = position;

            if (cue.useManualRotation)
            {
                cue.cameraTransform.rotation = Quaternion.SlerpUnclamped(
                    Quaternion.Euler(cue.startEulerAngles),
                    Quaternion.Euler(cue.endEulerAngles),
                    eased);
                continue;
            }

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
            Transform marker = actorMotions[i].actorMarker;
            if (marker == null || HasPreviousMarker(i, marker))
            {
                continue;
            }

            int earliestIndex = -1;
            int activeIndex = -1;
            int completedIndex = -1;

            for (int j = 0; j < actorMotions.Length; j++)
            {
                ActorMotionCue cue = actorMotions[j];
                if (cue.actorMarker != marker)
                {
                    continue;
                }

                if (earliestIndex < 0 || cue.start < actorMotions[earliestIndex].start)
                {
                    earliestIndex = j;
                }

                if (time >= cue.start && time <= cue.end)
                {
                    // If cues touch/overlap, the cue that starts later owns the
                    // actor. Inspector array order must not affect playback.
                    if (activeIndex < 0 || cue.start > actorMotions[activeIndex].start)
                    {
                        activeIndex = j;
                    }
                }
                else if (time > cue.end &&
                         (completedIndex < 0 || cue.end > actorMotions[completedIndex].end))
                {
                    completedIndex = j;
                }
            }

            if (earliestIndex < 0)
            {
                continue;
            }

            ActorMotionCue earliest = actorMotions[earliestIndex];
            Vector3 position = earliest.startPosition;
            Quaternion rotation = Quaternion.Euler(earliest.startEulerAngles);

            if (activeIndex >= 0)
            {
                ActorMotionCue active = actorMotions[activeIndex];
                float eased = Mathf.SmoothStep(0f, 1f, NormalizedTime(time, active.start, active.end));
                position = Vector3.LerpUnclamped(active.startPosition, active.endPosition, eased);
                rotation = ResolveActorRotation(active, eased, rotation);
            }
            else if (completedIndex >= 0)
            {
                ActorMotionCue completed = actorMotions[completedIndex];
                position = completed.endPosition;
                rotation = ResolveActorRotation(completed, 1f, rotation);
            }

            marker.SetPositionAndRotation(position, rotation);
        }
    }

    private static Quaternion ResolveActorRotation(ActorMotionCue cue, float progress, Quaternion fallback)
    {
        if (cue.useManualRotation)
        {
            return Quaternion.SlerpUnclamped(
                Quaternion.Euler(cue.startEulerAngles),
                Quaternion.Euler(cue.endEulerAngles),
                progress);
        }

        Vector3 direction = Vector3.ProjectOnPlane(cue.endPosition - cue.startPosition, Vector3.up);
        if (cue.faceTravelDirection && direction.sqrMagnitude > 0.0001f)
        {
            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        return fallback;
    }

    private bool HasPreviousMarker(int index, Transform marker)
    {
        for (int i = 0; i < index; i++)
        {
            if (actorMotions[i].actorMarker == marker)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateDoor(double time)
    {
        if (doorPivot == null)
        {
            return;
        }

        float eased = Mathf.SmoothStep(0f, 1f, NormalizedTime(time, doorOpenStart, doorOpenEnd));
        doorPivot.localPosition = Vector3.LerpUnclamped(doorClosedLocalPosition, doorOpenLocalPosition, eased);
        doorPivot.localRotation = Quaternion.SlerpUnclamped(
            Quaternion.Euler(doorClosedLocalEulerAngles),
            Quaternion.Euler(doorOpenLocalEulerAngles),
            eased);
    }

    private void UpdateCryopodPower(double time)
    {
        if (interiorLight != null)
        {
            if (time < shutdownStart)
            {
                interiorLight.enabled = true;
                interiorLight.intensity = interiorLightIntensity;
            }
            else if (time < shutdownEnd)
            {
                float flicker = Mathf.Sin((float)time * 41f) > 0.15f ? 1f : 0.08f;
                interiorLight.enabled = true;
                interiorLight.intensity = interiorLightIntensity * flicker;
            }
            else
            {
                interiorLight.enabled = false;
            }
        }

        if (statusLight != null)
        {
            statusLight.enabled = true;
            statusLight.color = time < shutdownStart
                ? new Color(0.18f, 0.82f, 1f)
                : new Color(1f, 0.12f, 0.05f);
            statusLight.intensity = time < shutdownEnd
                ? statusLightIntensity
                : statusLightIntensity * 0.45f;
        }

        if (Application.IsPlaying(gameObject) && machineAmbienceSource != null && time >= shutdownStart && machineAmbienceSource.isPlaying)
        {
            machineAmbienceSource.Stop();
        }
    }

    private void UpdateAudio(double time)
    {
        if (!Application.IsPlaying(gameObject) || oneShotAudioSource == null)
        {
            return;
        }

        EnsureAudioState();
        if (previousTime >= 0d && time + 0.05d < previousTime)
        {
            Array.Clear(playedAudioCues, 0, playedAudioCues.Length);
            StartMachineAmbienceIfConfigured();
        }

        for (int i = 0; i < audioCues.Length; i++)
        {
            AudioCue cue = audioCues[i];
            if (playedAudioCues[i] || cue.clip == null || time < cue.start)
            {
                continue;
            }

            playedAudioCues[i] = true;
            oneShotAudioSource.PlayOneShot(cue.clip, cue.volume <= 0f ? 1f : cue.volume);
        }
    }

    private void StartMachineAmbienceIfConfigured()
    {
        if (machineAmbienceSource == null || machineAmbienceSource.clip == null || machineAmbienceSource.isPlaying)
        {
            return;
        }

        machineAmbienceSource.loop = true;
        machineAmbienceSource.Play();
    }

    private void EnsureAudioState()
    {
        if (playedAudioCues.Length != audioCues.Length)
        {
            playedAudioCues = new bool[audioCues.Length];
        }
    }

    private static float NormalizedTime(double time, double start, double end)
    {
        double duration = Math.Max(0.01d, end - start);
        return Mathf.Clamp01((float)((time - start) / duration));
    }

    private void HandleDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (titleGroup != null)
        {
            titleGroup.alpha = 0f;
        }

        if (subtitleGroup != null)
        {
            subtitleGroup.alpha = 0f;
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 1f;
            fadeGroup.blocksRaycasts = true;
        }

        if (!Application.IsPlaying(gameObject) || !reachedClosingFade)
        {
            return;
        }

        RequestGameplayTransition();
    }

    private void RequestGameplayTransition()
    {
        if (transitionRequested || !loadNextSceneOnComplete)
        {
            return;
        }

        transitionRequested = true;
        if (IntroSequenceFlow.IsSequenceActive)
        {
            IntroSequenceFlow.MarkIntroCompleted();
        }

        if (!SceneTransitionService.IsSceneInBuildSettings(nextSceneName))
        {
            Debug.LogError($"[IntroCryoWake] Scene gameplay '{nextSceneName}' chưa có trong Build Settings.");
            return;
        }

        Debug.Log($"[IntroCryoWake] Timeline hoàn tất. Chuyển sang gameplay scene '{nextSceneName}'.");
        SceneTransitionService.Load(
            nextSceneName,
            useLoadingScreen,
            suppressLoadingAudio: true);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        PlayableDirector playableDirector,
        CanvasGroup titleCanvasGroup,
        TMP_Text titleLabel,
        CanvasGroup subtitleCanvasGroup,
        TMP_Text subtitleLabel,
        CanvasGroup fadeCanvasGroup,
        Transform cryoDoorPivot,
        Vector3 closedDoorPosition,
        Vector3 openDoorPosition,
        Vector3 closedDoorEuler,
        Vector3 openDoorEuler,
        Light cyanInteriorLight,
        Light redStatusLight,
        AudioSource ambienceSource,
        AudioSource sfxSource,
        SubtitleCue[] subtitleCueData,
        CameraCue[] cameraCueData,
        ActorMotionCue[] actorMotionData,
        AudioCue[] optionalAudioCueData,
        string gameplaySceneName)
    {
        director = playableDirector;
        titleGroup = titleCanvasGroup;
        titleText = titleLabel;
        subtitleGroup = subtitleCanvasGroup;
        subtitleText = subtitleLabel;
        fadeGroup = fadeCanvasGroup;
        doorPivot = cryoDoorPivot;
        doorClosedLocalPosition = closedDoorPosition;
        doorOpenLocalPosition = openDoorPosition;
        doorClosedLocalEulerAngles = closedDoorEuler;
        doorOpenLocalEulerAngles = openDoorEuler;
        interiorLight = cyanInteriorLight;
        statusLight = redStatusLight;
        machineAmbienceSource = ambienceSource;
        oneShotAudioSource = sfxSource;
        subtitles = subtitleCueData ?? Array.Empty<SubtitleCue>();
        cameraCues = cameraCueData ?? Array.Empty<CameraCue>();
        actorMotions = actorMotionData ?? Array.Empty<ActorMotionCue>();
        audioCues = optionalAudioCueData ?? Array.Empty<AudioCue>();
        nextSceneName = string.IsNullOrWhiteSpace(gameplaySceneName) ? "World_Eden7" : gameplaySceneName;
        loadNextSceneOnComplete = true;
        useLoadingScreen = true;
        EnsureAudioState();
    }
#endif
}
