#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterData))]
public sealed class CharacterDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CharacterData character = (CharacterData)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Saved Purchase State", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            string state = !EditorApplication.isPlaying || GameDataManager.Instance == null
                ? "Available in Play Mode (stored per save)"
                : character.IsOwned ? "OWNED" : "NOT OWNED";
            EditorGUILayout.TextField("Current Save", state);
        }

        EditorGUILayout.HelpBox(
            "OWNED is saved by HeroId in GameDataManager. It is intentionally not written into this shared CharacterData asset.",
            MessageType.Info);
    }
}
#endif
