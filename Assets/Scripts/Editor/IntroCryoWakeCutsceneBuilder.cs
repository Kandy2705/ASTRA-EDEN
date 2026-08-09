#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

/// <summary>
/// Builds only TL_Intro_CryoWake in CutScene 4. The user-authored environment
/// and the existing scene object named "CryoPod" are treated as read-only.
/// </summary>
public static class IntroCryoWakeCutsceneBuilder
{
    public const string ScenePath = "Assets/Scenes/CutScenes/CutScene 4.unity";
    public const string TimelinePath = "Assets/_Project/Timeline/Cutscenes/TL_Intro_CryoWake.playable";

    private const string RootName = "CS_IntroCryoWake_Root";
    private const string PlayerPrefabPath = "Assets/_Project/Prefab/Player.prefab";
    private const string SurvivorPrefabPath = "Assets/Prefabs/Vroids/NPC/NPC.prefab";
    private const string GlassMaterialPath = "Assets/_Project/Materials/CryoPod/CryoPod_Glass.mat";
    private const string MetalMaterialPath = "Assets/Prefabs/Environment/Hub/Material/M_Metal_Panel.mat";
    private const double Duration = 46d;

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Create Missing TL Intro CryoWake")]
    public static void BuildIfMissing()
    {
        TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        bool configured = File.Exists(Path.GetFullPath(ScenePath)) &&
                          File.ReadAllText(Path.GetFullPath(ScenePath)).Contains($"m_Name: {RootName}");
        if (timeline == null || !configured)
        {
            Build(false);
        }
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Rebuild TL Intro CryoWake")]
    public static void Rebuild()
    {
        Build(true);
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Test TL Intro CryoWake Ending")]
    public static void TestEnding()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[IntroCryoWake] Hãy vào Play Mode trước khi test đoạn kết.");
            return;
        }

        IntroCryoWakeCutsceneController controller =
            UnityEngine.Object.FindFirstObjectByType<IntroCryoWakeCutsceneController>();
        PlayableDirector director = controller != null ? controller.GetComponent<PlayableDirector>() : null;
        if (director == null)
        {
            Debug.LogError("[IntroCryoWake] Không tìm thấy PlayableDirector đang chạy.");
            return;
        }

        director.time = Math.Max(0d, director.duration - 0.2d);
        director.Evaluate();
        director.Play();
        Debug.Log("[IntroCryoWake] Đã tua tới 0.2 giây cuối để test fade/scene transition.");
    }

    private static void Build(bool forceRebuild)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[IntroCryoWake] Hãy chạy builder sau khi Unity compile xong và không ở Play Mode.");
            return;
        }

        if (!File.Exists(Path.GetFullPath(ScenePath)))
        {
            Debug.LogError($"[IntroCryoWake] Không tìm thấy scene: {ScenePath}");
            return;
        }

        EnsureFolder("Assets/_Project/Timeline");
        EnsureFolder("Assets/_Project/Timeline/Cutscenes");

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForBuild = !scene.IsValid() || !scene.isLoaded;
        if (openedForBuild)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject cryoPod = FindInScene(scene, "CryoPod");
            if (cryoPod == null)
            {
                Debug.LogError("[IntroCryoWake] CutScene 4 chưa có object tên 'CryoPod'. Không thay đổi scene.");
                return;
            }

            GameObject existingRoot = FindInScene(scene, RootName);
            if (existingRoot != null)
            {
                if (!forceRebuild && AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
                {
                    return;
                }

                UnityEngine.Object.DestroyImmediate(existingRoot);
            }

            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
            {
                AssetDatabase.DeleteAsset(TimelinePath);
            }

            Bounds worldBounds = CalculateWorldBounds(cryoPod);
            Bounds localBounds = CalculateLocalBounds(cryoPod.transform, cryoPod);
            ResolvePodAxes(cryoPod.transform, localBounds, out Vector3 podForward, out Vector3 podRight,
                out float podLength, out float podWidth);
            Vector3 podCenter = worldBounds.center;
            float groundY = cryoPod.transform.position.y;
            float podHeight = Mathf.Max(1.5f, worldBounds.size.y);

            GameObject root = new(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            Vector3 playerCryoPosition = podCenter - podForward * Mathf.Min(0.7f, podLength * 0.2f);
            playerCryoPosition.y = worldBounds.max.y + 0.04f;
            Quaternion playerCryoRotation = Quaternion.LookRotation(Vector3.up, podForward);

            Vector3 wakePosition = podCenter + podRight * (podWidth * 0.55f + 1.05f) - podForward * 0.2f;
            wakePosition.y = groundY;
            Vector3 controlPosition = podCenter - podRight * (podWidth * 0.55f + 0.75f) - podForward * 0.35f;
            controlPosition.y = groundY;
            Vector3 exitPosition = podCenter - podForward * Mathf.Max(5f, podLength * 1.2f) + podRight * 1.1f;
            exitPosition.y = groundY;
            Vector3 survivorStartPosition = exitPosition - podForward * 2.2f;
            survivorStartPosition.y = groundY;

            Quaternion faceControl = FlatLook(controlPosition - survivorStartPosition, podForward);
            Quaternion faceExit = FlatLook(exitPosition - controlPosition, -podForward);
            Quaternion playerWakeRotation = FlatLook(controlPosition - wakePosition, -podRight);
            Quaternion survivorFacePlayerRotation = FlatLook(wakePosition - controlPosition, podRight);

            Transform playerCryo = CreateMarker(root.transform, "CS_Player_Cryopod", playerCryoPosition, playerCryoRotation);
            Transform playerWake = CreateMarker(root.transform, "CS_Player_WakePosition", wakePosition, playerWakeRotation);
            Transform survivorStart = CreateMarker(root.transform, "CS_Survivor_Start", survivorStartPosition, faceControl);
            Transform survivorControl = CreateMarker(root.transform, "CS_Survivor_ControlPanel", controlPosition, faceControl);
            Transform survivorExit = CreateMarker(root.transform, "CS_Survivor_Exit", exitPosition, faceExit);
            Transform exitMarker = CreateMarker(root.transform, "CS_Exit", exitPosition + podForward * 0.5f, faceExit);

            GameObject player = InstantiateAsset(PlayerPrefabPath, scene, "Player - Intro CryoWake");
            GameObject survivor = InstantiateAsset(SurvivorPrefabPath, scene, "Survivor - Intro CryoWake");
            if (player == null || survivor == null)
            {
                Debug.LogError("[IntroCryoWake] Không thể tạo Player hoặc Survivor.");
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }

            ParentActor(player, playerCryo);
            ParentActor(survivor, survivorStart);
            PrepareCutsceneActor(player);
            PrepareCutsceneActor(survivor);

            Transform podLook = CreateMarker(root.transform, "CS_Look_CryoPod", podCenter + Vector3.up * 0.25f, Quaternion.identity);
            Transform controlLook = CreateMarker(root.transform, "CS_Look_Control", controlPosition + Vector3.up * 1.15f, Quaternion.identity);
            Transform wakeLook = CreateMarker(root.transform, "CS_Look_PlayerWake", wakePosition + Vector3.up * 1.25f, Quaternion.identity);
            Transform dialogueLook = CreateMarker(root.transform, "CS_Look_Dialogue",
                Vector3.Lerp(wakePosition, controlPosition, 0.48f) + Vector3.up * 1.35f, Quaternion.identity);

            Camera[] cameras = new Camera[4];
            cameras[0] = CreateCamera(root.transform, "CS_Camera_Cryo_01",
                podCenter - podForward * (podLength * 0.75f + 1.3f) + podRight * (podWidth + 1.3f) + Vector3.up * (podHeight + 0.9f), podLook, 50f);
            cameras[1] = CreateCamera(root.transform, "CS_Camera_Cryo_02",
                controlPosition - podRight * 2.1f - podForward * 1.1f + Vector3.up * 1.75f, controlLook, 44f);
            cameras[2] = CreateCamera(root.transform, "CS_Camera_Cryo_03",
                wakePosition + podRight * 1.8f - podForward * 1.6f + Vector3.up * 1.55f, wakeLook, 42f);
            cameras[3] = CreateCamera(root.transform, "CS_Camera_Cryo_04",
                Vector3.Lerp(wakePosition, controlPosition, 0.5f) + podForward * 3.2f + podRight * 2f + Vector3.up * 2f, dialogueLook, 48f);

            Transform doorPivot = CreateCryoDoor(root.transform, cryoPod.transform, worldBounds, podForward, podRight, podLength, podWidth);
            CreateControlPanel(root.transform, controlPosition, podForward, podRight);
            Light interiorLight = CreateLight(root.transform, "CS_CryoInteriorLight",
                podCenter + Vector3.up * (podHeight * 0.35f), new Color(0.18f, 0.82f, 1f), 2.2f, Mathf.Max(4f, podWidth * 2f));
            Light statusLight = CreateLight(root.transform, "CS_CryoStatusLight",
                controlPosition + Vector3.up * 1.25f, new Color(0.18f, 0.82f, 1f), 2.8f, 3f);

            CreatePresentationCanvas(root.transform, out CanvasGroup titleGroup, out TMP_Text titleText,
                out CanvasGroup subtitleGroup, out TMP_Text subtitleText, out CanvasGroup fadeGroup);

            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "TL_Intro_CryoWake";
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            GameObject directorObject = new("TL_Intro_CryoWake_Director", typeof(PlayableDirector));
            directorObject.transform.SetParent(root.transform, false);
            PlayableDirector director = directorObject.GetComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = true;
            director.extrapolationMode = DirectorWrapMode.None;
            // A cutscene must continue even if a gameplay/pause component in
            // the authored laboratory environment has left Time.timeScale at 0.
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;

            AudioSource ambienceSource = directorObject.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            AudioSource sfxSource = directorObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            AnimationClip idle = LoadAnimation("Assets/Animations/Motion/Player/Idle.anim");
            AnimationClip standUp = LoadAnimation("Assets/Animations/Motion/Player/Stand Up.anim");
            AnimationClip walk = LoadAnimation("Assets/Animations/Motion/Player/Walk.anim");
            AnimationClip button = LoadAnimation("Assets/Animations/Motion/Player/Button Pushing.anim");
            AnimationClip talk = LoadAnimation("Assets/Animations/Motion/Player/Talking.anim");

            CreateAnimationTrack(timeline, director, "ANIM Player - Cryo Wake", FindAnimator(player),
                Clip(idle, 0d, 23.5d, "Idle - Unconscious In Cryopod", true),
                Clip(standUp, 23.5d, 31.77d, "Stand Up - Wake", false),
                Clip(idle, 31.77d, Duration, "Idle - Weak And Confused", true));

            CreateAnimationTrack(timeline, director, "ANIM Survivor - Rescue", FindAnimator(survivor),
                Clip(idle, 0d, 11d, "Idle - Off Camera", true),
                Clip(walk, 11d, 18d, "Walk - Enter Room", true),
                Clip(idle, 18d, 19d, "Idle - At Controls", true),
                Clip(button, 19d, 22.233d, "Press Button - Disable Cryopod", false),
                Clip(idle, 22.233d, 25.8d, "Idle - Observe Player", true),
                Clip(talk, 25.8d, 27.2d, "Talk - Keep Quiet", true),
                Clip(idle, 27.2d, 30.6d, "Idle - Listen", true),
                Clip(talk, 30.6d, 31.8d, "Talk - Ten Years", true),
                Clip(idle, 31.8d, 35.5d, "Idle - Difficult Pause", true),
                Clip(talk, 35.5d, 41.1d, "Talk - Astra Eden Reveal", true),
                Clip(walk, 41.1d, Duration, "Walk - Escape", true));

            CreateCameraActivationTrack(timeline, director, cameras[0].gameObject, "CAM 01 - Cryopod Reveal", 0d, 11d);
            CreateCameraActivationTrack(timeline, director, cameras[1].gameObject, "CAM 02 - Survivor And Controls", 11d, 23d);
            CreateCameraActivationTrack(timeline, director, cameras[2].gameObject, "CAM 03 - Player Wakes", 23d, 34d);
            CreateCameraActivationTrack(timeline, director, cameras[3].gameObject, "CAM 04 - Truth And Escape", 34d, Duration);

            IntroCryoWakeCutsceneController controller = directorObject.AddComponent<IntroCryoWakeCutsceneController>();
            controller.EditorConfigure(
                director,
                titleGroup,
                titleText,
                subtitleGroup,
                subtitleText,
                fadeGroup,
                doorPivot,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                new Vector3(-72f, 0f, 0f),
                interiorLight,
                statusLight,
                ambienceSource,
                sfxSource,
                CreateSubtitles(),
                CreateCameraCues(cameras, podLook, controlLook, wakeLook, dialogueLook, podForward, podRight),
                new[]
                {
                    ActorCue(playerCryo, 23.5d, 31.77d, playerCryoPosition, wakePosition, false, true,
                        playerCryoRotation.eulerAngles, playerWakeRotation.eulerAngles),
                    ActorCue(survivorStart, 11d, 18d, survivorStartPosition, controlPosition, true, false,
                        faceControl.eulerAngles, faceControl.eulerAngles),
                    ActorCue(survivorStart, 22.233d, 24d, controlPosition, controlPosition, false, true,
                        faceControl.eulerAngles, survivorFacePlayerRotation.eulerAngles),
                    ActorCue(survivorStart, 41.1d, Duration, controlPosition, exitMarker.position, true, false,
                        faceExit.eulerAngles, faceExit.eulerAngles)
                },
                new[]
                {
                    AudioCue("Machinery ambience (optional - assign looping clip)", 0d, null, 0.55f),
                    AudioCue("Electrical shutdown SFX (optional)", 19.8d, null, 0.9f),
                    AudioCue("Distant alarm SFX (optional)", 41d, null, 0.75f)
                },
                "World_Eden7");

            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].gameObject.SetActive(i == 0);
            }

            AddSceneToBuildSettings(ScenePath);
            EditorUtility.SetDirty(timeline);
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IntroCryoWake] Đã tạo TL_Intro_CryoWake 46 giây trong CutScene 4 bằng CryoPod hiện có; bind Player + Survivor + 4 camera và transition World_Eden7.");
        }
        finally
        {
            if (openedForBuild && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static IntroCryoWakeCutsceneController.SubtitleCue[] CreateSubtitles()
    {
        return new[]
        {
            Subtitle(15.5d, 16.9d, "SURVIVOR", "There you are..."),
            Subtitle(24.5d, 25.8d, "PLAYER", "Where am I...?"),
            Subtitle(25.8d, 27.2d, "SURVIVOR", "Keep your voice down."),
            Subtitle(27.5d, 29.2d, "PLAYER", "How long was I in there?"),
            Subtitle(30.6d, 31.8d, "SURVIVOR", "Ten years."),
            Subtitle(32.1d, 33.4d, "PLAYER", "...Ten years?"),
            Subtitle(34d, 35.2d, "PLAYER", "Astra Eden?"),
            Subtitle(35.5d, 36.7d, "SURVIVOR", "They took it."),
            Subtitle(37d, 38.4d, "SURVIVOR", "We tried to fight them."),
            Subtitle(38.6d, 40d, "SURVIVOR", "But too many people fell."),
            Subtitle(40d, 41.3d, "SURVIVOR", "They captured the dinosaurs and stripped the island bare."),
            Subtitle(41.4d, 42.6d, "SURVIVOR", "We don't have time."),
            Subtitle(42.8d, 45.2d, "SURVIVOR", "If you want our home back... first we need to get out of here.")
        };
    }

    private static IntroCryoWakeCutsceneController.SubtitleCue Subtitle(double start, double end, string speaker, string text)
    {
        return new IntroCryoWakeCutsceneController.SubtitleCue { start = start, end = end, speaker = speaker, text = text };
    }

    private static IntroCryoWakeCutsceneController.AudioCue AudioCue(string label, double start, AudioClip clip, float volume)
    {
        return new IntroCryoWakeCutsceneController.AudioCue { label = label, start = start, clip = clip, volume = volume };
    }

    private static IntroCryoWakeCutsceneController.CameraCue[] CreateCameraCues(
        Camera[] cameras, Transform podLook, Transform controlLook, Transform wakeLook, Transform dialogueLook,
        Vector3 podForward, Vector3 podRight)
    {
        return new[]
        {
            CameraCue(cameras[0], podLook, 0d, 11d, cameras[0].transform.position,
                cameras[0].transform.position + podForward * 1.25f - Vector3.up * 0.25f),
            CameraCue(cameras[1], controlLook, 11d, 23d, cameras[1].transform.position,
                cameras[1].transform.position + podRight * 0.8f),
            CameraCue(cameras[2], wakeLook, 23d, 34d, cameras[2].transform.position,
                cameras[2].transform.position - podRight * 0.55f + Vector3.up * 0.12f),
            CameraCue(cameras[3], dialogueLook, 34d, Duration, cameras[3].transform.position,
                cameras[3].transform.position - podForward * 0.8f)
        };
    }

    private static IntroCryoWakeCutsceneController.CameraCue CameraCue(
        Camera camera, Transform target, double start, double end, Vector3 from, Vector3 to)
    {
        return new IntroCryoWakeCutsceneController.CameraCue
        {
            cameraTransform = camera.transform,
            lookTarget = target,
            start = start,
            end = end,
            startPosition = from,
            endPosition = to,
            useManualRotation = false,
            startEulerAngles = camera.transform.eulerAngles,
            endEulerAngles = camera.transform.eulerAngles
        };
    }

    private static IntroCryoWakeCutsceneController.ActorMotionCue ActorCue(
        Transform marker, double start, double end, Vector3 from, Vector3 to,
        bool faceDirection, bool manualRotation, Vector3 startEuler, Vector3 endEuler)
    {
        return new IntroCryoWakeCutsceneController.ActorMotionCue
        {
            actorMarker = marker,
            start = start,
            end = end,
            startPosition = from,
            endPosition = to,
            faceTravelDirection = faceDirection,
            useManualRotation = manualRotation,
            startEulerAngles = startEuler,
            endEulerAngles = endEuler
        };
    }

    private readonly struct ClipDefinition
    {
        public readonly AnimationClip clip;
        public readonly double start;
        public readonly double end;
        public readonly string displayName;
        public readonly bool loop;

        public ClipDefinition(AnimationClip clip, double start, double end, string displayName, bool loop)
        {
            this.clip = clip;
            this.start = start;
            this.end = end;
            this.displayName = displayName;
            this.loop = loop;
        }
    }

    private static ClipDefinition Clip(AnimationClip clip, double start, double end, string displayName, bool loop)
    {
        return new ClipDefinition(clip, start, end, displayName, loop);
    }

    private static void CreateAnimationTrack(TimelineAsset timeline, PlayableDirector director, string name,
        Animator animator, params ClipDefinition[] definitions)
    {
        AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, name);
        track.trackOffset = TrackOffset.ApplySceneOffsets;
        if (animator != null)
        {
            director.SetGenericBinding(track, animator);
        }
        else
        {
            Debug.LogWarning($"[IntroCryoWake] Track '{name}' chưa có Animator binding.");
        }

        foreach (ClipDefinition definition in definitions)
        {
            if (definition.clip == null || definition.end <= definition.start)
            {
                continue;
            }

            TimelineClip clip = track.CreateClip<AnimationPlayableAsset>();
            AnimationPlayableAsset playable = (AnimationPlayableAsset)clip.asset;
            playable.clip = definition.clip;
            playable.loop = definition.loop ? AnimationPlayableAsset.LoopMode.On : AnimationPlayableAsset.LoopMode.Off;
            clip.start = definition.start;
            clip.duration = definition.end - definition.start;
            clip.displayName = definition.displayName;
        }
    }

    private static void CreateCameraActivationTrack(TimelineAsset timeline, PlayableDirector director,
        GameObject cameraObject, string name, double start, double end)
    {
        ActivationTrack track = timeline.CreateTrack<ActivationTrack>(null, name);
        track.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
        TimelineClip clip = track.CreateDefaultClip();
        clip.start = start;
        clip.duration = end - start;
        clip.displayName = name;
        director.SetGenericBinding(track, cameraObject);
    }

    private static Transform CreateCryoDoor(Transform parent, Transform cryoPod, Bounds bounds,
        Vector3 forward, Vector3 right, float length, float width)
    {
        Vector3 pivotPosition = bounds.center + forward * (length * 0.45f);
        pivotPosition.y = bounds.max.y + 0.12f;
        Transform pivot = CreateMarker(parent, "CS_CryoDoorPivot", pivotPosition, Quaternion.LookRotation(forward, Vector3.up));

        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "CS_CryoDoor_Glass";
        door.transform.SetParent(pivot, false);
        door.transform.localPosition = new Vector3(0f, 0f, -length * 0.45f);
        door.transform.localScale = new Vector3(Mathf.Max(0.8f, width * 0.82f), 0.045f, Mathf.Max(1.2f, length * 0.88f));
        UnityEngine.Object.DestroyImmediate(door.GetComponent<Collider>());
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        if (glass != null)
        {
            door.GetComponent<Renderer>().sharedMaterial = glass;
        }

        return pivot;
    }

    private static void CreateControlPanel(Transform parent, Vector3 position, Vector3 forward, Vector3 right)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "CS_CryoControlPanel";
        panel.transform.SetParent(parent, false);
        panel.transform.SetPositionAndRotation(position + Vector3.up * 0.85f,
            Quaternion.LookRotation(right, Vector3.up) * Quaternion.Euler(-12f, 0f, 0f));
        panel.transform.localScale = new Vector3(0.65f, 0.85f, 0.28f);
        Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
        if (metal != null)
        {
            panel.GetComponent<Renderer>().sharedMaterial = metal;
        }

        UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());

        GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        screen.name = "CS_CryoControlScreen";
        screen.transform.SetParent(panel.transform, false);
        screen.transform.localPosition = new Vector3(0f, 0.15f, 0.56f);
        screen.transform.localScale = new Vector3(0.72f, 0.42f, 0.035f);
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        if (glass != null)
        {
            screen.GetComponent<Renderer>().sharedMaterial = glass;
        }

        UnityEngine.Object.DestroyImmediate(screen.GetComponent<Collider>());
    }

    private static Light CreateLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
    {
        GameObject lightObject = new(name, typeof(Light));
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = position;
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    private static void CreatePresentationCanvas(Transform parent, out CanvasGroup titleGroup, out TMP_Text titleText,
        out CanvasGroup subtitleGroup, out TMP_Text subtitleText, out CanvasGroup fadeGroup)
    {
        GameObject canvasObject = new("CS_IntroCryoWake_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject titleObject = new("TenYearsLater", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(CanvasGroup));
        titleObject.transform.SetParent(canvasObject.transform, false);
        Stretch(titleObject.GetComponent<RectTransform>());
        TextMeshProUGUI title = titleObject.GetComponent<TextMeshProUGUI>();
        title.font = TMP_Settings.defaultFontAsset;
        title.fontSize = 64f;
        title.fontStyle = FontStyles.SmallCaps;
        title.characterSpacing = 8f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(1f, 0.93f, 0.73f);
        title.raycastTarget = false;
        titleText = title;
        titleGroup = titleObject.GetComponent<CanvasGroup>();
        titleGroup.alpha = 0f;

        GameObject subtitlePanel = new("SubtitlePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        subtitlePanel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = subtitlePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.035f);
        panelRect.anchorMax = new Vector2(0.92f, 0.20f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        subtitlePanel.GetComponent<Image>().color = new Color(0.01f, 0.018f, 0.028f, 0.82f);
        subtitlePanel.GetComponent<Image>().raycastTarget = false;
        subtitleGroup = subtitlePanel.GetComponent<CanvasGroup>();
        subtitleGroup.alpha = 0f;

        GameObject subtitleObject = new("SubtitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        subtitleObject.transform.SetParent(subtitlePanel.transform, false);
        RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
        subtitleRect.anchorMin = Vector2.zero;
        subtitleRect.anchorMax = Vector2.one;
        subtitleRect.offsetMin = new Vector2(35f, 12f);
        subtitleRect.offsetMax = new Vector2(-35f, -12f);
        TextMeshProUGUI subtitle = subtitleObject.GetComponent<TextMeshProUGUI>();
        subtitle.font = TMP_Settings.defaultFontAsset;
        subtitle.fontSize = 30f;
        subtitle.color = new Color(1f, 0.96f, 0.84f);
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.textWrappingMode = TextWrappingModes.Normal;
        subtitle.richText = true;
        subtitle.raycastTarget = false;
        subtitleText = subtitle;

        GameObject fadeObject = new("FadeToBlack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        fadeObject.transform.SetParent(canvasObject.transform, false);
        fadeObject.transform.SetAsFirstSibling();
        Stretch(fadeObject.GetComponent<RectTransform>());
        fadeObject.GetComponent<Image>().color = Color.black;
        fadeObject.GetComponent<Image>().raycastTarget = false;
        fadeGroup = fadeObject.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 1f;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void ResolvePodAxes(Transform pod, Bounds localBounds, out Vector3 forward, out Vector3 right,
        out float length, out float width)
    {
        bool xIsLong = localBounds.size.x >= localBounds.size.z;
        Vector3 localLong = xIsLong ? Vector3.right : Vector3.forward;
        Vector3 localShort = xIsLong ? Vector3.forward : Vector3.right;
        forward = Vector3.ProjectOnPlane(pod.TransformDirection(localLong), Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        right = Vector3.ProjectOnPlane(pod.TransformDirection(localShort), Vector3.up).normalized;
        if (right.sqrMagnitude < 0.001f || Mathf.Abs(Vector3.Dot(right, forward)) > 0.85f)
        {
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }

        length = pod.TransformVector(localLong * (xIsLong ? localBounds.size.x : localBounds.size.z)).magnitude;
        width = pod.TransformVector(localShort * (xIsLong ? localBounds.size.z : localBounds.size.x)).magnitude;
        length = Mathf.Max(2.4f, length);
        width = Mathf.Max(1.2f, width);
    }

    private static Bounds CalculateWorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position + Vector3.up, new Vector3(2f, 2f, 3.5f));
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static Bounds CalculateLocalBounds(Transform root, GameObject objectRoot)
    {
        Renderer[] renderers = objectRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.up, new Vector3(2f, 2f, 3.5f));
        }

        bool initialized = false;
        Bounds local = default;
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            Vector3 min = world.min;
            Vector3 max = world.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 corner = new(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                Vector3 point = root.InverseTransformPoint(corner);
                if (!initialized)
                {
                    local = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    local.Encapsulate(point);
                }
            }
        }

        return local;
    }

    private static Quaternion FlatLook(Vector3 direction, Vector3 fallback)
    {
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = fallback;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static AnimationClip LoadAnimation(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            Debug.LogError($"[IntroCryoWake] Không tìm thấy animation: {path}");
        }

        return clip;
    }

    private static Animator FindAnimator(GameObject actor)
    {
        return actor != null ? actor.GetComponentInChildren<Animator>(true) : null;
    }

    private static GameObject InstantiateAsset(string path, Scene scene, string name)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogError($"[IntroCryoWake] Không tìm thấy asset: {path}");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(asset, scene) as GameObject;
        if (instance != null)
        {
            instance.name = name;
        }

        return instance;
    }

    private static void ParentActor(GameObject actor, Transform marker)
    {
        actor.transform.SetParent(marker, false);
        actor.transform.localPosition = Vector3.zero;
        actor.transform.localRotation = Quaternion.identity;
        actor.transform.localScale = Vector3.one;
    }

    private static void PrepareCutsceneActor(GameObject actor)
    {
        foreach (MonoBehaviour behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))
        {
            // Timeline owns these presentation-only actor instances. Animator,
            // renderers and rig components are not MonoBehaviours and stay active.
            behaviour.enabled = false;
        }

        foreach (AudioSource source in actor.GetComponentsInChildren<AudioSource>(true))
        {
            source.playOnAwake = false;
        }

        foreach (Canvas canvas in actor.GetComponentsInChildren<Canvas>(true))
        {
            canvas.gameObject.SetActive(false);
        }

        CharacterController characterController = actor.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }
    }

    private static Transform CreateMarker(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        GameObject marker = new(name);
        marker.transform.SetParent(parent, false);
        marker.transform.SetPositionAndRotation(position, rotation);
        return marker.transform;
    }

    private static Camera CreateCamera(Transform parent, string name, Vector3 position, Transform lookTarget, float fov)
    {
        GameObject cameraObject = new(name, typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = position;
        cameraObject.transform.rotation = FlatLook3D(lookTarget.position - position);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.fieldOfView = fov;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 1800f;
        return camera;
    }

    private static Quaternion FlatLook3D(Vector3 direction)
    {
        return direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }

            Transform found = FindChildRecursive(root.transform, name);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void AddSceneToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == path)
            {
                if (!scene.enabled)
                {
                    scene.enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                }
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
