using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_SceneAudioCatalog", menuName = "ASTRA EDEN/Audio/Scene Audio Catalog")]
public class SceneAudioCatalog : ScriptableObject
{
    [SerializeField] private SceneAudioProfile loadingProfile;
    [SerializeField] private List<SceneAudioProfile> sceneProfiles = new List<SceneAudioProfile>();

    public SceneAudioProfile LoadingProfile => loadingProfile;

    public SceneAudioProfile GetProfile(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return null;
        }

        for (int i = 0; i < sceneProfiles.Count; i++)
        {
            SceneAudioProfile profile = sceneProfiles[i];
            if (profile != null && profile.sceneName == sceneName)
            {
                return profile;
            }
        }

        return null;
    }

    public IReadOnlyList<SceneAudioProfile> SceneProfiles => sceneProfiles;
}