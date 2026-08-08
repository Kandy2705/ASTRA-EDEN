#if UNITY_EDITOR
using System;
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
/// Dựng một lần cutscene mở đầu ASTRA EDEN từ asset sẵn có trong project.
/// Menu Rebuild chỉ thay CS_IntroVillage_Root và TL_Intro_Village, không chạm CutScene 2.
/// </summary>
[InitializeOnLoad]
public static class IntroVillageCutsceneBuilder
{
    public const string ScenePath = "Assets/Scenes/CutScenes/CutScene 1.unity";
    public const string TimelinePath = "Assets/_Project/Timeline/Cutscenes/TL_Intro_Village.playable";

    private const string RootName = "CS_IntroVillage_Root";
    private const string PlayerPrefabPath = "Assets/_Project/Prefab/Player.prefab";
    private const string VillageLeaderPath = "Assets/Prefabs/Vroids/Village Leader/Village_Leader.prefab";
    private const string Villager01Path = "Assets/Prefabs/Vroids/NPC/Villager_01.fbx";
    private const string Villager02Path = "Assets/Prefabs/Vroids/NPC/NPC.prefab";
    private const string RaptorPrefabPath = "Assets/Packages/PBRVelociraptor/Prefabs/Mobile/5K/Raptor_Animated_FBX_5K_Orange.prefab";
    private const string RaptorModelPath = "Assets/Packages/PBRVelociraptor/Models/5K/Raptor_Animated_FBX_5K.fbx";

    static IntroVillageCutsceneBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Create Missing TL Intro Village")]
    public static void BuildIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += BuildIfMissing;
            return;
        }

        TimelineAsset existingTimeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        string absoluteScenePath = Path.GetFullPath(ScenePath);
        bool sceneAlreadyConfigured = File.Exists(absoluteScenePath) &&
                                      File.ReadAllText(absoluteScenePath).Contains($"m_Name: {RootName}");
        if (existingTimeline != null && sceneAlreadyConfigured)
        {
            return;
        }

        Build(false);
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Rebuild TL Intro Village")]
    public static void Rebuild()
    {
        Build(true);
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Sync TL Intro Village To Current Layout")]
    public static void SyncToCurrentVillageLayout()
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
            IntroVillageCutsceneController controller = root != null
                ? root.GetComponentInChildren<IntroVillageCutsceneController>(true)
                : null;
            if (root == null || controller == null)
            {
                Debug.LogError("[IntroVillage] Không tìm thấy CS_IntroVillage_Root/Controller để đồng bộ layout.");
                return;
            }

            SerializedObject serializedController = new(controller);
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

            Transform playerEnd = FindInScene(scene, "CS_Player_End")?.transform;
            SerializedProperty actorArray = serializedController.FindProperty("actorMotions");
            for (int i = 0; i < actorArray.arraySize; i++)
            {
                SerializedProperty cue = actorArray.GetArrayElementAtIndex(i);
                Transform actorMarker = cue.FindPropertyRelative("actorMarker").objectReferenceValue as Transform;
                if (actorMarker == null)
                {
                    continue;
                }

                SerializedProperty start = cue.FindPropertyRelative("startPosition");
                SerializedProperty end = cue.FindPropertyRelative("endPosition");
                Vector3 travel = end.vector3Value - start.vector3Value;
                start.vector3Value = actorMarker.position;
                end.vector3Value = i == 0 && playerEnd != null
                    ? playerEnd.position
                    : actorMarker.position + travel;
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IntroVillage] Đã đồng bộ camera/actor cues theo vị trí làng hiện tại trong CutScene 1.");
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
            Debug.LogError($"[IntroVillage] Không tìm thấy scene: {ScenePath}");
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
            GameObject existingRoot = FindInScene(scene, RootName);
            GameObject player = FindInScene(scene, "Player");

            if (existingRoot != null)
            {
                Transform existingPlayer = FindChildRecursive(existingRoot.transform, "Player");
                if (existingPlayer != null && IsPlayerPrefab(existingPlayer.gameObject))
                {
                    existingPlayer.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(existingPlayer.gameObject, scene);
                    player = existingPlayer.gameObject;
                }

                UnityEngine.Object.DestroyImmediate(existingRoot);
            }

            if (player == null || !IsPlayerPrefab(player))
            {
                player = FindPlayerPrefabInstance(scene);
            }

            if (player == null)
            {
                Debug.LogError($"[IntroVillage] Scene {ScenePath} chưa có Player prefab '{PlayerPrefabPath}'.");
                return;
            }

            if (forceRebuild || AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
            {
                AssetDatabase.DeleteAsset(TimelinePath);
            }

            DisableLegacyTimelineAndGameplayCamera(scene);

            GameObject root = new(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Vector3 origin = player.transform.position;

            Transform playerMarker = CreateMarker(root.transform, "CS_Player", origin, Quaternion.identity);
            Transform playerEnd = CreateMarker(root.transform, "CS_Player_End", origin + new Vector3(0f, 0f, 8f), Quaternion.identity);
            ParentActor(player, playerMarker);

            GameObject leader = InstantiatePrefab(VillageLeaderPath, scene, "Village Leader");
            GameObject villager01 = InstantiatePrefab(Villager01Path, scene, "Villager 01");
            GameObject villager02 = InstantiatePrefab(Villager02Path, scene, "Villager 02");
            GameObject dino01 = InstantiatePrefab(RaptorPrefabPath, scene, "Dino 01 - Orange Raptor");
            GameObject dino02 = InstantiatePrefab(RaptorPrefabPath, scene, "Dino 02 - Orange Raptor");

            if (leader == null || villager01 == null || villager02 == null || dino01 == null || dino02 == null)
            {
                Debug.LogError("[IntroVillage] Thiếu một hoặc nhiều prefab nhân vật. Xem log đường dẫn ngay phía trên.");
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }

            Transform leaderMarker = CreateMarker(root.transform, "CS_VillageLeader", origin + new Vector3(0f, 0f, 3.5f), Quaternion.Euler(0f, 180f, 0f));
            Transform villager01Marker = CreateMarker(root.transform, "CS_Villager_01", origin + new Vector3(-3.5f, 0f, 1.5f), Quaternion.Euler(0f, 35f, 0f));
            Transform villager02Marker = CreateMarker(root.transform, "CS_Villager_02", origin + new Vector3(4f, 0f, 1f), Quaternion.Euler(0f, -35f, 0f));
            Transform dino01Marker = CreateMarker(root.transform, "CS_Dino_01", origin + new Vector3(-7f, 0f, 5f), Quaternion.Euler(0f, 55f, 0f));
            Transform dino02Marker = CreateMarker(root.transform, "CS_Dino_02", origin + new Vector3(7f, 0f, 3f), Quaternion.Euler(0f, -55f, 0f));

            ParentActor(leader, leaderMarker);
            ParentActor(villager01, villager01Marker);
            ParentActor(villager02, villager02Marker);
            ParentActor(dino01, dino01Marker);
            ParentActor(dino02, dino02Marker);

            Transform cameraLook01 = CreateMarker(root.transform, "CS_CameraLook_01", origin + new Vector3(0f, 1.4f, 1.5f), Quaternion.identity);
            Transform cameraLook02 = CreateMarker(root.transform, "CS_CameraLook_02", origin + new Vector3(0f, 1.3f, 2.5f), Quaternion.identity);
            Transform cameraLook03 = CreateMarker(root.transform, "CS_CameraLook_03", playerEnd.position + new Vector3(0f, 1.6f, 2f), Quaternion.identity);
            Transform cameraLook04 = CreateMarker(root.transform, "CS_CameraLook_04", origin + new Vector3(0f, 1.5f, 2f), Quaternion.identity);

            Camera[] cameras = new Camera[4];
            cameras[0] = CreateCamera(root.transform, "CS_Camera_01", origin + new Vector3(14f, 7f, -14f), cameraLook01);
            cameras[1] = CreateCamera(root.transform, "CS_Camera_02", origin + new Vector3(-7f, 3.5f, -7f), cameraLook02);
            cameras[2] = CreateCamera(root.transform, "CS_Camera_03", origin + new Vector3(7f, 3.2f, -6f), cameraLook03);
            cameras[3] = CreateCamera(root.transform, "CS_Camera_04", origin + new Vector3(4f, 2.5f, -4f), cameraLook04);

            CanvasGroup subtitleGroup;
            TMP_Text subtitleText;
            CanvasGroup fadeGroup;
            CreatePresentationCanvas(root.transform, out subtitleGroup, out subtitleText, out fadeGroup);

            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "TL_Intro_Village";
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            GameObject directorObject = new("TL_Intro_Village_Director", typeof(PlayableDirector));
            directorObject.transform.SetParent(root.transform, false);
            PlayableDirector director = directorObject.GetComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = true;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;

            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Motion/Player/Idle.anim");
            AnimationClip walk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Motion/Player/Walk.anim");
            AnimationClip talking = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Motion/Player/Talking.anim");
            AnimationClip raptorIdle01 = FindRaptorClip("Raptor_Idle1_Anim");
            AnimationClip raptorIdle02 = FindRaptorClip("Raptor_Idle2_Anim") ?? raptorIdle01;
            AnimationClip raptorWalk = FindRaptorClip("Raptor_Walk_Anim") ?? raptorIdle01;

            CreateAnimationTrack(timeline, director, "ANIM Player", FindAnimator(player),
                new ClipDefinition(idle, 0d, 34d, "Idle"),
                new ClipDefinition(walk, 34d, 40d, "Walk to Village Exit"));
            CreateAnimationTrack(timeline, director, "ANIM Village Leader", FindAnimator(leader),
                new ClipDefinition(idle, 0d, 26d, "Idle"),
                new ClipDefinition(talking, 26d, 40d, "Talk to Player"));
            CreateAnimationTrack(timeline, director, "ANIM Villager 01", FindAnimator(villager01),
                new ClipDefinition(idle, 0d, 40d, "Idle"));
            CreateAnimationTrack(timeline, director, "ANIM Villager 02", FindAnimator(villager02),
                new ClipDefinition(walk, 0d, 18d, "Walk Near Dinosaur"),
                new ClipDefinition(idle, 18d, 40d, "Idle"));
            CreateAnimationTrack(timeline, director, "ANIM Dino 01", FindAnimator(dino01),
                new ClipDefinition(raptorIdle01, 0d, 40d, "Raptor Idle 1"));
            CreateAnimationTrack(timeline, director, "ANIM Dino 02", FindAnimator(dino02),
                new ClipDefinition(raptorWalk, 0d, 18d, "Raptor Walk"),
                new ClipDefinition(raptorIdle02, 18d, 40d, "Raptor Idle 2"));

            CreateCameraActivationTrack(timeline, director, cameras[0].gameObject, "CAM Shot 01 - Establishing", 0d, 8d);
            CreateCameraActivationTrack(timeline, director, cameras[1].gameObject, "CAM Shot 02 - Daily Life", 8d, 18d);
            CreateCameraActivationTrack(timeline, director, cameras[2].gameObject, "CAM Shot 03 - Unknown World", 18d, 26d);
            CreateCameraActivationTrack(timeline, director, cameras[3].gameObject, "CAM Shot 04 - Leader and Departure", 26d, 40d);

            IntroVillageCutsceneController controller = directorObject.AddComponent<IntroVillageCutsceneController>();
            controller.EditorConfigure(
                director,
                subtitleGroup,
                subtitleText,
                fadeGroup,
                CreateSubtitles(),
                new[]
                {
                    CameraCue(cameras[0], cameraLook01, 0d, 8d, origin + new Vector3(14f, 7f, -14f), origin + new Vector3(8f, 5f, -10f)),
                    CameraCue(cameras[1], cameraLook02, 8d, 18d, origin + new Vector3(-7f, 3.5f, -7f), origin + new Vector3(-4f, 2.8f, -5f)),
                    CameraCue(cameras[2], cameraLook03, 18d, 26d, origin + new Vector3(7f, 3.2f, -6f), origin + new Vector3(5f, 3.2f, 0f)),
                    CameraCue(cameras[3], cameraLook04, 26d, 40d, origin + new Vector3(4f, 2.5f, -4f), origin + new Vector3(7f, 4f, -9f))
                },
                new[]
                {
                    ActorCue(playerMarker, 34d, 40d, origin, playerEnd.position),
                    ActorCue(villager02Marker, 0d, 18d, origin + new Vector3(4f, 0f, 1f), origin + new Vector3(1.2f, 0f, 5f)),
                    ActorCue(dino02Marker, 0d, 18d, origin + new Vector3(7f, 0f, 3f), origin + new Vector3(5f, 0f, 6f))
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
            Debug.Log($"[IntroVillage] Đã tạo TL_Intro_Village 40 giây và bind 6 actor + 4 camera vào {ScenePath}.");
        }
        finally
        {
            if (openedForBuild && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static IntroVillageCutsceneController.SubtitleCue[] CreateSubtitles()
    {
        return new[]
        {
            Subtitle(0.6d, 7.7d, "", "Astra Eden. A land where humans and dinosaurs have lived together for generations."),
            Subtitle(8.2d, 12.7d, "", "We hunted, explored, and survived together."),
            Subtitle(12.9d, 17.6d, "", "Neither humans nor dinosaurs lived alone."),
            Subtitle(18.2d, 21.8d, "", "But for us, the world ended at the horizon."),
            Subtitle(21.9d, 25.8d, "", "We knew Astra Eden... but little of what existed beyond the sea."),
            Subtitle(26.2d, 28.8d, "VILLAGE LEADER", "Today, it is your turn."),
            Subtitle(28.9d, 31.8d, "VILLAGE LEADER", "Go and see what lies beyond our shores."),
            Subtitle(31.9d, 35.1d, "VILLAGE LEADER", "Find out whether other people live beyond Astra Eden."),
            Subtitle(35.2d, 37.8d, "PLAYER", "I will return.")
        };
    }

    private static IntroVillageCutsceneController.SubtitleCue Subtitle(double start, double end, string speaker, string text)
    {
        return new IntroVillageCutsceneController.SubtitleCue { start = start, end = end, speaker = speaker, text = text };
    }

    private static IntroVillageCutsceneController.CameraCue CameraCue(Camera camera, Transform target, double start, double end, Vector3 from, Vector3 to)
    {
        return new IntroVillageCutsceneController.CameraCue
        {
            cameraTransform = camera.transform,
            lookTarget = target,
            start = start,
            end = end,
            startPosition = from,
            endPosition = to
        };
    }

    private static IntroVillageCutsceneController.ActorMotionCue ActorCue(Transform marker, double start, double end, Vector3 from, Vector3 to)
    {
        return new IntroVillageCutsceneController.ActorMotionCue
        {
            actorMarker = marker,
            start = start,
            end = end,
            startPosition = from,
            endPosition = to,
            faceTravelDirection = true
        };
    }

    private readonly struct ClipDefinition
    {
        public readonly AnimationClip clip;
        public readonly double start;
        public readonly double end;
        public readonly string displayName;

        public ClipDefinition(AnimationClip clip, double start, double end, string displayName)
        {
            this.clip = clip;
            this.start = start;
            this.end = end;
            this.displayName = displayName;
        }
    }

    private static void CreateAnimationTrack(TimelineAsset timeline, PlayableDirector director, string trackName, Animator animator, params ClipDefinition[] definitions)
    {
        AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, trackName);
        track.trackOffset = TrackOffset.ApplySceneOffsets;
        if (animator != null)
        {
            director.SetGenericBinding(track, animator);
        }
        else
        {
            Debug.LogWarning($"[IntroVillage] Track '{trackName}' chưa tìm thấy Animator để bind.");
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            ClipDefinition definition = definitions[i];
            if (definition.clip == null)
            {
                Debug.LogWarning($"[IntroVillage] Thiếu AnimationClip '{definition.displayName}' trên track '{trackName}'.");
                continue;
            }

            TimelineClip clip = track.CreateClip<AnimationPlayableAsset>();
            AnimationPlayableAsset playable = (AnimationPlayableAsset)clip.asset;
            playable.clip = definition.clip;
            playable.loop = AnimationPlayableAsset.LoopMode.On;
            clip.start = definition.start;
            clip.duration = definition.end - definition.start;
            clip.displayName = definition.displayName;
        }
    }

    private static void CreateCameraActivationTrack(TimelineAsset timeline, PlayableDirector director, GameObject cameraObject, string trackName, double start, double end)
    {
        ActivationTrack track = timeline.CreateTrack<ActivationTrack>(null, trackName);
        track.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
        TimelineClip clip = track.CreateDefaultClip();
        clip.start = start;
        clip.duration = end - start;
        clip.displayName = trackName;
        director.SetGenericBinding(track, cameraObject);
    }

    private static AnimationClip FindRaptorClip(string partialName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(RaptorModelPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                                    clip.name.Contains(partialName, StringComparison.OrdinalIgnoreCase));
    }

    private static Animator FindAnimator(GameObject actor)
    {
        return actor != null ? actor.GetComponentInChildren<Animator>(true) : null;
    }

    private static GameObject InstantiatePrefab(string assetPath, Scene scene, string objectName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogError($"[IntroVillage] Không tìm thấy prefab/model: {assetPath}");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
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

    private static Camera CreateCamera(Transform parent, string objectName, Vector3 position, Transform lookTarget)
    {
        GameObject cameraObject = new(objectName, typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = position;
        Vector3 direction = lookTarget.position - position;
        cameraObject.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.fieldOfView = 52f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1200f;
        return camera;
    }

    private static void CreatePresentationCanvas(Transform parent, out CanvasGroup subtitleGroup, out TMP_Text subtitleText, out CanvasGroup fadeGroup)
    {
        GameObject canvasObject = new("CS_IntroVillage_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
        panelImage.color = new Color(0.015f, 0.01f, 0.025f, 0.74f);
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

    private static void DisableLegacyTimelineAndGameplayCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Timeline" || root.name == "Legacy_Timeline_Disabled")
            {
                root.name = "Legacy_Timeline_Disabled";
                root.SetActive(false);
            }

            if (root.name == "Main Camera")
            {
                root.SetActive(false);
            }
        }
    }

    private static bool IsPlayerPrefab(GameObject gameObject)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
        return source != null && AssetDatabase.GetAssetPath(source) == PlayerPrefabPath;
    }

    private static GameObject FindPlayerPrefabInstance(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (IsPlayerPrefab(child.gameObject))
                {
                    return PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject);
                }
            }
        }

        return null;
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
        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif
