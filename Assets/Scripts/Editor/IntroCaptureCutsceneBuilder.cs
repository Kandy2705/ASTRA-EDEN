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
/// Builds only TL_Intro_Capture inside CutScene 3. Discovery/CutScene 2 and
/// later CryoWake content are intentionally outside this builder's scope.
/// </summary>
[InitializeOnLoad]
public static class IntroCaptureCutsceneBuilder
{
    public const string ScenePath = "Assets/Scenes/CutScenes/CutScene 3.unity";
    public const string TimelinePath = "Assets/_Project/Timeline/Cutscenes/TL_Intro_Capture.playable";

    private const string RootName = "CS_IntroCapture_Root";
    private const string PlayerPrefabPath = "Assets/_Project/Prefab/Player.prefab";
    private const string CommanderModelPath = "Assets/Prefabs/Vroids/Boss/3721511935325929846.fbx";
    private const string SoldierModelPath = "Assets/Prefabs/Vroids/Guard/Guard.fbx";
    private const double Duration = 32d;

    static IntroCaptureCutsceneBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Create Missing TL Intro Capture")]
    public static void BuildIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += BuildIfMissing;
            return;
        }

        TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        bool sceneConfigured = File.Exists(Path.GetFullPath(ScenePath)) &&
                               File.ReadAllText(Path.GetFullPath(ScenePath)).Contains($"m_Name: {RootName}");
        if (timeline != null && sceneConfigured)
        {
            return;
        }

        Build(false);
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Rebuild TL Intro Capture")]
    public static void Rebuild()
    {
        Build(true);
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Sync TL Intro Capture To Current Layout")]
    public static void SyncToCurrentLayout()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForSync = !scene.IsValid() || !scene.isLoaded;
        if (openedForSync)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject root = FindInScene(scene, RootName);
            IntroCaptureCutsceneController controller = root != null
                ? root.GetComponentInChildren<IntroCaptureCutsceneController>(true)
                : null;
            if (root == null || controller == null)
            {
                Debug.LogError("[IntroCapture] Không tìm thấy root/controller để đồng bộ layout.");
                return;
            }

            Transform playerRunStart = FindChildRecursive(root.transform, "CS_Player_RunStart");
            Transform playerEncounter = FindChildRecursive(root.transform, "CS_Player_CommanderEncounter");
            Transform playerFall = FindChildRecursive(root.transform, "CS_Player_Fall");
            Transform soldierStart = FindChildRecursive(root.transform, "CS_Soldier_Start");
            Transform soldierHit = FindChildRecursive(root.transform, "CS_Soldier_HitPosition");
            if (playerRunStart == null || playerEncounter == null || playerFall == null || soldierStart == null || soldierHit == null)
            {
                Debug.LogError("[IntroCapture] Thiếu marker Player hoặc Soldier để đồng bộ.");
                return;
            }

            SerializedObject serializedController = new(controller);
            SerializedProperty actorArray = serializedController.FindProperty("actorMotions");
            if (actorArray.arraySize >= 3)
            {
                SetActorCuePositions(actorArray.GetArrayElementAtIndex(0), playerRunStart.position, playerEncounter.position);
                SetActorCuePositions(actorArray.GetArrayElementAtIndex(1), playerEncounter.position, playerFall.position);
                SetActorCuePositions(actorArray.GetArrayElementAtIndex(2), soldierStart.position, soldierHit.position);
            }

            SerializedProperty cameraArray = serializedController.FindProperty("cameraCues");
            for (int i = 0; i < cameraArray.arraySize; i++)
            {
                SerializedProperty cue = cameraArray.GetArrayElementAtIndex(i);
                Transform cameraTransform = cue.FindPropertyRelative("cameraTransform").objectReferenceValue as Transform;
                if (cameraTransform == null)
                {
                    continue;
                }

                SerializedProperty start = cue.FindPropertyRelative("startPosition");
                SerializedProperty end = cue.FindPropertyRelative("endPosition");
                Vector3 travel = end.vector3Value - start.vector3Value;
                start.vector3Value = cameraTransform.position;
                end.vector3Value = cameraTransform.position + travel;
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IntroCapture] Đã đồng bộ actor/camera cues theo layout hiện tại của CutScene 3.");
        }
        finally
        {
            if (openedForSync && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void Build(bool forceRebuild)
    {
        if (!File.Exists(Path.GetFullPath(ScenePath)))
        {
            Debug.LogError($"[IntroCapture] Không tìm thấy scene: {ScenePath}");
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
            GameObject zone = FindInScene(scene, "Zone 1");
            GameObject poiDoorA = FindInScene(scene, "PoiDoor");
            GameObject poiDoorB = FindInScene(scene, "PoiDoor (1)");
            Terrain terrain = FindInScene(scene, "Terrain Map")?.GetComponent<Terrain>();
            if (zone == null || poiDoorA == null || poiDoorB == null || terrain == null)
            {
                Debug.LogError("[IntroCapture] CutScene 3 cần Zone 1, PoiDoor, PoiDoor (1) và Terrain Map.");
                return;
            }

            GameObject existingRoot = FindInScene(scene, RootName);
            if (existingRoot != null)
            {
                if (!forceRebuild)
                {
                    Debug.LogWarning("[IntroCapture] Root đã tồn tại nhưng Timeline bị thiếu; dựng lại riêng Capture.");
                }

                UnityEngine.Object.DestroyImmediate(existingRoot);
            }

            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
            {
                AssetDatabase.DeleteAsset(TimelinePath);
            }

            GameObject root = new(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            Vector3 gateCenter = (poiDoorA.transform.position + poiDoorB.transform.position) * 0.5f;
            Vector3 outward = Vector3.ProjectOnPlane(gateCenter - zone.transform.position, Vector3.up).normalized;
            if (outward.sqrMagnitude < 0.01f)
            {
                outward = Vector3.forward;
            }

            Vector3 tangent = Vector3.ProjectOnPlane(poiDoorA.transform.position - poiDoorB.transform.position, Vector3.up).normalized;
            if (tangent.sqrMagnitude < 0.01f)
            {
                tangent = Vector3.Cross(Vector3.up, outward).normalized;
            }

            float fallbackY = gateCenter.y;
            Vector3 playerRunStartPosition = Grounded(terrain, gateCenter + outward * 31f - tangent * 8f, fallbackY);
            Vector3 playerEncounterPosition = Grounded(terrain, playerRunStartPosition + outward * 13f - tangent * 2f, fallbackY);
            Vector3 playerFallPosition = Grounded(terrain, playerEncounterPosition + outward * 0.45f, fallbackY);
            Vector3 commanderPosition = Grounded(terrain, playerEncounterPosition + outward * 4.8f, fallbackY);
            Vector3 soldierStartPosition = Grounded(terrain, playerRunStartPosition - outward * 6f - tangent * 1.8f, fallbackY);
            Vector3 soldierHitPosition = Grounded(terrain, playerEncounterPosition - outward * 1.05f, fallbackY);

            Quaternion faceEscape = Quaternion.LookRotation(outward, Vector3.up);
            Quaternion facePlayer = Quaternion.LookRotation(-outward, Vector3.up);
            Transform playerRunStart = CreateMarker(root.transform, "CS_Player_RunStart", playerRunStartPosition, faceEscape);
            Transform playerEncounter = CreateMarker(root.transform, "CS_Player_CommanderEncounter", playerEncounterPosition, faceEscape);
            Transform playerFall = CreateMarker(root.transform, "CS_Player_Fall", playerFallPosition, faceEscape);
            Transform commanderMarker = CreateMarker(root.transform, "CS_Commander_Start", commanderPosition, facePlayer);
            Transform soldierStart = CreateMarker(root.transform, "CS_Soldier_Start", soldierStartPosition, faceEscape);
            Transform soldierHit = CreateMarker(root.transform, "CS_Soldier_HitPosition", soldierHitPosition, faceEscape);

            GameObject player = InstantiateAsset(PlayerPrefabPath, scene, "Player - Intro Capture");
            GameObject commander = InstantiateAsset(CommanderModelPath, scene, "Final Boss - Commander");
            GameObject soldier = InstantiateAsset(SoldierModelPath, scene, "Soldier - Capture");
            if (player == null || commander == null || soldier == null)
            {
                Debug.LogError("[IntroCapture] Không thể tạo đủ Player, Commander và Soldier.");
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }

            ParentActor(player, playerRunStart);
            ParentActor(commander, commanderMarker);
            ParentActor(soldier, soldierStart);

            Transform playerLook = CreateMarker(playerRunStart, "CS_PlayerLook", playerRunStartPosition + Vector3.up * 1.5f, Quaternion.identity);
            Transform commanderLook = CreateMarker(commanderMarker, "CS_CommanderLook", commanderPosition + Vector3.up * 1.55f, Quaternion.identity);
            Transform confrontationLook = CreateMarker(
                root.transform,
                "CS_ConfrontationLook",
                Vector3.Lerp(playerEncounterPosition, commanderPosition, 0.52f) + Vector3.up * 1.4f,
                Quaternion.identity);
            Transform knockoutLook = CreateMarker(
                root.transform,
                "CS_KnockoutLook",
                Vector3.Lerp(playerFallPosition, commanderPosition, 0.42f) + Vector3.up * 1.05f,
                Quaternion.identity);

            Camera[] cameras = new Camera[4];
            cameras[0] = CreateCamera(root.transform, "CS_Camera_Capture_01",
                playerRunStartPosition - outward * 5.5f + tangent * 4f + Vector3.up * 2.8f, playerLook, 52f);
            cameras[1] = CreateCamera(root.transform, "CS_Camera_Capture_02",
                commanderPosition - outward * 2.2f + tangent * 3.8f + Vector3.up * 1.25f, commanderLook, 42f);
            cameras[2] = CreateCamera(root.transform, "CS_Camera_Capture_03",
                playerEncounterPosition + tangent * 6.2f + Vector3.up * 2.25f, confrontationLook, 47f);
            cameras[3] = CreateCamera(root.transform, "CS_Camera_Capture_04",
                playerFallPosition - outward * 1.35f + tangent * 2.25f + Vector3.up * 0.72f, knockoutLook, 44f);

            CreatePresentationCanvas(root.transform, out CanvasGroup subtitleGroup, out TMP_Text subtitleText, out CanvasGroup fadeGroup);

            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "TL_Intro_Capture";
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            GameObject directorObject = new("TL_Intro_Capture_Director", typeof(PlayableDirector), typeof(AudioSource));
            directorObject.transform.SetParent(root.transform, false);
            PlayableDirector director = directorObject.GetComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = true;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            AudioSource audioSource = directorObject.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            AnimationClip run = LoadAnimation("Assets/Animations/Motion/Player/Fast Run.anim");
            AnimationClip idle = LoadAnimation("Assets/Animations/Motion/Player/Idle.anim");
            AnimationClip combatIdle = LoadAnimation("Assets/Animations/Motion/Player/Great Sword Idle.anim");
            AnimationClip hitReaction = LoadAnimation("Assets/Animations/Motion/Player/Receiving An Uppercut.anim");
            AnimationClip knockout = LoadAnimation("Assets/Animations/Motion/Player/Zombie Dying.anim");
            AnimationClip talk = LoadAnimation("Assets/Animations/Motion/Player/Talking.anim");
            AnimationClip soldierAttack = LoadAnimation("Assets/Animations/Motion/Player/Right Hook.anim");

            CreateAnimationTrack(timeline, director, "ANIM Player", FindAnimator(player),
                Clip(run, 0d, 5d, "Run - Escape", true),
                Clip(idle, 5d, 13.2d, "Idle - Commander Encounter", true),
                Clip(combatIdle, 13.2d, 22.3d, "Combat Idle - Defensive", true),
                Clip(hitReaction, 22.3d, 23.95d, "Hit Reaction - From Behind", false),
                Clip(knockout, 23.95d, Duration, "Fall - Knockout", false));

            CreateDialogueAnimationTrack(timeline, director, "ANIM Commander", FindAnimator(commander), idle, talk,
                new TimeRange(6.3d, 7.8d),
                new TimeRange(8.8d, 10.5d),
                new TimeRange(11.2d, 12.6d),
                new TimeRange(16.0d, 17.1d),
                new TimeRange(18.0d, 19.5d),
                new TimeRange(26.0d, 27.5d),
                new TimeRange(28.0d, 30.5d));

            CreateAnimationTrack(timeline, director, "ANIM Soldier", FindAnimator(soldier),
                Clip(idle, 0d, 18.5d, "Idle - Waiting Behind", true),
                Clip(run, 18.5d, 21.9d, "Run - Approach Player", true),
                Clip(soldierAttack, 21.9d, 23.0d, "Right Hook - Knockout Strike", false),
                Clip(idle, 23.0d, Duration, "Idle - Guard Captive", true));

            CreateCameraActivationTrack(timeline, director, cameras[0].gameObject, "CAM 01 - Escape", new TimeRange(0d, 5d));
            CreateCameraActivationTrack(timeline, director, cameras[1].gameObject, "CAM 02 - Commander Reveal", new TimeRange(5d, 12.8d));
            CreateCameraActivationTrack(timeline, director, cameras[2].gameObject, "CAM 03 - Confrontation", new TimeRange(12.8d, 21.5d));
            CreateCameraActivationTrack(timeline, director, cameras[3].gameObject, "CAM 04 - Knockout POV", new TimeRange(21.5d, Duration));

            Vector3 cam1Start = cameras[0].transform.position;
            Vector3 cam2Start = cameras[1].transform.position;
            Vector3 cam3Start = cameras[2].transform.position;
            Vector3 cam4Start = cameras[3].transform.position;

            IntroCaptureCutsceneController controller = directorObject.AddComponent<IntroCaptureCutsceneController>();
            controller.EditorConfigure(
                director,
                subtitleGroup,
                subtitleText,
                fadeGroup,
                audioSource,
                terrain,
                CreateSubtitles(),
                new[]
                {
                    CameraCue(cameras[0], playerLook, 0d, 5d, cam1Start, cam1Start + outward * 10f),
                    CameraCue(cameras[1], commanderLook, 5d, 12.8d, cam2Start, cam2Start - tangent * 0.8f + Vector3.up * 0.25f),
                    CameraCue(cameras[2], confrontationLook, 12.8d, 21.5d, cam3Start, cam3Start - tangent * 1.2f),
                    CameraCue(cameras[3], knockoutLook, 21.5d, Duration, cam4Start, cam4Start + outward * 0.7f - Vector3.up * 0.18f)
                },
                new[]
                {
                    ActorCue(playerRunStart, 0d, 5d, playerRunStartPosition, playerEncounterPosition, true, true),
                    ActorCue(playerRunStart, 22.3d, 23.95d, playerEncounterPosition, playerFallPosition, false, true),
                    ActorCue(soldierStart, 18.5d, 22.2d, soldierStartPosition, soldierHitPosition, true, true)
                },
                new[]
                {
                    new IntroCaptureCutsceneController.AudioCue
                    {
                        label = "Chase tension sting (optional)",
                        start = 0.2d,
                        clip = null,
                        volume = 0.65f
                    },
                    new IntroCaptureCutsceneController.AudioCue
                    {
                        label = "Commander reveal boom (optional)",
                        start = 5d,
                        clip = null,
                        volume = 0.8f
                    },
                    new IntroCaptureCutsceneController.AudioCue
                    {
                        label = "Knockout impact hit (optional)",
                        start = 22.3d,
                        clip = null,
                        volume = 1f
                    },
                    new IntroCaptureCutsceneController.AudioCue
                    {
                        label = "Ear ringing after impact (optional)",
                        start = 23d,
                        clip = null,
                        volume = 0.7f
                    }
                });

            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].gameObject.SetActive(i == 0);
            }

            EditorUtility.SetDirty(timeline);
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IntroCapture] Đã tạo TL_Intro_Capture 32 giây trong CutScene 3, bind Player + Commander + Soldier + 4 camera.");
        }
        finally
        {
            if (openedForBuild && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static IntroCaptureCutsceneController.SubtitleCue[] CreateSubtitles()
    {
        return new[]
        {
            Subtitle(0.7d, 1.7d, "GUARD (OFF-SCREEN)", "Stop!"),
            Subtitle(6.3d, 7.8d, "COMMANDER", "Who are you?"),
            Subtitle(8.8d, 10.5d, "COMMANDER", "You're not from here."),
            Subtitle(11.2d, 12.6d, "COMMANDER", "Astra Eden..."),
            Subtitle(14.0d, 15.2d, "PLAYER", "Stay back."),
            Subtitle(16.0d, 17.1d, "COMMANDER", "Interesting."),
            Subtitle(18.0d, 19.5d, "COMMANDER", "Take him alive."),
            Subtitle(26.0d, 27.5d, "COMMANDER", "Keep him alive."),
            Subtitle(28.0d, 30.5d, "COMMANDER", "I want to know where he came from.")
        };
    }

    private static IntroCaptureCutsceneController.SubtitleCue Subtitle(double start, double end, string speaker, string text)
    {
        return new IntroCaptureCutsceneController.SubtitleCue
        {
            start = start,
            end = end,
            speaker = speaker,
            text = text
        };
    }

    private static IntroCaptureCutsceneController.CameraCue CameraCue(
        Camera camera,
        Transform target,
        double start,
        double end,
        Vector3 from,
        Vector3 to)
    {
        Vector3 initialEulerAngles = camera.transform.eulerAngles;
        return new IntroCaptureCutsceneController.CameraCue
        {
            cameraTransform = camera.transform,
            lookTarget = target,
            start = start,
            end = end,
            startPosition = from,
            endPosition = to,
            useManualRotation = false,
            startEulerAngles = initialEulerAngles,
            endEulerAngles = initialEulerAngles
        };
    }

    private static IntroCaptureCutsceneController.ActorMotionCue ActorCue(
        Transform marker,
        double start,
        double end,
        Vector3 from,
        Vector3 to,
        bool faceDirection,
        bool followTerrain)
    {
        return new IntroCaptureCutsceneController.ActorMotionCue
        {
            actorMarker = marker,
            start = start,
            end = end,
            startPosition = from,
            endPosition = to,
            faceTravelDirection = faceDirection,
            followTerrain = followTerrain
        };
    }

    private readonly struct TimeRange
    {
        public readonly double start;
        public readonly double end;

        public TimeRange(double start, double end)
        {
            this.start = start;
            this.end = end;
        }
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

    private static void CreateDialogueAnimationTrack(
        TimelineAsset timeline,
        PlayableDirector director,
        string trackName,
        Animator animator,
        AnimationClip idle,
        AnimationClip talk,
        params TimeRange[] talkWindows)
    {
        List<ClipDefinition> clips = new();
        double cursor = 0d;
        for (int i = 0; i < talkWindows.Length; i++)
        {
            TimeRange range = talkWindows[i];
            if (range.start > cursor)
            {
                clips.Add(Clip(idle, cursor, range.start, "Idle", true));
            }

            clips.Add(Clip(talk, range.start, range.end, "Talk", true));
            cursor = range.end;
        }

        if (cursor < Duration)
        {
            clips.Add(Clip(idle, cursor, Duration, "Idle", true));
        }

        CreateAnimationTrack(timeline, director, trackName, animator, clips.ToArray());
    }

    private static void CreateAnimationTrack(
        TimelineAsset timeline,
        PlayableDirector director,
        string trackName,
        Animator animator,
        params ClipDefinition[] definitions)
    {
        AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, trackName);
        track.trackOffset = TrackOffset.ApplySceneOffsets;
        if (animator != null)
        {
            director.SetGenericBinding(track, animator);
        }
        else
        {
            Debug.LogWarning($"[IntroCapture] Track '{trackName}' chưa tìm thấy Animator để bind.");
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            ClipDefinition definition = definitions[i];
            if (definition.clip == null || definition.end <= definition.start)
            {
                Debug.LogWarning($"[IntroCapture] Bỏ qua clip thiếu/không hợp lệ '{definition.displayName}' trên '{trackName}'.");
                continue;
            }

            TimelineClip timelineClip = track.CreateClip<AnimationPlayableAsset>();
            AnimationPlayableAsset playable = (AnimationPlayableAsset)timelineClip.asset;
            playable.clip = definition.clip;
            playable.loop = definition.loop ? AnimationPlayableAsset.LoopMode.On : AnimationPlayableAsset.LoopMode.Off;
            timelineClip.start = definition.start;
            timelineClip.duration = definition.end - definition.start;
            timelineClip.displayName = definition.displayName;
        }
    }

    private static void CreateCameraActivationTrack(
        TimelineAsset timeline,
        PlayableDirector director,
        GameObject cameraObject,
        string trackName,
        params TimeRange[] ranges)
    {
        ActivationTrack track = timeline.CreateTrack<ActivationTrack>(null, trackName);
        track.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
        for (int i = 0; i < ranges.Length; i++)
        {
            TimelineClip clip = track.CreateDefaultClip();
            clip.start = ranges[i].start;
            clip.duration = ranges[i].end - ranges[i].start;
            clip.displayName = trackName;
        }

        director.SetGenericBinding(track, cameraObject);
    }

    private static AnimationClip LoadAnimation(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            Debug.LogError($"[IntroCapture] Không tìm thấy animation: {path}");
        }

        return clip;
    }

    private static Animator FindAnimator(GameObject actor)
    {
        return actor != null ? actor.GetComponentInChildren<Animator>(true) : null;
    }

    private static GameObject InstantiateAsset(string assetPath, Scene scene, string objectName)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            Debug.LogError($"[IntroCapture] Không tìm thấy asset: {assetPath}");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(asset, scene) as GameObject;
        if (instance != null)
        {
            instance.name = objectName;
        }

        return instance;
    }

    private static Transform CreateMarker(Transform parent, string objectName, Vector3 position, Quaternion rotation)
    {
        GameObject marker = new(objectName);
        marker.transform.SetParent(parent, false);
        marker.transform.SetPositionAndRotation(position, rotation);
        return marker.transform;
    }

    private static void ParentActor(GameObject actor, Transform marker)
    {
        actor.transform.SetParent(marker, false);
        actor.transform.localPosition = Vector3.zero;
        actor.transform.localRotation = Quaternion.identity;
        actor.transform.localScale = Vector3.one;
    }

    private static Camera CreateCamera(Transform parent, string objectName, Vector3 position, Transform lookTarget, float fieldOfView)
    {
        GameObject cameraObject = new(objectName, typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = position;
        cameraObject.transform.rotation = LookAt(position, lookTarget.position);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1500f;
        return camera;
    }

    private static Quaternion LookAt(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        return direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;
    }

    private static Vector3 Grounded(Terrain terrain, Vector3 position, float fallbackY)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            position.y = fallbackY;
            return position;
        }

        Vector3 local = position - terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
        {
            position.y = fallbackY;
            return position;
        }

        position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.05f;
        return position;
    }

    private static void SetActorCuePositions(SerializedProperty cue, Vector3 start, Vector3 end)
    {
        cue.FindPropertyRelative("startPosition").vector3Value = start;
        cue.FindPropertyRelative("endPosition").vector3Value = end;
    }

    private static void CreatePresentationCanvas(
        Transform parent,
        out CanvasGroup subtitleGroup,
        out TMP_Text subtitleText,
        out CanvasGroup fadeGroup)
    {
        GameObject canvasObject = new("CS_IntroCapture_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject subtitlePanel = new("SubtitlePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        subtitlePanel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = subtitlePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.035f);
        panelRect.anchorMax = new Vector2(0.92f, 0.20f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        Image panelImage = subtitlePanel.GetComponent<Image>();
        panelImage.color = new Color(0.015f, 0.01f, 0.025f, 0.78f);
        panelImage.raycastTarget = false;
        subtitleGroup = subtitlePanel.GetComponent<CanvasGroup>();
        subtitleGroup.alpha = 0f;

        GameObject textObject = new("SubtitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(subtitlePanel.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(35f, 12f);
        textRect.offsetMax = new Vector2(-35f, -12f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 30f;
        text.color = new Color(1f, 0.96f, 0.84f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.richText = true;
        text.raycastTarget = false;
        subtitleText = text;

        GameObject fadeObject = new("FadeToBlack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        fadeObject.transform.SetParent(canvasObject.transform, false);
        RectTransform fadeRect = fadeObject.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = fadeRect.offsetMax = Vector2.zero;
        Image fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;
        fadeGroup = fadeObject.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 1f;
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }

            Transform child = FindChildRecursive(root.transform, objectName);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string objectName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == objectName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
