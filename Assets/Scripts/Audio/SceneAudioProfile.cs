using UnityEngine;

[CreateAssetMenu(fileName = "SO_SceneAudio_", menuName = "ASTRA EDEN/Audio/Scene Audio Profile")]
public class SceneAudioProfile : ScriptableObject
{
    [Header("Scene")]
    public string sceneName;

    [Header("Music")]
    public AudioClip music;
    public bool loopMusic = true;
    [Range(0f, 1f)] public float musicVolume = 1f;

    [Header("Ambient")]
    public AudioClip ambient;
    public bool loopAmbient = true;
    [Range(0f, 1f)] public float ambientVolume = 0.55f;

    [Header("Crossfade")]
    [Min(0f)] public float enterCrossfadeDuration = 2f;
    [Min(0f)] public float exitCrossfadeDuration = 1.5f;
}