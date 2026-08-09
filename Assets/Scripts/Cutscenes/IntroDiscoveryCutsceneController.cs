using System;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Presentation controller for TL_Intro_Discovery. Timeline owns animation and
/// camera activation; this component evaluates editable camera/actor paths,
/// subtitles, fades and optional audio cues from the same director clock.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class IntroDiscoveryCutsceneController : MonoBehaviour
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

        [Tooltip("When enabled, this cue uses the world-space Euler rotations below instead of Look Target.")]
        public bool useManualRotation;

        [Tooltip("World-space camera rotation at the beginning of this cue.")]
        public Vector3 startEulerAngles;

        [Tooltip("World-space camera rotation at the end of this cue.")]
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
    }

    [Serializable]
    public struct AudioCue
    {
        public double start;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;

    [Header("Presentation UI")]
    [SerializeField] private CanvasGroup subtitleGroup;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField, Min(0.01f)] private float openingFadeDuration = 1.2f;
    [SerializeField] private double closingFadeStart = 46.8d;
    [SerializeField] private double closingFadeEnd = 48d;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioCue[] audioCues = Array.Empty<AudioCue>();

    [Header("Editable Cues")]
    [SerializeField] private Terrain actorTerrain;
    [SerializeField, Min(0f)] private float actorGroundOffset = 0.05f;
    [SerializeField] private SubtitleCue[] subtitles = Array.Empty<SubtitleCue>();
    [SerializeField] private CameraCue[] cameraCues = Array.Empty<CameraCue>();
    [SerializeField] private ActorMotionCue[] actorMotions = Array.Empty<ActorMotionCue>();

    private double previousTime = -1d;
    private bool[] playedAudioCues = Array.Empty<bool>();

    private void Awake()
    {
        ResolveDirector();
        EnsureAudioState();
        if (Application.IsPlaying(gameObject))
        {
            ApplyPresentation(0d);
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
        UpdateSubtitles(time);
        UpdateFade(time);
        UpdateCameras(time);
        UpdateActors(time);
        UpdateAudio(time);
        previousTime = time;
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

            if (cue.useManualRotation)
            {
                Quaternion startRotation = Quaternion.Euler(cue.startEulerAngles);
                Quaternion endRotation = Quaternion.Euler(cue.endEulerAngles);
                cue.cameraTransform.rotation = Quaternion.SlerpUnclamped(startRotation, endRotation, eased);
                continue;
            }

            if (cue.lookTarget == null)
            {
                continue;
            }

            Vector3 direction = cue.lookTarget.position - position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                cue.cameraTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }

    private void UpdateActors(double time)
    {
        for (int i = 0; i < actorMotions.Length; i++)
        {
            ActorMotionCue firstCue = actorMotions[i];
            Transform marker = firstCue.actorMarker;
            if (marker == null || HasEarlierCueForMarker(i, marker))
            {
                continue;
            }

            Vector3 position = firstCue.startPosition;
            Vector3 facingDirection = firstCue.endPosition - firstCue.startPosition;
            bool shouldFace = firstCue.faceTravelDirection;

            for (int j = i; j < actorMotions.Length; j++)
            {
                ActorMotionCue cue = actorMotions[j];
                if (cue.actorMarker != marker)
                {
                    continue;
                }

                if (time < cue.start)
                {
                    break;
                }

                facingDirection = cue.endPosition - cue.startPosition;
                shouldFace = cue.faceTravelDirection;
                if (time <= cue.end)
                {
                    position = Vector3.LerpUnclamped(
                        cue.startPosition,
                        cue.endPosition,
                        NormalizedTime(time, cue.start, cue.end));
                    break;
                }

                position = cue.endPosition;
            }

            if (TrySampleTerrainHeight(position, out float terrainHeight))
            {
                position.y = terrainHeight + actorGroundOffset;
            }

            marker.position = position;
            if (shouldFace && facingDirection.sqrMagnitude > 0.0001f)
            {
                marker.rotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
            }
        }
    }

    private bool HasEarlierCueForMarker(int index, Transform marker)
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

    private bool TrySampleTerrainHeight(Vector3 worldPosition, out float height)
    {
        height = worldPosition.y;
        if (actorTerrain == null || actorTerrain.terrainData == null)
        {
            return false;
        }

        Vector3 local = worldPosition - actorTerrain.transform.position;
        Vector3 size = actorTerrain.terrainData.size;
        if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
        {
            return false;
        }

        height = actorTerrain.SampleHeight(worldPosition) + actorTerrain.transform.position.y;
        return true;
    }

    private void UpdateAudio(double time)
    {
        if (!Application.IsPlaying(gameObject) || audioSource == null)
        {
            return;
        }

        EnsureAudioState();
        if (previousTime >= 0d && time + 0.05d < previousTime)
        {
            Array.Clear(playedAudioCues, 0, playedAudioCues.Length);
        }

        for (int i = 0; i < audioCues.Length; i++)
        {
            AudioCue cue = audioCues[i];
            if (playedAudioCues[i] || cue.clip == null || time < cue.start)
            {
                continue;
            }

            playedAudioCues[i] = true;
            audioSource.PlayOneShot(cue.clip, cue.volume <= 0f ? 1f : cue.volume);
        }
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
        AudioSource optionalAudioSource,
        Terrain terrain,
        SubtitleCue[] subtitleCueData,
        CameraCue[] cameraCueData,
        ActorMotionCue[] actorMotionData)
    {
        director = playableDirector;
        subtitleGroup = subtitleCanvasGroup;
        subtitleText = text;
        fadeGroup = fadeCanvasGroup;
        audioSource = optionalAudioSource;
        actorTerrain = terrain;
        actorGroundOffset = 0.05f;
        audioCues = Array.Empty<AudioCue>();
        subtitles = subtitleCueData ?? Array.Empty<SubtitleCue>();
        cameraCues = cameraCueData ?? Array.Empty<CameraCue>();
        actorMotions = actorMotionData ?? Array.Empty<ActorMotionCue>();
        EnsureAudioState();
    }
#endif
}
