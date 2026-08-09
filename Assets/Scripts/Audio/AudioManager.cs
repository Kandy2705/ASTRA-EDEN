using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-250)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const string PrefMaster = "ASTRA_AUDIO_MASTER";
    const string PrefMusic = "ASTRA_AUDIO_MUSIC";
    const string PrefAmbient = "ASTRA_AUDIO_AMBIENT";
    const string PrefSfx = "ASTRA_AUDIO_SFX";
    const string PrefBeach = "ASTRA_AUDIO_BEACH";

    [Header("Catalog")]
    [SerializeField] private SceneAudioCatalog catalog;

    [Header("Defaults")]
    [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float defaultAmbientVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float defaultBeachVolume = 0.85f;

    [Header("Beach Layer")]
    [SerializeField] private AudioClip defaultBeachClip;
    [SerializeField, Min(0f)] private float beachFadeDuration = 2.5f;

    AudioSource musicSourceA;
    AudioSource musicSourceB;
    AudioSource ambientSourceA;
    AudioSource ambientSourceB;
    AudioSource beachSource;
    AudioSource sfxSource;

    AudioSource activeMusicSource;
    AudioSource activeAmbientSource;

    Coroutine musicFadeRoutine;
    Coroutine ambientFadeRoutine;
    Coroutine beachFadeRoutine;

    SceneAudioProfile activeProfile;
    string pendingTargetScene;
    string activeSceneName = string.Empty;
    bool beachLayerActive;
    AudioClip activeBeachClip;
    bool musicOverrideActive;
    AudioClip musicOverrideClip;
    float musicOverrideVolumeScale = 1f;

    float masterVolume = 1f;
    float musicBusVolume = 1f;
    float ambientBusVolume = 1f;
    float sfxBusVolume = 1f;
    float beachBusVolume = 1f;

    public float MasterVolume => masterVolume;
    public float MusicVolume => musicBusVolume;
    public float AmbientVolume => ambientBusVolume;
    public float SfxVolume => sfxBusVolume;
    public float BeachVolume => beachBusVolume;

    public static AudioManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        AudioManager existing = FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.InitializeSingleton();
            return Instance;
        }

        GameObject host = new GameObject("AudioManager");
        return host.AddComponent<AudioManager>();
    }

    void Awake()
    {
        InitializeSingleton();
        EnsureCatalogLoaded();
        LoadVolumeSettings();
        BuildAudioSources();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void EnsureCatalogLoaded()
    {
        if (catalog != null)
        {
            return;
        }

        catalog = Resources.Load<SceneAudioCatalog>("ASTRA/SO_SceneAudioCatalog");
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    void InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void BuildAudioSources()
    {
        musicSourceA = CreateLoopSource("Music_A");
        musicSourceB = CreateLoopSource("Music_B");
        ambientSourceA = CreateLoopSource("Ambient_A");
        ambientSourceB = CreateLoopSource("Ambient_B");
        beachSource = CreateLoopSource("Beach");
        sfxSource = CreateOneShotSource("SFX");

        activeMusicSource = musicSourceA;
        activeAmbientSource = ambientSourceA;
    }

    AudioSource CreateLoopSource(string childName)
    {
        Transform child = new GameObject(childName).transform;
        child.SetParent(transform, false);
        AudioSource source = child.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    AudioSource CreateOneShotSource(string childName)
    {
        Transform child = new GameObject(childName).transform;
        child.SetParent(transform, false);
        AudioSource source = child.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 1f;
        return source;
    }

    void LoadVolumeSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(PrefMaster, defaultMasterVolume);
        musicBusVolume = PlayerPrefs.GetFloat(PrefMusic, defaultMusicVolume);
        ambientBusVolume = PlayerPrefs.GetFloat(PrefAmbient, defaultAmbientVolume);
        sfxBusVolume = PlayerPrefs.GetFloat(PrefSfx, defaultSfxVolume);
        beachBusVolume = PlayerPrefs.GetFloat(PrefBeach, defaultBeachVolume);
    }

    void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(PrefMaster, masterVolume);
        PlayerPrefs.SetFloat(PrefMusic, musicBusVolume);
        PlayerPrefs.SetFloat(PrefAmbient, ambientBusVolume);
        PlayerPrefs.SetFloat(PrefSfx, sfxBusVolume);
        PlayerPrefs.SetFloat(PrefBeach, beachBusVolume);
        PlayerPrefs.Save();
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        ApplyBusVolumes();
        SaveVolumeSettings();
    }

    public void SetMusicVolume(float value)
    {
        musicBusVolume = Mathf.Clamp01(value);
        ApplyBusVolumes();
        SaveVolumeSettings();
    }

    public void SetAmbientVolume(float value)
    {
        ambientBusVolume = Mathf.Clamp01(value);
        ApplyBusVolumes();
        SaveVolumeSettings();
    }

    public void SetSfxVolume(float value)
    {
        sfxBusVolume = Mathf.Clamp01(value);
        SaveVolumeSettings();
    }

    public void SetBeachVolume(float value)
    {
        beachBusVolume = Mathf.Clamp01(value);
        ApplyBusVolumes();
        SaveVolumeSettings();
    }

    void ApplyBusVolumes()
    {
        if (activeMusicSource != null && activeMusicSource.isPlaying)
        {
            activeMusicSource.volume = GetCurrentMusicChannelVolume();
        }

        if (activeAmbientSource != null && activeAmbientSource.isPlaying)
        {
            activeAmbientSource.volume = GetAmbientChannelVolume(activeProfile);
        }

        if (beachSource != null && beachSource.isPlaying)
        {
            beachSource.volume = beachLayerActive ? GetBeachChannelVolume() : 0f;
        }
    }

    float GetMusicChannelVolume(SceneAudioProfile profile)
    {
        float profileScale = profile != null ? profile.musicVolume : 1f;
        return masterVolume * musicBusVolume * profileScale;
    }

    float GetCurrentMusicChannelVolume()
    {
        return musicOverrideActive
            ? masterVolume * musicBusVolume * musicOverrideVolumeScale
            : GetMusicChannelVolume(activeProfile);
    }

    float GetAmbientChannelVolume(SceneAudioProfile profile)
    {
        float profileScale = profile != null ? profile.ambientVolume : 1f;
        return masterVolume * ambientBusVolume * profileScale;
    }

    float GetBeachChannelVolume()
    {
        return masterVolume * beachBusVolume;
    }

    public void AssignCatalog(SceneAudioCatalog newCatalog)
    {
        catalog = newCatalog;
    }

    public void NotifyTransitionToLoading(string targetSceneName)
    {
        pendingTargetScene = targetSceneName;
        ClearMusicOverrideState();
        SceneAudioProfile loadingProfile = catalog != null ? catalog.LoadingProfile : null;
        if (loadingProfile != null)
        {
            ApplyProfile(loadingProfile, force: true);
        }
    }

    public void NotifyTransitionToLoadingSilently(string targetSceneName)
    {
        pendingTargetScene = targetSceneName;
        ClearMusicOverrideState();
        FadeBeachVolume(0f, 0.2f);
        CrossfadeMusic(null, loop: false, targetVolume: 0f, duration: 0.2f);
        CrossfadeAmbient(null, loop: false, targetVolume: 0f, duration: 0.2f);
        Debug.Log($"[AudioManager] Loading tới '{targetSceneName}' ở chế độ im lặng (intro).");
    }

    public void NotifyTargetSceneReady(string targetSceneName)
    {
        if (catalog == null)
        {
            return;
        }

        SceneAudioProfile profile = catalog.GetProfile(targetSceneName);
        if (profile != null)
        {
            ApplyProfile(profile, force: true);
        }
    }

    public void ApplySceneByName(string sceneName, bool force = false)
    {
        if (catalog == null)
        {
            Debug.LogWarning("[AudioManager] Chưa có SceneAudioCatalog.");
            return;
        }

        SceneAudioProfile profile = catalog.GetProfile(sceneName);
        if (profile != null)
        {
            ApplyProfile(profile, force);
            return;
        }

        Debug.LogWarning($"[AudioManager] Không tìm thấy audio profile cho scene '{sceneName}'.");
    }

    public void ApplyProfile(SceneAudioProfile profile, bool force = false)
    {
        if (profile == null)
        {
            Debug.LogWarning("[AudioManager] ApplyProfile: profile null.");
            return;
        }

        if (!force && activeProfile == profile && activeSceneName == profile.sceneName)
        {
            return;
        }

        activeProfile = profile;
        activeSceneName = profile.sceneName;

        if (profile.music == null && profile.ambient == null)
        {
            Debug.LogWarning($"[AudioManager] Scene '{profile.sceneName}' chưa gán music/ambient clip.");
        }

        float fadeDuration = profile.enterCrossfadeDuration;
        if (!musicOverrideActive)
        {
            CrossfadeMusic(profile.music, profile.loopMusic, GetMusicChannelVolume(profile), fadeDuration);
        }
        CrossfadeAmbient(profile.ambient, profile.loopAmbient, GetAmbientChannelVolume(profile), fadeDuration);
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneTransitionService.LoadingSceneName)
        {
            return;
        }

        if (scene.name == activeSceneName && activeProfile != null)
        {
            return;
        }

        beachLayerActive = false;
        FadeBeachVolume(0f, beachFadeDuration);
        ClearMusicOverrideState();

        ApplySceneByName(scene.name, force: false);
    }

    /// <summary>
    /// Tạm thay nhạc nền scene bằng một track gameplay (ví dụ boss fight).
    /// Âm lượng vẫn đi qua Master + Music bus trong Settings.
    /// </summary>
    public void PlayMusicOverride(
        AudioClip clip,
        float volumeScale = 1f,
        float fadeDuration = 1.2f)
    {
        if (clip == null)
        {
            return;
        }

        float safeVolumeScale = Mathf.Clamp01(volumeScale);
        if (musicOverrideActive && musicOverrideClip == clip)
        {
            musicOverrideVolumeScale = safeVolumeScale;
            ApplyBusVolumes();
            return;
        }

        musicOverrideActive = true;
        musicOverrideClip = clip;
        musicOverrideVolumeScale = safeVolumeScale;
        CrossfadeMusic(
            clip,
            loop: true,
            masterVolume * musicBusVolume * musicOverrideVolumeScale,
            fadeDuration);
    }

    /// <summary>Trả lại track nhạc nền của scene sau khi gameplay override kết thúc.</summary>
    public void StopMusicOverride(AudioClip expectedClip = null, float fadeDuration = 1.5f)
    {
        if (!musicOverrideActive ||
            (expectedClip != null && musicOverrideClip != expectedClip))
        {
            return;
        }

        ClearMusicOverrideState();
        AudioClip sceneMusic = activeProfile != null ? activeProfile.music : null;
        bool loopSceneMusic = activeProfile == null || activeProfile.loopMusic;
        CrossfadeMusic(
            sceneMusic,
            loopSceneMusic,
            GetMusicChannelVolume(activeProfile),
            fadeDuration);
    }

    void ClearMusicOverrideState()
    {
        musicOverrideActive = false;
        musicOverrideClip = null;
        musicOverrideVolumeScale = 1f;
    }

    void CrossfadeMusic(AudioClip clip, bool loop, float targetVolume, float duration)
    {
        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
        }

        musicFadeRoutine = StartCoroutine(CrossfadeRoutine(
            activeMusicSource,
            activeMusicSource == musicSourceA ? musicSourceB : musicSourceA,
            clip,
            loop,
            targetVolume,
            duration,
            source => activeMusicSource = source));
    }

    void CrossfadeAmbient(AudioClip clip, bool loop, float targetVolume, float duration)
    {
        if (ambientFadeRoutine != null)
        {
            StopCoroutine(ambientFadeRoutine);
        }

        ambientFadeRoutine = StartCoroutine(CrossfadeRoutine(
            activeAmbientSource,
            activeAmbientSource == ambientSourceA ? ambientSourceB : ambientSourceA,
            clip,
            loop,
            targetVolume,
            duration,
            source => activeAmbientSource = source));
    }

    IEnumerator CrossfadeRoutine(
        AudioSource fromSource,
        AudioSource toSource,
        AudioClip clip,
        bool loop,
        float targetVolume,
        float duration,
        System.Action<AudioSource> setActiveSource)
    {
        if (clip == null)
        {
            if (fromSource != null)
            {
                yield return FadeVolumeRoutine(fromSource, 0f, duration);
                fromSource.Stop();
            }

            setActiveSource(toSource);
            yield break;
        }

        toSource.clip = clip;
        toSource.loop = loop;
        toSource.volume = 0f;
        toSource.Play();

        float fromStart = fromSource != null ? fromSource.volume : 0f;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / safeDuration));

            if (fromSource != null)
            {
                fromSource.volume = Mathf.Lerp(fromStart, 0f, t);
            }

            toSource.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        if (fromSource != null)
        {
            fromSource.volume = 0f;
            fromSource.Stop();
        }

        toSource.volume = targetVolume;
        setActiveSource(toSource);
    }

    IEnumerator FadeVolumeRoutine(AudioSource source, float targetVolume, float duration)
    {
        if (source == null)
        {
            yield break;
        }

        float start = source.volume;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / safeDuration));
            source.volume = Mathf.Lerp(start, targetVolume, t);
            yield return null;
        }

        source.volume = targetVolume;
    }

    public void SetBeachActive(bool active, AudioClip overrideClip = null)
    {
        AudioClip clip = overrideClip != null ? overrideClip : defaultBeachClip;
        if (active && clip == null)
        {
            return;
        }

        if (active)
        {
            beachLayerActive = true;
            activeBeachClip = clip;

            if (beachSource.clip != clip)
            {
                beachSource.clip = clip;
                beachSource.loop = true;
                beachSource.Play();
            }
            else if (!beachSource.isPlaying)
            {
                beachSource.Play();
            }

            FadeBeachVolume(GetBeachChannelVolume(), beachFadeDuration);
            return;
        }

        beachLayerActive = false;
        FadeBeachVolume(0f, beachFadeDuration);
    }

    void FadeBeachVolume(float targetVolume, float duration)
    {
        if (beachFadeRoutine != null)
        {
            StopCoroutine(beachFadeRoutine);
        }

        beachFadeRoutine = StartCoroutine(FadeVolumeRoutine(beachSource, targetVolume, duration));
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip, masterVolume * sfxBusVolume * Mathf.Clamp01(volumeScale));
    }

    public void PlayUiClick()
    {
        // Reserved for future UI click clip assignment.
    }
}
