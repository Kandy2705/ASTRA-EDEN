using UnityEngine;

/// <summary>
/// Plays dinosaur/enemy vocal clips from animation events or gameplay hooks.
/// Kept field names compatible with the serialized vocal clip component already
/// present on enemy prefabs.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class DinosaurVocalAudio : MonoBehaviour
{
    private const string DefaultCatalogResourcePath = "ASTRA/SO_EnemyVocalCatalog";
    private const float AttackRepeatGuard = 0.7f;

    [SerializeField] private AudioClip[] growlClips;
    [SerializeField] private AudioClip[] sniffClips;
    [SerializeField] private AudioClip[] yelpClips;
    [SerializeField] private AudioClip[] barkClips;
    [SerializeField] private AudioClip[] roarClips;
    [SerializeField] private AudioClip[] screechClips;
    [SerializeField] private AudioClip[] callClips;
    [SerializeField] private AudioClip[] deathClips;

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private float minInterval = 0.25f;

    private AudioSource source;
    private CharacterHealth health;
    private float lastPlayTime = -999f;
    private float lastAttackPlayTime = -999f;

    private void Awake()
    {
        ApplyDefaultCatalogIfNeeded();
        source = GetComponent<AudioSource>();
        health = GetComponentInParent<CharacterHealth>();
    }

    private void OnEnable()
    {
        if (health == null)
        {
            health = GetComponentInParent<CharacterHealth>();
        }

        if (health != null)
        {
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= OnDied;
        }
    }

    public void PlayGrowl() => PlayRandom(growlClips);
    public void Growl() => PlayGrowl();
    public void OnGrowl() => PlayGrowl();

    public void PlaySniff() => PlayRandom(sniffClips);
    public void Sniff() => PlaySniff();
    public void OnSniff() => PlaySniff();

    public void PlayYelp() => PlayRandom(yelpClips);
    public void Yelp() => PlayYelp();
    public void OnYelp() => PlayYelp();

    public void PlayBark() => PlayRandom(barkClips);
    public void Bark() => PlayBark();
    public void OnBark() => PlayBark();

    public void PlayRoar() => PlayRandom(roarClips, growlClips, barkClips);
    public void Roar() => PlayRoar();
    public void OnRoar() => PlayRoar();

    public void PlayScreech() => PlayRandom(screechClips, roarClips);
    public void Screech() => PlayScreech();
    public void OnScreech() => PlayScreech();

    public void PlayCall() => PlayRandom(callClips, roarClips, growlClips);
    public void Call() => PlayCall();
    public void OnCall() => PlayCall();

    public void PlayAttack()
    {
        if (Time.time < lastAttackPlayTime + AttackRepeatGuard)
        {
            return;
        }

        lastAttackPlayTime = Time.time;
        PlayRandom(roarClips, barkClips, growlClips);
    }
    public void Attack() => PlayAttack();
    public void OnAttack() => PlayAttack();

    public void PlayDeath() => PlayRandom(deathClips, roarClips, yelpClips);
    public void Death() => PlayDeath();
    public void OnDeath() => PlayDeath();

    private void OnDied(CharacterHealth _) => PlayDeath();

    private void ApplyDefaultCatalogIfNeeded()
    {
        if (HasAnyConfiguredClip())
        {
            return;
        }

        EnemyVocalCatalog catalog = Resources.Load<EnemyVocalCatalog>(
            DefaultCatalogResourcePath);
        if (catalog == null)
        {
            return;
        }

        growlClips = catalog.GrowlClips;
        sniffClips = catalog.SniffClips;
        yelpClips = catalog.YelpClips;
        barkClips = catalog.BarkClips;
        roarClips = catalog.RoarClips;
        screechClips = catalog.ScreechClips;
        callClips = catalog.CallClips;
        deathClips = catalog.DeathClips;
    }

    private bool HasAnyConfiguredClip()
    {
        return HasClip(growlClips) ||
               HasClip(sniffClips) ||
               HasClip(yelpClips) ||
               HasClip(barkClips) ||
               HasClip(roarClips) ||
               HasClip(screechClips) ||
               HasClip(callClips) ||
               HasClip(deathClips);
    }

    private static bool HasClip(AudioClip[] clips)
    {
        if (clips == null)
        {
            return false;
        }

        foreach (AudioClip clip in clips)
        {
            if (clip != null)
            {
                return true;
            }
        }

        return false;
    }

    private void PlayRandom(params AudioClip[][] clipSets)
    {
        if (source == null)
        {
            source = GetComponent<AudioSource>();
        }

        if (source == null || Time.time < lastPlayTime + minInterval)
        {
            return;
        }

        AudioClip clip = PickClip(clipSets);
        if (clip == null)
        {
            return;
        }

        lastPlayTime = Time.time;
        source.PlayOneShot(clip, volume);
    }

    private static AudioClip PickClip(AudioClip[][] clipSets)
    {
        if (clipSets == null)
        {
            return null;
        }

        int total = 0;
        foreach (AudioClip[] clips in clipSets)
        {
            if (clips == null)
            {
                continue;
            }

            foreach (AudioClip clip in clips)
            {
                if (clip != null)
                {
                    total++;
                }
            }
        }

        if (total == 0)
        {
            return null;
        }

        int target = Random.Range(0, total);
        foreach (AudioClip[] clips in clipSets)
        {
            if (clips == null)
            {
                continue;
            }

            foreach (AudioClip clip in clips)
            {
                if (clip == null)
                {
                    continue;
                }

                if (target == 0)
                {
                    return clip;
                }

                target--;
            }
        }

        return null;
    }
}
