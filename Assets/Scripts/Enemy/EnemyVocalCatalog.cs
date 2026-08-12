using UnityEngine;

[CreateAssetMenu(
    fileName = "SO_EnemyVocalCatalog",
    menuName = "ASTRA EDEN/Audio/Enemy Vocal Catalog")]
public sealed class EnemyVocalCatalog : ScriptableObject
{
    [SerializeField] private AudioClip[] growlClips;
    [SerializeField] private AudioClip[] sniffClips;
    [SerializeField] private AudioClip[] yelpClips;
    [SerializeField] private AudioClip[] barkClips;
    [SerializeField] private AudioClip[] roarClips;
    [SerializeField] private AudioClip[] screechClips;
    [SerializeField] private AudioClip[] callClips;
    [SerializeField] private AudioClip[] deathClips;

    public AudioClip[] GrowlClips => growlClips;
    public AudioClip[] SniffClips => sniffClips;
    public AudioClip[] YelpClips => yelpClips;
    public AudioClip[] BarkClips => barkClips;
    public AudioClip[] RoarClips => roarClips;
    public AudioClip[] ScreechClips => screechClips;
    public AudioClip[] CallClips => callClips;
    public AudioClip[] DeathClips => deathClips;
}
