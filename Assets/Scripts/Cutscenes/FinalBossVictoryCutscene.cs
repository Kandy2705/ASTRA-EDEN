using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Runs once when the living Final Boss health reaches zero. The existing AI
/// still begins its own Death animation first; this director only frames that
/// animation and permanently locks gameplay before transitioning to the
/// configured ending scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class FinalBossVictoryCutscene : MonoBehaviour
{
    const string MainMenuSceneName = "MainMenu";
    const string LegacyEndingSceneName = "TL_Ending_Freedom";

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

    [Header("Boss")]
    [SerializeField] private FinalBossBehaviour finalBoss;
    [SerializeField] private CharacterHealth bossHealth;
    [SerializeField] private EnemyAIController bossAi;

    [Header("Camera")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private CameraController gameplayCameraController;
    [SerializeField] private CameraCue[] cameraCues = Array.Empty<CameraCue>();

    [Header("Presentation")]
    [SerializeField] private CanvasGroup subtitleGroup;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private GameObject gameplayHudRoot;
    [SerializeField] private Light[] towerLights = Array.Empty<Light>();
    [SerializeField] private double towerShutdownStart = 12d;
    [SerializeField] private double victoryTextStart = 16d;
    [SerializeField] private double victoryTextEnd = 19d;
    [SerializeField] private double closingFadeStart = 19d;
    [SerializeField] private double closingFadeEnd = 21d;

    [Header("Ending Transition")]
    [SerializeField] private bool loadEndingOnComplete = true;
    [SerializeField] private bool useLoadingScreen = true;
    [SerializeField] private string nextSceneName = MainMenuSceneName;

    Transform player;
    PlayerController playerController;
    PlayerCombatController playerCombat;
    PlayerInputReader playerInput;
    bool playing;
    bool transitionRequested;
    bool previewMode;
    bool gameplayCameraWasEnabled;
    bool gameplayCameraControllerWasEnabled;

    void Awake()
    {
        MigrateLegacyEndingScene();
        ResolveReferences();
        ReleaseInactivePresentationRaycasts();
    }

    void OnValidate()
    {
        MigrateLegacyEndingScene();
    }

    void OnEnable()
    {
        ResolveReferences();
        if (!playing)
        {
            ReleaseInactivePresentationRaycasts();
        }
        if (bossHealth != null)
        {
            bossHealth.Died -= HandleBossDied;
            bossHealth.Died += HandleBossDied;
        }

        if (director != null)
        {
            director.stopped -= HandleDirectorStopped;
            director.stopped += HandleDirectorStopped;
        }
    }

    void OnDisable()
    {
        if (bossHealth != null) bossHealth.Died -= HandleBossDied;
        if (director != null) director.stopped -= HandleDirectorStopped;
    }

    void LateUpdate()
    {
        if (!playing || director == null) return;
        ApplyPresentation(director.time);
    }

    void HandleBossDied(CharacterHealth _)
    {
        if (playing || (GameDataManager.Instance != null && GameDataManager.Instance.IsFinalBossDefeated))
        {
            return;
        }

        StartCoroutine(BeginAfterDeathState());
    }

    /// <summary>
    /// Plays the victory presentation without saving completion or loading the
    /// ending. This exists solely for the in-game teacher/demo debug panel.
    /// </summary>
    public bool TryPlayForDemo()
    {
        ResolveReferences();
        if (playing || director == null || finalBoss == null ||
            (bossHealth != null && bossHealth.IsDead))
        {
            return false;
        }

        previewMode = true;
        StartCoroutine(BeginPreview());
        return true;
    }

    IEnumerator BeginAfterDeathState()
    {
        // Let EnemyAIController consume Died and enter its Death animation.
        yield return null;

        if (playing || bossHealth == null || !bossHealth.IsDead)
        {
            yield break;
        }

        player = FindPlayer();
        BeginVictoryPresentation(markDefeated: true);
    }

    IEnumerator BeginPreview()
    {
        yield return null;
        if (playing || finalBoss == null || bossHealth == null || bossHealth.IsDead)
        {
            previewMode = false;
            yield break;
        }

        Animator animator = finalBoss.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.ResetTrigger("Hit");
            animator.ResetTrigger("Stagger");
            animator.SetTrigger("Die");
        }

        player = FindPlayer();
        BeginVictoryPresentation(markDefeated: false);
    }

    void BeginVictoryPresentation(bool markDefeated)
    {
        LockGameplay();
        bossAi?.PauseForCinematic();
        if (bossAi != null) bossAi.enabled = false;
        BossHUDController hud = FindFirstObjectByType<BossHUDController>(FindObjectsInactive.Include);
        hud?.ClearBoss();
        if (markDefeated)
        {
            GameDataManager.Instance?.MarkFinalBossDefeated();
        }

        EnableShotCameraAt(0d);
        playing = true;
        director.time = 0d;
        director.Evaluate();
        director.Play();
        Debug.Log(previewMode
            ? "[FinalBossVictory] Bắt đầu preview TL_Boss_Victory."
            : "[FinalBossVictory] Bắt đầu TL_Boss_Victory.", this);
    }

    void LockGameplay()
    {
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            playerCombat = player.GetComponent<PlayerCombatController>();
            playerInput = player.GetComponent<PlayerInputReader>();
            if (playerController != null) playerController.enabled = false;
            if (playerCombat != null) playerCombat.enabled = false;
            if (playerInput != null) playerInput.enabled = false;
        }

        gameplayCameraWasEnabled = gameplayCamera != null && gameplayCamera.enabled;
        gameplayCameraControllerWasEnabled = gameplayCameraController != null && gameplayCameraController.enabled;
        if (gameplayCameraController != null) gameplayCameraController.enabled = false;
        if (gameplayCamera != null) gameplayCamera.enabled = false;

        if (gameplayHudRoot != null) gameplayHudRoot.SetActive(false);
    }

    void ApplyPresentation(double time)
    {
        UpdateCameras(time);
        if (player != null && finalBoss != null && time >= 7d)
        {
            Vector3 direction = Vector3.ProjectOnPlane(finalBoss.transform.position - player.position, Vector3.up);
            if (direction.sqrMagnitude > 0.001f)
            {
                player.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        if (towerLights != null && time >= towerShutdownStart)
        {
            foreach (Light light in towerLights)
            {
                if (light != null) light.enabled = false;
            }
        }

        if (subtitleGroup != null && subtitleText != null)
        {
            bool visible = time >= victoryTextStart && time < victoryTextEnd;
            subtitleGroup.alpha = visible ? 1f : 0f;
            subtitleText.text = visible ? "<color=#FFEDC7><b>ASTRA EDEN</b></color>\nThe island is finally free." : string.Empty;
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = time < closingFadeStart
                ? 0f
                : Normalized(time, closingFadeStart, closingFadeEnd);
            fadeGroup.blocksRaycasts = fadeGroup.alpha > 0.01f;
        }

        if (!previewMode && time >= closingFadeEnd - 0.02d)
        {
            RequestEndingTransition();
        }
    }

    void UpdateCameras(double time)
    {
        foreach (CameraCue cue in cameraCues)
        {
            if (cue.camera == null) continue;
            bool active = time >= cue.start && time < cue.end;
            if (cue.camera.gameObject.activeSelf != active) cue.camera.gameObject.SetActive(active);
            if (!active) continue;

            float t = Mathf.SmoothStep(0f, 1f, Normalized(time, cue.start, cue.end));
            cue.camera.transform.position = Vector3.Lerp(cue.startPosition, cue.endPosition, t);
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

    void HandleDirectorStopped(PlayableDirector stopped)
    {
        if (!playing || stopped != director) return;

        if (previewMode)
        {
            EndPreview();
            return;
        }

        RequestEndingTransition();
    }

    void EndPreview()
    {
        playing = false;
        previewMode = false;
        transitionRequested = false;
        foreach (CameraCue cue in cameraCues)
        {
            if (cue.camera != null) cue.camera.gameObject.SetActive(false);
        }

        if (subtitleGroup != null) subtitleGroup.alpha = 0f;
        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }

        if (gameplayHudRoot != null) gameplayHudRoot.SetActive(true);
        if (gameplayCamera != null) gameplayCamera.enabled = gameplayCameraWasEnabled;
        if (gameplayCameraController != null) gameplayCameraController.enabled = gameplayCameraControllerWasEnabled;
        if (playerController != null) playerController.enabled = true;
        if (playerCombat != null) playerCombat.enabled = true;
        if (playerInput != null) playerInput.enabled = true;
        if (bossAi != null)
        {
            bossAi.enabled = true;
            bossAi.ResumeFromCinematic(0.75f);
        }

        Animator animator = finalBoss != null ? finalBoss.GetComponentInChildren<Animator>(true) : null;
        animator?.Rebind();
        Debug.Log("[FinalBossVictory] Preview hoàn tất — gameplay đã được khôi phục, không lưu trạng thái thắng.", this);
    }

    void ReleaseInactivePresentationRaycasts()
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

    void RequestEndingTransition()
    {
        if (transitionRequested || !loadEndingOnComplete) return;
        transitionRequested = true;

        // Scene instance đang mở trong Unity có thể vẫn giữ serialized value cũ
        // dù YAML trên đĩa đã đổi. Luôn migrate ngay trước khi load để không kẹt
        // ở màn fade đen trong buổi demo.
        MigrateLegacyEndingScene();

        if (!SceneTransitionService.IsSceneInBuildSettings(nextSceneName))
        {
            Debug.LogWarning($"[FinalBossVictory] Scene ending '{nextSceneName}' chưa có trong Build Settings. Giữ fade đen; hãy assign/đưa scene này vào Build Profile.", this);
            return;
        }

        SceneTransitionService.Load(nextSceneName, useLoadingScreen, suppressLoadingAudio: true);
    }

    void MigrateLegacyEndingScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName) ||
            string.Equals(nextSceneName, LegacyEndingSceneName, StringComparison.Ordinal))
        {
            nextSceneName = MainMenuSceneName;
        }
    }

    void ResolveReferences()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
        if (finalBoss == null) finalBoss = FindFirstObjectByType<FinalBossBehaviour>(FindObjectsInactive.Include);
        if (bossHealth == null && finalBoss != null) bossHealth = finalBoss.GetComponent<CharacterHealth>();
        if (bossAi == null && finalBoss != null) bossAi = finalBoss.GetComponent<EnemyAIController>();
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (gameplayCameraController == null && gameplayCamera != null) gameplayCameraController = gameplayCamera.GetComponent<CameraController>();
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

#if UNITY_EDITOR
    public void EditorConfigure(
        PlayableDirector playableDirector, FinalBossBehaviour boss, CharacterHealth health, EnemyAIController ai,
        Camera mainCamera, CameraController mainCameraController, CameraCue[] cameras,
        CanvasGroup subtitleCanvas, TMP_Text subtitleLabel, CanvasGroup fadeCanvas,
        GameObject gameplayHud, Light[] nearbyTowerLights)
    {
        director = playableDirector;
        finalBoss = boss;
        bossHealth = health;
        bossAi = ai;
        gameplayCamera = mainCamera;
        gameplayCameraController = mainCameraController;
        cameraCues = cameras ?? Array.Empty<CameraCue>();
        subtitleGroup = subtitleCanvas;
        subtitleText = subtitleLabel;
        fadeGroup = fadeCanvas;
        gameplayHudRoot = gameplayHud;
        towerLights = nearbyTowerLights ?? Array.Empty<Light>();
        nextSceneName = MainMenuSceneName;
    }
#endif
}
