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
/// Builds only the second opening cutscene, TL_Intro_Discovery, inside CutScene 2.
/// TL_Intro_Village and CutScene 1 are intentionally outside this builder's scope.
/// </summary>
[InitializeOnLoad]
public static class IntroDiscoveryCutsceneBuilder
{
    public const string ScenePath = "Assets/Scenes/CutScenes/CutScene 2.unity";
    public const string TimelinePath = "Assets/_Project/Timeline/Cutscenes/TL_Intro_Discovery.playable";

    private const string RootName = "CS_IntroDiscovery_Root";
    private const string PlayerPrefabPath = "Assets/_Project/Prefab/Player.prefab";
    private const string GuardModelPath = "Assets/Prefabs/Vroids/Guard/Guard.fbx";
    private const double Duration = 48d;

    static IntroDiscoveryCutsceneBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Create Missing TL Intro Discovery")]
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

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Rebuild TL Intro Discovery")]
    public static void Rebuild()
    {
        Build(true);
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Sync TL Intro Discovery To Current Layout")]
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
            IntroDiscoveryCutsceneController controller = root != null
                ? root.GetComponentInChildren<IntroDiscoveryCutsceneController>(true)
                : null;
            if (root == null || controller == null)
            {
                Debug.LogError("[IntroDiscovery] Không tìm thấy root/controller để đồng bộ layout.");
                return;
            }

            Transform arrival = FindChildRecursive(root.transform, "CS_Player_Arrival");
            Transform approach = FindChildRecursive(root.transform, "CS_Player_Approach");
            Transform listening = FindChildRecursive(root.transform, "CS_Player_Listening");
            Transform runEnd = FindChildRecursive(root.transform, "CS_Player_RunEnd");

            SerializedObject serializedController = new(controller);
            SerializedProperty actorArray = serializedController.FindProperty("actorMotions");
            Vector3[] starts = { arrival.position, approach.position, listening.position };
            Vector3[] ends = { approach.position, listening.position, runEnd.position };
            for (int i = 0; i < actorArray.arraySize && i < starts.Length; i++)
            {
                SerializedProperty cue = actorArray.GetArrayElementAtIndex(i);
                cue.FindPropertyRelative("startPosition").vector3Value = starts[i];
                cue.FindPropertyRelative("endPosition").vector3Value = ends[i];
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
            Debug.Log("[IntroDiscovery] Đã đồng bộ cue theo layout hiện tại của CutScene 2.");
        }
        finally
        {
            if (openedForSync && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Snap TL Intro Discovery To Terrain")]
    public static void SnapToTerrain()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForSnap = !scene.IsValid() || !scene.isLoaded;
        if (openedForSnap)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject root = FindInScene(scene, RootName);
            Terrain terrain = FindInScene(scene, "Terrain Map")?.GetComponent<Terrain>();
            IntroDiscoveryCutsceneController controller = root != null
                ? root.GetComponentInChildren<IntroDiscoveryCutsceneController>(true)
                : null;
            if (root == null || terrain == null || controller == null)
            {
                Debug.LogError("[IntroDiscovery] Thiếu Root, Terrain Map hoặc Controller để snap lại độ cao.");
                return;
            }

            Transform arrival = FindChildRecursive(root.transform, "CS_Player_Arrival");
            Transform approach = FindChildRecursive(root.transform, "CS_Player_Approach");
            Transform listening = FindChildRecursive(root.transform, "CS_Player_Listening");
            Transform runEnd = FindChildRecursive(root.transform, "CS_Player_RunEnd");
            Vector3 stableArrivalPosition = arrival != null ? arrival.position : Vector3.zero;

            // CS_Player_Arrival is also the moving parent marker. Timeline preview
            // can leave that Transform at Listening/RunEnd, so reconstruct its XZ
            // from the stable Zone 1/PoiDoor layout before sampling the new terrain.
            GameObject zone = FindInScene(scene, "Zone 1");
            GameObject poiDoorA = FindInScene(scene, "PoiDoor");
            GameObject poiDoorB = FindInScene(scene, "PoiDoor (1)");
            if (arrival != null && zone != null && poiDoorA != null && poiDoorB != null)
            {
                Vector3 gateCenter = (poiDoorA.transform.position + poiDoorB.transform.position) * 0.5f;
                Vector3 outward = Vector3.ProjectOnPlane(gateCenter - zone.transform.position, Vector3.up).normalized;
                Vector3 tangent = Vector3.ProjectOnPlane(poiDoorA.transform.position - poiDoorB.transform.position, Vector3.up).normalized;
                if (outward.sqrMagnitude < 0.01f)
                {
                    outward = Vector3.forward;
                }

                if (tangent.sqrMagnitude < 0.01f)
                {
                    tangent = Vector3.Cross(Vector3.up, outward).normalized;
                }

                Vector3 arrivalPosition = gateCenter + outward * 30f + tangent * 2f;
                arrivalPosition.y = arrival.position.y;
                stableArrivalPosition = arrivalPosition;
                arrival.position = arrivalPosition;
            }

            SnapWorldPositionToTerrain(ref stableArrivalPosition, terrain, 0.05f);
            if (arrival != null)
            {
                arrival.position = stableArrivalPosition;
            }

            Transform[] playerMarkers = { approach, listening, runEnd };
            for (int i = 0; i < playerMarkers.Length; i++)
            {
                SnapTransformToTerrain(playerMarkers[i], terrain, 0.05f);
            }

            if (arrival != null && approach != null)
            {
                arrival.rotation = LookAtFlat(arrival.position, approach.position, arrival.forward);
            }

            string[] cameraNames =
            {
                "CS_Camera_Discovery_01",
                "CS_Camera_Discovery_02",
                "CS_Camera_Discovery_03",
                "CS_Camera_Discovery_04",
                "CS_Camera_Discovery_05"
            };
            float[] cameraClearances = { 12f, 3f, 3.2f, 2f, 4.2f };
            SerializedObject serializedController = new(controller);
            SerializedProperty cameraArray = serializedController.FindProperty("cameraCues");
            Dictionary<Transform, Vector3> firstCameraCueStarts = new();
            for (int i = 0; i < cameraArray.arraySize; i++)
            {
                SerializedProperty cue = cameraArray.GetArrayElementAtIndex(i);
                Transform cameraTransform = cue.FindPropertyRelative("cameraTransform").objectReferenceValue as Transform;
                if (cameraTransform == null || firstCameraCueStarts.ContainsKey(cameraTransform))
                {
                    continue;
                }

                Vector3 cueStart = cue.FindPropertyRelative("startPosition").vector3Value;
                firstCameraCueStarts.Add(cameraTransform, cueStart);
                cameraTransform.position = cueStart;
            }

            Dictionary<Transform, Vector3> cameraShifts = new();
            for (int i = 0; i < cameraNames.Length; i++)
            {
                Transform cameraTransform = FindChildRecursive(root.transform, cameraNames[i]);
                Vector3 previousPosition = cameraTransform != null ? cameraTransform.position : Vector3.zero;
                SnapTransformToTerrain(cameraTransform, terrain, cameraClearances[i]);
                if (cameraTransform != null)
                {
                    cameraShifts[cameraTransform] = cameraTransform.position - previousPosition;
                }
            }

            Transform baseLook = FindChildRecursive(root.transform, "CS_BaseLook");
            SnapTransformToTerrain(baseLook, terrain, 5f);

            serializedController.FindProperty("actorTerrain").objectReferenceValue = terrain;
            serializedController.FindProperty("actorGroundOffset").floatValue = 0.05f;

            SerializedProperty actorArray = serializedController.FindProperty("actorMotions");
            Vector3[] starts = { stableArrivalPosition, approach.position, listening.position };
            Vector3[] ends = { approach.position, listening.position, runEnd.position };
            for (int i = 0; i < actorArray.arraySize && i < starts.Length; i++)
            {
                SerializedProperty cue = actorArray.GetArrayElementAtIndex(i);
                cue.FindPropertyRelative("startPosition").vector3Value = starts[i];
                cue.FindPropertyRelative("endPosition").vector3Value = ends[i];
            }

            for (int i = 0; i < cameraArray.arraySize; i++)
            {
                SerializedProperty cue = cameraArray.GetArrayElementAtIndex(i);
                Transform cameraTransform = cue.FindPropertyRelative("cameraTransform").objectReferenceValue as Transform;
                if (cameraTransform == null || !cameraShifts.TryGetValue(cameraTransform, out Vector3 shift))
                {
                    continue;
                }

                SerializedProperty start = cue.FindPropertyRelative("startPosition");
                SerializedProperty end = cue.FindPropertyRelative("endPosition");
                start.vector3Value += shift;
                end.vector3Value += shift;
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
            ReorientDiscoveryCameras(root.transform);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IntroDiscovery] Đã đưa Player/camera lên Terrain Map mới và đồng bộ lại toàn bộ cue.");
        }
        finally
        {
            if (openedForSnap && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void Build(bool forceRebuild)
    {
        if (!File.Exists(Path.GetFullPath(ScenePath)))
        {
            Debug.LogError($"[IntroDiscovery] Không tìm thấy scene: {ScenePath}");
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
            if (zone == null || poiDoorA == null || poiDoorB == null)
            {
                Debug.LogError("[IntroDiscovery] CutScene 2 phải có Zone 1, PoiDoor và PoiDoor (1).");
                return;
            }

            GameObject existingRoot = FindInScene(scene, RootName);
            GameObject player = FindPlayerPrefabInstance(scene);
            if (existingRoot != null)
            {
                Transform nestedPlayer = FindPlayerPrefabInside(existingRoot.transform);
                if (nestedPlayer != null)
                {
                    GameObject playerRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(nestedPlayer.gameObject);
                    playerRoot.transform.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(playerRoot, scene);
                    player = playerRoot;
                }

                UnityEngine.Object.DestroyImmediate(existingRoot);
            }

            if (player == null)
            {
                player = InstantiateAsset(PlayerPrefabPath, scene, "Player");
            }

            if (player == null)
            {
                Debug.LogError($"[IntroDiscovery] Không thể tạo Player từ {PlayerPrefabPath}.");
                return;
            }

            if (forceRebuild || AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
            {
                AssetDatabase.DeleteAsset(TimelinePath);
            }

            GameObject root = new(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            Terrain terrain = FindInScene(scene, "Terrain Map")?.GetComponent<Terrain>();
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
            Vector3 arrivalPosition = Grounded(scene, gateCenter + outward * 30f + tangent * 2f, fallbackY);
            Vector3 approachPosition = Grounded(scene, gateCenter + outward * 18f + tangent * 1f, fallbackY);
            Vector3 listeningPosition = Grounded(scene, gateCenter + outward * 9f - tangent * 5.5f, fallbackY);
            Vector3 runEndPosition = Grounded(scene, gateCenter + outward * 31f - tangent * 8f, fallbackY);

            Quaternion faceBase = Quaternion.LookRotation(-outward, Vector3.up);
            Transform playerArrival = CreateMarker(root.transform, "CS_Player_Arrival", arrivalPosition, faceBase);
            Transform playerApproach = CreateMarker(root.transform, "CS_Player_Approach", approachPosition, faceBase);
            Transform playerListening = CreateMarker(root.transform, "CS_Player_Listening", listeningPosition, faceBase);
            Transform playerRunEnd = CreateMarker(root.transform, "CS_Player_RunEnd", runEndPosition, Quaternion.LookRotation(outward, Vector3.up));
            ParentActor(player, playerArrival);

            GameObject guardA = InstantiateAsset(GuardModelPath, scene, "Guard A");
            GameObject guardB = InstantiateAsset(GuardModelPath, scene, "Guard B");
            if (guardA == null || guardB == null)
            {
                Debug.LogError($"[IntroDiscovery] Không thể tạo đủ hai guard từ {GuardModelPath}.");
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }

            Vector3 guardAPosition = poiDoorA.transform.position;
            Vector3 guardBPosition = poiDoorB.transform.position;
            Quaternion guardARotation = LookAtFlat(guardAPosition, guardBPosition, -outward);
            Quaternion guardBRotation = LookAtFlat(guardBPosition, guardAPosition, -outward);
            Transform guardAMarker = CreateMarker(root.transform, "CS_GuardA", guardAPosition, guardARotation);
            Transform guardBMarker = CreateMarker(root.transform, "CS_GuardB", guardBPosition, guardBRotation);
            ParentActor(guardA, guardAMarker);
            ParentActor(guardB, guardBMarker);

            Transform baseLook = CreateMarker(root.transform, "CS_BaseLook", gateCenter - outward * 14f + Vector3.up * 5f, Quaternion.identity);
            Transform guardLook = CreateMarker(root.transform, "CS_GuardConversationLook", gateCenter + Vector3.up * 1.55f, Quaternion.identity);
            Transform playerLook = CreateMarker(playerArrival, "CS_PlayerLook", arrivalPosition + Vector3.up * 1.55f, Quaternion.identity);

            Camera[] cameras = new Camera[5];
            cameras[0] = CreateCamera(root.transform, "CS_Camera_Discovery_01", gateCenter + outward * 39f + tangent * 18f + Vector3.up * 12f, baseLook, 54f);
            cameras[1] = CreateCamera(root.transform, "CS_Camera_Discovery_02", approachPosition + outward * 3f - tangent * 4f + Vector3.up * 2.8f, baseLook, 50f);
            cameras[2] = CreateCamera(root.transform, "CS_Camera_Discovery_03", gateCenter + outward * 7f + tangent * 8f + Vector3.up * 3.2f, guardLook, 46f);
            cameras[3] = CreateCamera(root.transform, "CS_Camera_Discovery_04", listeningPosition - outward * 2.5f - tangent * 1.5f + Vector3.up * 1.9f, playerLook, 42f);
            cameras[4] = CreateCamera(root.transform, "CS_Camera_Discovery_05", listeningPosition - tangent * 7f + Vector3.up * 4.2f, playerLook, 50f);

            CanvasGroup subtitleGroup;
            TMP_Text subtitleText;
            CanvasGroup fadeGroup;
            CreatePresentationCanvas(root.transform, out subtitleGroup, out subtitleText, out fadeGroup);

            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "TL_Intro_Discovery";
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            GameObject directorObject = new("TL_Intro_Discovery_Director", typeof(PlayableDirector), typeof(AudioSource));
            directorObject.transform.SetParent(root.transform, false);
            PlayableDirector director = directorObject.GetComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = true;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            AudioSource audioSource = directorObject.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            AnimationClip idle = LoadAnimation("Assets/Animations/Motion/Player/Idle.anim");
            AnimationClip walk = LoadAnimation("Assets/Animations/Motion/Player/Walk.anim");
            AnimationClip run = LoadAnimation("Assets/Animations/Motion/Player/Fast Run.anim");
            AnimationClip talk = LoadAnimation("Assets/Animations/Motion/Player/Talking.anim");

            CreateAnimationTrack(timeline, director, "ANIM Player", FindAnimator(player),
                new ClipDefinition(idle, 0d, 7d, "Idle - Arrival"),
                new ClipDefinition(walk, 7d, 15d, "Walk - Peaceful Approach"),
                new ClipDefinition(idle, 15d, 43d, "Idle - Listening and Reaction"),
                new ClipDefinition(run, 43d, Duration, "Run - Escape Toward Coast"));

            CreateGuardAnimationTrack(timeline, director, "ANIM Guard A", FindAnimator(guardA), idle, talk,
                new TimeRange(15.0d, 16.8d),
                new TimeRange(18.3d, 20.3d),
                new TimeRange(24.1d, 25.3d),
                new TimeRange(25.5d, 27.3d),
                new TimeRange(29.1d, 31.1d),
                new TimeRange(31.5d, 33.5d),
                new TimeRange(35.1d, 38.3d));

            CreateGuardAnimationTrack(timeline, director, "ANIM Guard B", FindAnimator(guardB), idle, talk,
                new TimeRange(16.9d, 18.0d),
                new TimeRange(20.5d, 22.0d),
                new TimeRange(22.3d, 23.9d),
                new TimeRange(27.5d, 29.0d),
                new TimeRange(33.7d, 35.0d),
                new TimeRange(46.0d, 47.0d));

            CreateCameraActivationTrack(timeline, director, cameras[0].gameObject, "CAM 01 - Unknown Island", new TimeRange(0d, 7d));
            CreateCameraActivationTrack(timeline, director, cameras[1].gameObject, "CAM 02 - Peaceful Approach", new TimeRange(7d, 15d));
            CreateCameraActivationTrack(timeline, director, cameras[2].gameObject, "CAM 03 - Guard Conversation",
                new TimeRange(15d, 24.1d), new TimeRange(25.3d, 38.5d));
            CreateCameraActivationTrack(timeline, director, cameras[3].gameObject, "CAM 04 - Player Realization",
                new TimeRange(24.1d, 25.3d), new TimeRange(38.5d, 43d));
            CreateCameraActivationTrack(timeline, director, cameras[4].gameObject, "CAM 05 - Run Away", new TimeRange(43d, Duration));

            Vector3 cam1Start = cameras[0].transform.position;
            Vector3 cam2Start = cameras[1].transform.position;
            Vector3 cam3Start = cameras[2].transform.position;
            Vector3 cam4Start = cameras[3].transform.position;
            Vector3 cam5Start = cameras[4].transform.position;

            IntroDiscoveryCutsceneController controller = directorObject.AddComponent<IntroDiscoveryCutsceneController>();
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
                    CameraCue(cameras[0], baseLook, 0d, 7d, cam1Start, cam1Start - outward * 5f - tangent * 2f),
                    CameraCue(cameras[1], baseLook, 7d, 15d, cam2Start, cam2Start - outward * 7f),
                    CameraCue(cameras[2], guardLook, 15d, 24.1d, cam3Start, cam3Start - tangent * 1.2f),
                    CameraCue(cameras[3], playerLook, 24.1d, 25.3d, cam4Start, cam4Start + outward * 0.55f),
                    CameraCue(cameras[2], guardLook, 25.3d, 38.5d, cam3Start - tangent * 1.2f, cam3Start + tangent * 1.1f),
                    CameraCue(cameras[3], playerLook, 38.5d, 43d, cam4Start, cam4Start + outward * 1.1f),
                    CameraCue(cameras[4], playerLook, 43d, Duration, cam5Start, cam5Start + outward * 10f)
                },
                new[]
                {
                    ActorCue(playerArrival, 7d, 10.5d, arrivalPosition, approachPosition),
                    ActorCue(playerArrival, 10.5d, 15d, approachPosition, listeningPosition),
                    ActorCue(playerArrival, 43d, Duration, listeningPosition, runEndPosition)
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
            Debug.Log("[IntroDiscovery] Đã tạo TL_Intro_Discovery 48 giây gần Zone 1, bind Player + Guard A + Guard B + 5 camera.");
        }
        finally
        {
            if (openedForBuild && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static IntroDiscoveryCutsceneController.SubtitleCue[] CreateSubtitles()
    {
        return new[]
        {
            Subtitle(4.7d, 6.5d, "PLAYER", "What is that...?"),
            Subtitle(15.0d, 16.8d, "GUARD A", "The extraction team arrives next week."),
            Subtitle(16.9d, 18.0d, "GUARD B", "Already?"),
            Subtitle(18.3d, 20.3d, "GUARD A", "The Commander doesn't want to wait any longer."),
            Subtitle(20.5d, 22.0d, "GUARD B", "What about the island to the east?"),
            Subtitle(22.3d, 23.9d, "GUARD B", "The one with all those dinosaurs?"),
            Subtitle(24.1d, 25.3d, "GUARD A", "Astra Eden."),
            Subtitle(25.5d, 27.3d, "GUARD A", "The order is to take the entire island."),
            Subtitle(27.5d, 29.0d, "GUARD B", "And the people living there?"),
            Subtitle(29.1d, 31.1d, "GUARD A", "If they cooperate, relocate them."),
            Subtitle(31.5d, 33.5d, "GUARD A", "If they resist... deal with them."),
            Subtitle(33.7d, 35.0d, "GUARD B", "And the dinosaurs?"),
            Subtitle(35.1d, 36.6d, "GUARD A", "Capture as many as possible."),
            Subtitle(36.7d, 38.3d, "GUARD A", "Research wants live specimens."),
            Subtitle(39.0d, 40.3d, "PLAYER", "Astra Eden..."),
            Subtitle(40.7d, 42.7d, "PLAYER", "They're going after my home."),
            Subtitle(46.0d, 47.0d, "GUARD B", "Hey!")
        };
    }

    private static IntroDiscoveryCutsceneController.SubtitleCue Subtitle(double start, double end, string speaker, string text)
    {
        return new IntroDiscoveryCutsceneController.SubtitleCue { start = start, end = end, speaker = speaker, text = text };
    }

    private static IntroDiscoveryCutsceneController.CameraCue CameraCue(Camera camera, Transform target, double start, double end, Vector3 from, Vector3 to)
    {
        Vector3 initialEulerAngles = camera.transform.eulerAngles;
        return new IntroDiscoveryCutsceneController.CameraCue
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

    private static IntroDiscoveryCutsceneController.ActorMotionCue ActorCue(Transform marker, double start, double end, Vector3 from, Vector3 to)
    {
        return new IntroDiscoveryCutsceneController.ActorMotionCue
        {
            actorMarker = marker,
            start = start,
            end = end,
            startPosition = from,
            endPosition = to,
            faceTravelDirection = true
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

        public ClipDefinition(AnimationClip clip, double start, double end, string displayName)
        {
            this.clip = clip;
            this.start = start;
            this.end = end;
            this.displayName = displayName;
        }
    }

    private static void CreateGuardAnimationTrack(
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
                clips.Add(new ClipDefinition(idle, cursor, range.start, "Idle"));
            }

            clips.Add(new ClipDefinition(talk, range.start, range.end, "Talk"));
            cursor = range.end;
        }

        if (cursor < Duration)
        {
            clips.Add(new ClipDefinition(idle, cursor, Duration, "Idle"));
        }

        CreateAnimationTrack(timeline, director, trackName, animator, clips.ToArray());
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
            Debug.LogWarning($"[IntroDiscovery] Track '{trackName}' chưa tìm thấy Animator để bind.");
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            ClipDefinition definition = definitions[i];
            if (definition.clip == null || definition.end <= definition.start)
            {
                Debug.LogWarning($"[IntroDiscovery] Bỏ qua clip thiếu/không hợp lệ '{definition.displayName}' trên '{trackName}'.");
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

    private static void CreateCameraActivationTrack(TimelineAsset timeline, PlayableDirector director, GameObject cameraObject, string trackName, params TimeRange[] ranges)
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
            Debug.LogError($"[IntroDiscovery] Không tìm thấy animation: {path}");
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
            Debug.LogError($"[IntroDiscovery] Không tìm thấy asset: {assetPath}");
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
        cameraObject.transform.rotation = LookAtFlatAndVertical(position, lookTarget.position);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1500f;
        return camera;
    }

    private static Quaternion LookAtFlat(Vector3 from, Vector3 to, Vector3 fallback)
    {
        Vector3 direction = Vector3.ProjectOnPlane(to - from, Vector3.up);
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = fallback;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static Quaternion LookAtFlatAndVertical(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        return direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;
    }

    private static Vector3 Grounded(Scene scene, Vector3 position, float fallbackY)
    {
        Terrain terrain = FindInScene(scene, "Terrain Map")?.GetComponent<Terrain>();
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

        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        return position;
    }

    private static void SnapTransformToTerrain(Transform target, Terrain terrain, float clearance)
    {
        if (target == null || terrain == null || terrain.terrainData == null)
        {
            return;
        }

        Vector3 position = target.position;
        Vector3 local = position - terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
        {
            return;
        }

        position.y = terrain.SampleHeight(position) + terrain.transform.position.y + clearance;
        target.position = position;
    }

    private static void SnapWorldPositionToTerrain(ref Vector3 position, Terrain terrain, float clearance)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return;
        }

        Vector3 local = position - terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
        {
            return;
        }

        position.y = terrain.SampleHeight(position) + terrain.transform.position.y + clearance;
    }

    private static void ReorientDiscoveryCameras(Transform root)
    {
        string[] cameraNames =
        {
            "CS_Camera_Discovery_01",
            "CS_Camera_Discovery_02",
            "CS_Camera_Discovery_03",
            "CS_Camera_Discovery_04",
            "CS_Camera_Discovery_05"
        };
        string[] lookNames =
        {
            "CS_BaseLook",
            "CS_BaseLook",
            "CS_GuardConversationLook",
            "CS_PlayerLook",
            "CS_PlayerLook"
        };

        for (int i = 0; i < cameraNames.Length; i++)
        {
            Transform cameraTransform = FindChildRecursive(root, cameraNames[i]);
            Transform lookTarget = FindChildRecursive(root, lookNames[i]);
            if (cameraTransform != null && lookTarget != null)
            {
                cameraTransform.rotation = LookAtFlatAndVertical(cameraTransform.position, lookTarget.position);
            }
        }
    }

    private static void CreatePresentationCanvas(Transform parent, out CanvasGroup subtitleGroup, out TMP_Text subtitleText, out CanvasGroup fadeGroup)
    {
        GameObject canvasObject = new("CS_IntroDiscovery_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
        panelImage.color = new Color(0.015f, 0.01f, 0.025f, 0.76f);
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

    private static GameObject FindPlayerPrefabInstance(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform player = FindPlayerPrefabInside(root.transform);
            if (player != null)
            {
                return PrefabUtility.GetOutermostPrefabInstanceRoot(player.gameObject);
            }
        }

        return null;
    }

    private static Transform FindPlayerPrefabInside(Transform parent)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
            if (source != null && AssetDatabase.GetAssetPath(source) == PlayerPrefabPath)
            {
                return child;
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
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
