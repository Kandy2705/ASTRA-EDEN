using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BeachAudioZone : MonoBehaviour
{
    [SerializeField] private AudioClip beachClipOverride;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool disableOnExit = true;

    int occupants;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        occupants++;
        if (occupants != 1)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        manager?.SetBeachActive(true, beachClipOverride);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        occupants = Mathf.Max(0, occupants - 1);
        if (!disableOnExit || occupants > 0)
        {
            return;
        }

        AudioManager manager = AudioManager.Instance;
        manager?.SetBeachActive(false);
    }
}