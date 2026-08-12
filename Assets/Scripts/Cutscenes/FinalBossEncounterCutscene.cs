using System;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Lightweight in-world introduction for the Final Boss. It intentionally
/// uses the real Player and Commander from World_Eden7, so battle can begin
/// directly after the Timeline without scene loading or duplicate actors.
/// </summary>
[DisallowMultipleComponent]
public sealed class FinalBossEncounterCutscene : MonoBehaviour
{
    [Serializable]
    public struct SubtitleCue
    {
        public double start;
        public double end;
        public string speaker;
        [TextArea(2, 3)] public string text;
    }

    [Serializable]
    public struct CameraCue
    {
        public Camera camera;
        public Transform lookTarget;
        public double start;
        public double end;
        public Vector3 startPosition;
        public Vector3 endPosition;
    }

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;

    [Header("Actors")]
    [SerializeField] private FinalBossBehaviour finalBoss;
    [SerializeField] private EnemyAIController bossAi;
    [SerializeField] private Animator bossAnimator;
    [SerializeField] private Transform playerStart;
    [SerializeField] private Transform playerStop;
    [SerializeField] private Transform bossThreatPoint;

    [Header("Camera")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private CameraController gameplayCameraController;
    [SerializeField] private CameraCue[] cameraCues = Array.Empty<CameraCue>();

    [Header("Presentation")]
    [SerializeField] private CanvasGroup subtitleGroup;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private SubtitleCue[] subtitles = Array.Empty<SubtitleCue>();
    [SerializeField] private double closingFadeStart = 21.5d;
    [SerializeField] private double closingFadeEnd = 23d;

    [Header("Actor Timing")]
    [SerializeField] private double playerWalkStart = 0d;
    [SerializeField] private double playerWalkEnd = 4.5d;
    [SerializeField] private double bossTurnStart = 9.5d;
    [SerializeField] private double bossTurnEnd = 13d;
    [SerializeField] private double bossThreatStart = 17.5d;
    [SerializeField] private double bossThreatEnd = 20d;

    Transform player;
    PlayerController playerController;
    PlayerCombatController playerCombat;
    PlayerInputReader playerInput;
    bool playing;
    bool playerControllerWasEnabled;
    bool playerCombatWasEnabled;
    bool playerInputWasEnabled;
    bool gameplayCameraWasEnabled;
    bool gameplayCameraControllerWasEnabled;
    bool bossAiWasEnabled;
    Quaternion bossInitialRotation;
    Vector3 bossInitialPosition;

    void Awake()
    {
        ResolveReferences();
        ReleasePresentationRaycasts();
    }

    void OnEnable()
    {
        ResolveReferences();
        if (!playing)
        {
            ReleasePresentationRaycasts();
        }
        if (director != null)
        {
            director.stopped -= HandleDirectorStopped;
            director.stopped += HandleDirectorStopped;
        }
    }

    void OnDisable()
    {
        if (director != null)
        {
            director.stopped -= HandleDirectorStopped;
        }
    }

    void LateUpdate()
    {
        if (!playing || director == null)
        {
            return;
        }

        ApplyPresentation(director.time);
    }

    public bool TryStartCutscene(Transform enteringPlayer)
    {
        return StartCutscene(enteringPlayer, false);
    }

    /// <summary>Debug-only friendly entry point: ignores the saved "already seen" flag.</summary>
    public bool TryStartForDemo()
    {
        return StartCutscene(FindPlayer(), true);
    }

    bool StartCutscene(Transform enteringPlayer, bool ignoreProgression)
    {
        ResolveReferences();
        if (playing || director == null || finalBoss == null || bossAi == null ||
            (!ignoreProgression && GameDataManager.Instance != null &&
             (GameDataManager.Instance.IsFinalBossEncounterSeen || GameDataManager.Instance.IsFinalBossDefeated)))
        {
            if (!playing && (director == null || finalBoss == null || bossAi == null))
            {
                Debug.LogWarning(
                    $"[FinalBossEncounter] Không thể chạy: Director={(director != null)}, " +
                    $"FinalBoss={(finalBoss != null)}, BossAI={(bossAi != null)}.",
                    this);
            }
            return false;
        }

        player = enteringPlayer != null ? enteringPlayer : FindPlayer();
        if (player == null)
        {
            Debug.LogWarning("[FinalBossEncounter] Không tìm thấy Player để bắt đầu cutscene.", this);
            return false;
        }

        CachePlayerComponents();
        bossInitialPosition = finalBoss.transform.position;
        bossInitialRotation = finalBoss.transform.rotation;
        LockGameplay();

        if (playerStart != null)
        {
            player.SetPositionAndRotation(playerStart.position, playerStart.rotation);
        }

        bossAi.PauseForCinematic();
        bossAi.enabled = false;
        EnableShotCameraAt(0d);

        playing = true;
        director.time = 0d;
        director.Evaluate();
        director.Play();
        Debug.Log("[FinalBossEncounter] Bắt đầu TL_Boss_Encounter.", this);
        return true;
    }

    void LockGameplay()
    {
        playerControllerWasEnabled = playerController != null && playerController.enabled;
        playerCombatWasEnabled = playerCombat != null && playerCombat.enabled;
        playerInputWasEnabled = playerInput != null && playerInput.enabled;
        gameplayCameraWasEnabled = gameplayCamera != null && gameplayCamera.enabled;
        gameplayCameraControllerWasEnabled = gameplayCameraController != null && gameplayCameraController.enabled;
        bossAiWasEnabled = bossAi != null && bossAi.enabled;

        if (playerController != null) playerController.enabled = false;
        if (playerCombat != null) playerCombat.enabled = false;
        if (playerInput != null) playerInput.enabled = false;
        if (gameplayCameraController != null) gameplayCameraController.enabled = false;
        if (gameplayCamera != null) gameplayCamera.enabled = false;
    }

    void RestoreGameplay()
    {
        ReleasePresentationRaycasts();

        foreach (CameraCue cue in cameraCues)
        {
            if (cue.camera != null) cue.camera.gameObject.SetActive(false);
        }

        if (gameplayCamera != null) gameplayCamera.enabled = gameplayCameraWasEnabled;
        if (gameplayCameraController != null) gameplayCameraController.enabled = gameplayCameraControllerWasEnabled;
        if (playerInput != null) playerInput.enabled = playerInputWasEnabled;
        if (playerCombat != null) playerCombat.enabled = playerCombatWasEnabled;
        if (playerController != null) playerController.enabled = playerControllerWasEnabled;

        if (bossAnimator != null)
        {
            bossAnimator.Rebind();
        }

        if (bossAi != null && bossAiWasEnabled)
        {
            bossAi.enabled = true;
            bossAi.ResumeFromCinematic(0.85f);
        }
    }

    void ApplyPresentation(double time)
    {
        UpdateCameras(time);
        UpdateSubtitles(time);
        UpdateFade(time);
        UpdateActors(time);
    }

    void UpdateCameras(double time)
    {
        for (int i = 0; i < cameraCues.Length; i++)
        {
            CameraCue cue = cameraCues[i];
            if (cue.camera == null) continue;
            bool active = time >= cue.start && time < cue.end;
            if (cue.camera.gameObject.activeSelf != active) cue.camera.gameObject.SetActive(active);
            if (!active) continue;

            float t = Normalized(time, cue.start, cue.end);
            cue.camera.transform.position = Vector3.Lerp(cue.startPosition, cue.endPosition, Mathf.SmoothStep(0f, 1f, t));
            if (cue.lookTarget != null)
            {
                Vector3 direction = cue.lookTarget.position - cue.camera.transform.position;
                if (direction.sqrMagnitude > 0.001f)
                {
                    cue.camera.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }
        }
    }

    void UpdateSubtitles(double time)
    {
        if (subtitleGroup == null || subtitleText == null) return;
        foreach (SubtitleCue cue in subtitles)
        {
            if (time < cue.start || time >= cue.end) continue;
            subtitleGroup.alpha = 1f;
            subtitleText.text = string.IsNullOrWhiteSpace(cue.speaker)
                ? cue.text
                : $"<color=#FFEDC7><b>{cue.speaker}</b></color>\n{cue.text}";
            return;
        }

        subtitleGroup.alpha = 0f;
        subtitleText.text = string.Empty;
    }

    void UpdateFade(double time)
    {
        if (fadeGroup == null) return;
        fadeGroup.alpha = time < closingFadeStart
            ? 0f
            : Normalized(time, closingFadeStart, closingFadeEnd);
        fadeGroup.blocksRaycasts = fadeGroup.alpha > 0.01f;
    }

    void UpdateActors(double time)
    {
        if (player != null && playerStart != null && playerStop != null)
        {
            float progress = Normalized(time, playerWalkStart, playerWalkEnd);
            player.position = Vector3.Lerp(playerStart.position, playerStop.position, Mathf.SmoothStep(0f, 1f, progress));
            Face(player, finalBoss != null ? finalBoss.transform.position : playerStop.position);
        }

        if (finalBoss == null) return;
        if (time < bossTurnStart)
        {
            finalBoss.transform.SetPositionAndRotation(bossInitialPosition, bossInitialRotation);
            return;
        }

        Quaternion facePlayer = LookRotation(finalBoss.transform.position, player != null ? player.position : finalBoss.transform.forward);
        float turn = Normalized(time, bossTurnStart, bossTurnEnd);
        finalBoss.transform.rotation = Quaternion.Slerp(bossInitialRotation, facePlayer, Mathf.SmoothStep(0f, 1f, turn));

        if (bossThreatPoint != null && time >= bossThreatStart)
        {
            float step = Normalized(time, bossThreatStart, bossThreatEnd);
            finalBoss.transform.position = Vector3.Lerp(bossInitialPosition, bossThreatPoint.position, Mathf.SmoothStep(0f, 1f, step));
        }
    }

    void HandleDirectorStopped(PlayableDirector stopped)
    {
        if (!playing || stopped != director) return;
        playing = false;
        ReleasePresentationRaycasts();
        RestoreGameplay();
        GameDataManager.Instance?.MarkFinalBossEncounterSeen();
        Debug.Log("[FinalBossEncounter] Hoàn tất — Commander chuyển Idle/Chase với attack delay an toàn.", this);
    }

    void ReleasePresentationRaycasts()
    {
        if (subtitleGroup != null)
        {
            subtitleGroup.alpha = 0f;
            subtitleGroup.blocksRaycasts = false;
            subtitleGroup.interactable = false;
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;
        }
    }

    void ResolveReferences()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
        if (finalBoss == null) finalBoss = FindFirstObjectByType<FinalBossBehaviour>(FindObjectsInactive.Include);
        if (bossAi == null && finalBoss != null) bossAi = finalBoss.GetComponent<EnemyAIController>();
        if (bossAnimator == null && finalBoss != null) bossAnimator = finalBoss.GetComponentInChildren<Animator>(true);
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (gameplayCameraController == null && gameplayCamera != null) gameplayCameraController = gameplayCamera.GetComponent<CameraController>();
    }

    void CachePlayerComponents()
    {
        playerController = player.GetComponent<PlayerController>();
        playerCombat = player.GetComponent<PlayerCombatController>();
        playerInput = player.GetComponent<PlayerInputReader>();
    }

    Transform FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }

    void EnableShotCameraAt(double time)
    {
        foreach (CameraCue cue in cameraCues)
        {
            if (cue.camera != null) cue.camera.gameObject.SetActive(time >= cue.start && time < cue.end);
        }
    }

    static float Normalized(double time, double start, double end) =>
        Mathf.Clamp01((float)((time - start) / Math.Max(0.01d, end - start)));

    static void Face(Transform actor, Vector3 target)
    {
        if (actor == null) return;
        actor.rotation = LookRotation(actor.position, target);
    }

    static Quaternion LookRotation(Vector3 from, Vector3 target)
    {
        Vector3 direction = Vector3.ProjectOnPlane(target - from, Vector3.up);
        return direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        PlayableDirector playableDirector, FinalBossBehaviour boss, EnemyAIController ai, Animator animator,
        Transform enter, Transform stop, Transform threat, Camera mainCamera, CameraController mainCameraController,
        CameraCue[] cameras, CanvasGroup subtitleCanvas, TMP_Text subtitleLabel, CanvasGroup fadeCanvas,
        SubtitleCue[] dialogue)
    {
        director = playableDirector;
        finalBoss = boss;
        bossAi = ai;
        bossAnimator = animator;
        playerStart = enter;
        playerStop = stop;
        bossThreatPoint = threat;
        gameplayCamera = mainCamera;
        gameplayCameraController = mainCameraController;
        cameraCues = cameras ?? Array.Empty<CameraCue>();
        subtitleGroup = subtitleCanvas;
        subtitleText = subtitleLabel;
        fadeGroup = fadeCanvas;
        subtitles = dialogue ?? Array.Empty<SubtitleCue>();
    }
#endif
}
