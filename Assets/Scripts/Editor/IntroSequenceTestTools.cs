#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

public static class IntroSequenceTestTools
{
    private const string LoadingScenePath = "Assets/Scenes/Loading.unity";

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Reset Opening Intro Flag (Keep Save)")]
    public static void ResetIntroFlag()
    {
        IntroSequenceFlow.ResetIntroForDemo();
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Open Loading Scene For Intro Test")]
    public static void OpenLoadingScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[IntroSequence] Hãy thoát Play Mode trước khi mở Loading test.");
            return;
        }

        EditorSceneManager.OpenScene(LoadingScenePath);
        Debug.Log("[IntroSequence] Đã mở Loading. Bấm Play để test boot → Main Menu.");
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Start Opening Intro Test Now")]
    public static void StartIntroNow()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[IntroSequence] Chỉ chạy lệnh này trong Play Mode.");
            return;
        }

        IntroSequenceFlow.ResetIntroForDemo();
        IntroSequenceFlow.BeginIntroSequence();
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Skip Current Opening Cutscene")]
    public static void SkipCurrentIntro()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[IntroSequence] Chỉ skip trong Play Mode.");
            return;
        }

        PlayableDirector director = Object.FindFirstObjectByType<PlayableDirector>();
        if (director == null || double.IsInfinity(director.duration) || director.duration <= 0d)
        {
            Debug.LogWarning("[IntroSequence] Scene hiện tại không có Timeline hữu hạn để skip.");
            return;
        }

        director.time = Mathf.Max(0f, (float)director.duration - 0.08f);
        director.Evaluate();
        director.Play();
        Debug.Log("[IntroSequence] Đã tua tới đoạn cuối Timeline hiện tại.");
    }

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Skip Whole Intro To Gameplay")]
    public static void SkipWholeIntro()
    {
        if (!EditorApplication.isPlaying || !IntroSequenceFlow.IsSequenceActive)
        {
            Debug.LogWarning("[IntroSequence] Chuỗi intro chưa chạy trong Play Mode.");
            return;
        }

        IntroSequenceFlow.SkipIntroToGameplay();
    }
}
#endif
