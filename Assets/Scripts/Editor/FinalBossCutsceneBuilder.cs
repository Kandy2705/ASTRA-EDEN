#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

/// <summary>
/// Creates the two in-world Commander cinematics in World_Eden7. They use the
/// real Final Boss + Player so no duplicate scene actors can desynchronise from
/// actual boss combat/progression.
/// </summary>
public static class FinalBossCutsceneBuilder
{
    const string ScenePath = "Assets/Scenes/World_Eden7.unity";
    const string TimelineFolder = "Assets/_Project/Timeline/Cutscenes";
    const string EncounterTimelinePath = TimelineFolder + "/TL_Boss_Encounter.playable";
    const string VictoryTimelinePath = TimelineFolder + "/TL_Boss_Victory.playable";
    const string RootName = "CS_FinalBossCinematics_Root";
    const double EncounterDuration = 23d;
    const double VictoryDuration = 21d;

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Build Final Boss Encounter + Victory")]
    public static void Build()
    {
        BuildInternal(rebuild: false);
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Rebuild Final Boss Encounter + Victory")]
    public static void Rebuild()
    {
        BuildInternal(rebuild: true);
    }

    public static void BuildBatch()
    {
        BuildInternal(rebuild: true);
        EditorApplication.Exit(0);
    }

    static void BuildInternal(bool rebuild)
    {
        EnsureFolder(TimelineFolder);
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject existingRoot = FindByName(scene, RootName);
            if (existingRoot != null && !rebuild)
            {
                Debug.Log("[FinalBossCutsceneBuilder] Cutscene root đã tồn tại. Dùng menu Rebuild nếu muốn tạo lại toàn bộ camera/marker.", existingRoot);
                return;
            }

            if (existingRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(existingRoot);
            }

            FinalBossBehaviour boss = FindComponent<FinalBossBehaviour>(scene);
            GameObject player = FindTagged(scene, "Player");
            Camera mainCamera = FindMainCamera(scene);
            if (boss == null || player == null || mainCamera == null)
            {
                Debug.LogError("[FinalBossCutsceneBuilder] Cần có FinalBossBehaviour, Player tag và Main Camera trong World_Eden7.");
                return;
            }

            EnemyAIController bossAi = boss.GetComponent<EnemyAIController>();
            CharacterHealth bossHealth = boss.GetComponent<CharacterHealth>();
            Animator bossAnimator = boss.GetComponentInChildren<Animator>(true);
            Animator playerAnimator = player.GetComponentInChildren<Animator>(true);
            if (bossAi == null || bossHealth == null || bossAnimator == null || playerAnimator == null)
            {
                Debug.LogError("[FinalBossCutsceneBuilder] Thiếu EnemyAI/Health/Animator trên Boss hoặc Player.", boss.gameObject);
                return;
            }

            GameObject rootObject = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(rootObject, scene);
            Transform root = rootObject.transform;
            root.position = boss.transform.position;

            Vector3 forward = Vector3.ProjectOnPlane(boss.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float groundY = boss.transform.position.y;

            Transform encounterRoot = CreateMarker(root, "CS_FinalBossEncounter_Root", boss.transform.position, boss.transform.rotation);
            Transform playerEnter = CreateMarker(encounterRoot, "CS_Player_EnterArena",
                boss.transform.position + forward * 12f + Vector3.up * (player.transform.position.y - groundY), Quaternion.LookRotation(-forward, Vector3.up));
            Transform playerStop = CreateMarker(encounterRoot, "CS_Player_Stop",
                boss.transform.position + forward * 6.5f + Vector3.up * (player.transform.position.y - groundY), Quaternion.LookRotation(-forward, Vector3.up));
            Transform bossThreat = CreateMarker(encounterRoot, "CS_FinalBoss_ThreatStep",
                boss.transform.position + forward * 0.75f, boss.transform.rotation);
            Transform bossLook = CreateMarker(encounterRoot, "CS_Look_FinalBoss", boss.transform.position + Vector3.up * 2.2f, Quaternion.identity);
            Transform playerLook = CreateMarker(encounterRoot, "CS_Look_Player", playerStop.position + Vector3.up * 1.5f, Quaternion.identity);
            Transform midpointLook = CreateMarker(encounterRoot, "CS_Look_Confrontation", Vector3.Lerp(playerStop.position, boss.transform.position, 0.5f) + Vector3.up * 1.65f, Quaternion.identity);

            Camera[] encounterCameras =
            {
                CreateCamera(encounterRoot, "CS_Camera_BossEncounter_01", playerEnter.position - forward * 3f + right * 11f + Vector3.up * 7f, midpointLook, 48f),
                CreateCamera(encounterRoot, "CS_Camera_BossEncounter_02", boss.transform.position + forward * 4.5f + right * 3.2f + Vector3.up * 2.6f, bossLook, 44f),
                CreateCamera(encounterRoot, "CS_Camera_BossEncounter_03", playerStop.position - forward * 1f - right * 3.8f + Vector3.up * 2.2f, bossLook, 46f),
                CreateCamera(encounterRoot, "CS_Camera_BossEncounter_04", Vector3.Lerp(playerStop.position, boss.transform.position, 0.5f) + right * 5.5f + Vector3.up * 2.4f, midpointLook, 45f),
                CreateCamera(encounterRoot, "CS_Camera_BossEncounter_05", boss.transform.position + forward * 3f - right * 2.5f + Vector3.up * 2.1f, bossLook, 42f)
            };

            CreatePresentationCanvas(encounterRoot, "CS_BossEncounter_UI", out CanvasGroup encounterSubtitle,
                out TMP_Text encounterText, out CanvasGroup encounterFade);
            TimelineAsset encounterTimeline = ReplaceTimeline(EncounterTimelinePath, "TL_Boss_Encounter");
            PlayableDirector encounterDirector = CreateDirector(encounterRoot, "TL_Boss_Encounter_Director", encounterTimeline);
            CreateEncounterTimeline(encounterTimeline, encounterDirector, playerAnimator, bossAnimator);

            FinalBossEncounterCutscene encounter = encounterDirector.gameObject.AddComponent<FinalBossEncounterCutscene>();
            encounter.EditorConfigure(
                encounterDirector, boss, bossAi, bossAnimator, playerEnter, playerStop, bossThreat,
                mainCamera, mainCamera.GetComponent<CameraController>(),
                CreateEncounterCues(encounterCameras, midpointLook, bossLook),
                encounterSubtitle, encounterText, encounterFade, CreateEncounterDialogue());

            GameObject triggerObject = new GameObject("CS_FinalBossEncounter_Trigger", typeof(BoxCollider), typeof(FinalBossEncounterTrigger));
            triggerObject.transform.SetParent(encounterRoot, false);
            triggerObject.transform.SetPositionAndRotation(boss.transform.position + forward * 10f, Quaternion.LookRotation(forward, Vector3.up));
            BoxCollider triggerCollider = triggerObject.GetComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector3(13f, 5f, 12f);
            triggerCollider.center = new Vector3(0f, 2.2f, 0f);
            triggerObject.GetComponent<FinalBossEncounterTrigger>().Configure(encounter);

            Transform victoryRoot = CreateMarker(root, "CS_FinalBossVictory_Root", boss.transform.position, boss.transform.rotation);
            Transform defeatedBossLook = CreateMarker(victoryRoot, "CS_Look_DefeatedBoss", boss.transform.position + Vector3.up * 1.25f, Quaternion.identity);
            Transform towerLook = CreateMarker(victoryRoot, "CS_Look_Tower", boss.transform.position - forward * 11f + Vector3.up * 7f, Quaternion.identity);
            Transform victoryPlayerLook = CreateMarker(victoryRoot, "CS_Look_VictoryPlayer", playerStop.position + Vector3.up * 1.6f, Quaternion.identity);
            Camera[] victoryCameras =
            {
                CreateCamera(victoryRoot, "CS_Camera_BossVictory_01", boss.transform.position + forward * 6.5f + right * 6f + Vector3.up * 3.8f, defeatedBossLook, 46f),
                CreateCamera(victoryRoot, "CS_Camera_BossVictory_02", playerStop.position - forward * 2f - right * 3.8f + Vector3.up * 2.2f, victoryPlayerLook, 45f),
                CreateCamera(victoryRoot, "CS_Camera_BossVictory_03", boss.transform.position - forward * 18f + right * 10f + Vector3.up * 9f, towerLook, 52f),
                CreateCamera(victoryRoot, "CS_Camera_BossVictory_04", playerStop.position - forward * 3f + right * 3.8f + Vector3.up * 2.3f, victoryPlayerLook, 45f)
            };
            CreatePresentationCanvas(victoryRoot, "CS_BossVictory_UI", out CanvasGroup victorySubtitle,
                out TMP_Text victoryText, out CanvasGroup victoryFade);
            TimelineAsset victoryTimeline = ReplaceTimeline(VictoryTimelinePath, "TL_Boss_Victory");
            PlayableDirector victoryDirector = CreateDirector(victoryRoot, "TL_Boss_Victory_Director", victoryTimeline);
            CreateVictoryTimeline(victoryTimeline, victoryDirector, playerAnimator);

            GameObject gameplayHud = FindByName(scene, "GameplayUI_Root");
            Light[] towerLights = FindLightsNear(scene, boss.transform.position, 60f);
            FinalBossVictoryCutscene victory = victoryDirector.gameObject.AddComponent<FinalBossVictoryCutscene>();
            victory.EditorConfigure(victoryDirector, boss, bossHealth, bossAi, mainCamera,
                mainCamera.GetComponent<CameraController>(),
                CreateVictoryCues(victoryCameras, defeatedBossLook, victoryPlayerLook, towerLook),
                victorySubtitle, victoryText, victoryFade, gameplayHud, towerLights);

            foreach (Camera camera in encounterCameras.Concat(victoryCameras))
            {
                camera.gameObject.SetActive(false);
            }

            EditorUtility.SetDirty(encounterTimeline);
            EditorUtility.SetDirty(victoryTimeline);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FinalBossCutsceneBuilder] Đã tạo TL_Boss_Encounter (23s) + TL_Boss_Victory (21s) ngay tại vị trí Final Boss thực ở World_Eden7.", rootObject);
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    static void CreateEncounterTimeline(TimelineAsset timeline, PlayableDirector director, Animator player, Animator boss)
    {
        AnimationClip walk = LoadClip("Assets/Animations/Motion/Player/Walk.anim");
        AnimationClip idle = LoadClip("Assets/Animations/Motion/Player/Great Sword Idle.anim");
        AnimationClip point = LoadClip("Assets/Animations/Motion/Player/Pointing.anim");
        CreateAnimationTrack(timeline, director, "ANIM Player - Enter Arena", player,
            Clip(walk, 0d, 4.5d, "Walk into the final arena", true),
            Clip(idle, 4.5d, EncounterDuration, "Idle - Face Commander", true));
        CreateAnimationTrack(timeline, director, "ANIM Commander - Reveal", boss,
            Clip(idle, 0d, 17.5d, "Idle - Commander reveal", true),
            Clip(point, 17.5d, 20d, "Threatening gesture", false),
            Clip(idle, 20d, EncounterDuration, "Idle - Battle ready", true));
    }

    static void CreateVictoryTimeline(TimelineAsset timeline, PlayableDirector director, Animator player)
    {
        AnimationClip idle = LoadClip("Assets/Animations/Motion/Player/Great Sword Idle.anim");
        CreateAnimationTrack(timeline, director, "ANIM Player - Victory", player,
            Clip(idle, 0d, VictoryDuration, "Idle - Observe defeated Commander", true));
    }

    static FinalBossEncounterCutscene.SubtitleCue[] CreateEncounterDialogue() => new[]
    {
        Subtitle(12.5d, 14.6d, "COMMANDER", "So... you finally made it."),
        Subtitle(15.0d, 17.3d, "COMMANDER", "This island belongs to us now."),
        Subtitle(18.1d, 20.2d, "PLAYER", "Not anymore.")
    };

    static FinalBossEncounterCutscene.SubtitleCue Subtitle(double start, double end, string speaker, string text) =>
        new() { start = start, end = end, speaker = speaker, text = text };

    static FinalBossEncounterCutscene.CameraCue[] CreateEncounterCues(Camera[] cameras, Transform midpoint, Transform boss) => new[]
    {
        CameraCue(cameras[0], midpoint, 0d, 5d),
        CameraCue(cameras[1], boss, 5d, 9.5d),
        CameraCue(cameras[2], boss, 9.5d, 14.5d),
        CameraCue(cameras[3], midpoint, 14.5d, 17.5d),
        CameraCue(cameras[4], boss, 17.5d, EncounterDuration)
    };

    static FinalBossVictoryCutscene.CameraCue[] CreateVictoryCues(Camera[] cameras, Transform boss, Transform player, Transform tower) => new[]
    {
        VictoryCue(cameras[0], boss, 0d, 5d),
        VictoryCue(cameras[1], player, 5d, 9d),
        VictoryCue(cameras[2], tower, 9d, 14d),
        VictoryCue(cameras[3], player, 14d, VictoryDuration)
    };

    static FinalBossEncounterCutscene.CameraCue CameraCue(Camera camera, Transform target, double start, double end) => new()
    {
        camera = camera, lookTarget = target, start = start, end = end,
        startPosition = camera.transform.position, endPosition = camera.transform.position + camera.transform.forward * 0.55f
    };

    static FinalBossVictoryCutscene.CameraCue VictoryCue(Camera camera, Transform target, double start, double end) => new()
    {
        camera = camera, lookTarget = target, start = start, end = end,
        startPosition = camera.transform.position, endPosition = camera.transform.position + camera.transform.forward * 0.45f
    };

    static PlayableDirector CreateDirector(Transform parent, string name, TimelineAsset timeline)
    {
        GameObject directorObject = new GameObject(name, typeof(PlayableDirector));
        directorObject.transform.SetParent(parent, false);
        PlayableDirector director = directorObject.GetComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;
        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        return director;
    }

    static TimelineAsset ReplaceTimeline(string path, string assetName)
    {
        if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = assetName;
        AssetDatabase.CreateAsset(timeline, path);
        return timeline;
    }

    static void CreateAnimationTrack(TimelineAsset timeline, PlayableDirector director, string name, Animator animator, params ClipDefinition[] clips)
    {
        AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, name);
        track.trackOffset = TrackOffset.ApplySceneOffsets;
        director.SetGenericBinding(track, animator);
        foreach (ClipDefinition definition in clips)
        {
            if (definition.clip == null || definition.end <= definition.start) continue;
            TimelineClip clip = track.CreateClip<AnimationPlayableAsset>();
            AnimationPlayableAsset playable = (AnimationPlayableAsset)clip.asset;
            playable.clip = definition.clip;
            playable.loop = definition.loop ? AnimationPlayableAsset.LoopMode.On : AnimationPlayableAsset.LoopMode.Off;
            clip.start = definition.start;
            clip.duration = definition.end - definition.start;
            clip.displayName = definition.name;
        }
    }

    static void CreatePresentationCanvas(Transform parent, string name, out CanvasGroup subtitleGroup, out TMP_Text subtitleText, out CanvasGroup fadeGroup)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject fadeObject = CreateUiObject(canvasObject.transform, "Fade", typeof(Image), typeof(CanvasGroup));
        RectTransform fadeRect = fadeObject.GetComponent<RectTransform>();
        Stretch(fadeRect);
        Image fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeGroup = fadeObject.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;

        GameObject subtitleObject = CreateUiObject(canvasObject.transform, "SubtitlePanel", typeof(Image), typeof(CanvasGroup));
        RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.16f, 0.04f);
        subtitleRect.anchorMax = new Vector2(0.84f, 0.18f);
        subtitleRect.offsetMin = subtitleRect.offsetMax = Vector2.zero;
        subtitleObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.52f);
        subtitleGroup = subtitleObject.GetComponent<CanvasGroup>();
        subtitleGroup.alpha = 0f;

        GameObject labelObject = CreateUiObject(subtitleObject.transform, "SubtitleText", typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect, 28f, 18f);
        subtitleText = labelObject.GetComponent<TextMeshProUGUI>();
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.fontSize = 34f;
        subtitleText.color = new Color(1f, 0.93f, 0.78f);
        subtitleText.enableWordWrapping = true;
        subtitleText.text = string.Empty;
    }

    static GameObject CreateUiObject(Transform parent, string name, params Type[] types)
    {
        Type[] components = new Type[types.Length + 2];
        components[0] = typeof(RectTransform);
        components[1] = typeof(CanvasRenderer);
        Array.Copy(types, 0, components, 2, types.Length);
        GameObject result = new GameObject(name, components);
        result.transform.SetParent(parent, false);
        return result;
    }

    static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    static Transform CreateMarker(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.SetPositionAndRotation(position, rotation);
        return marker.transform;
    }

    static Camera CreateCamera(Transform parent, string name, Vector3 position, Transform lookTarget, float fieldOfView)
    {
        GameObject cameraObject = new GameObject(name, typeof(Camera), typeof(AudioListener));
        cameraObject.transform.SetParent(parent, false);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = 0.05f;
        camera.depth = 10f;
        camera.clearFlags = CameraClearFlags.Skybox;
        cameraObject.transform.position = position;
        if (lookTarget != null)
        {
            Vector3 direction = lookTarget.position - position;
            if (direction.sqrMagnitude > 0.001f) cameraObject.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
        return camera;
    }

    static Light[] FindLightsNear(Scene scene, Vector3 point, float distance)
    {
        float sqr = distance * distance;
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light>(true))
            .Where(light => light != null && light.type != LightType.Directional && (light.transform.position - point).sqrMagnitude <= sqr)
            .ToArray();
    }

    static GameObject FindTagged(Scene scene, string tag) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
        .Select(transform => transform.gameObject)
        .FirstOrDefault(go => go.CompareTag(tag));

    static Camera FindMainCamera(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
        .FirstOrDefault(camera => camera.CompareTag("MainCamera"));

    static T FindComponent<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true))
        .FirstOrDefault();

    static GameObject FindByName(Scene scene, string name) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
        .Select(transform => transform.gameObject)
        .FirstOrDefault(go => go.name == name);

    static AnimationClip LoadClip(string path) => AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    readonly struct ClipDefinition
    {
        public readonly AnimationClip clip;
        public readonly double start;
        public readonly double end;
        public readonly string name;
        public readonly bool loop;
        public ClipDefinition(AnimationClip clip, double start, double end, string name, bool loop)
        {
            this.clip = clip; this.start = start; this.end = end; this.name = name; this.loop = loop;
        }
    }

    static ClipDefinition Clip(AnimationClip clip, double start, double end, string name, bool loop) => new(clip, start, end, name, loop);
}
#endif
