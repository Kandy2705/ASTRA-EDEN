using UnityEngine;

/// <summary>
/// Gắn lên enemy prefab (hoặc thêm runtime khi spawn) để đếm kill cho zone objective.
/// </summary>
[DisallowMultipleComponent]
public class EnemyKillTracker : MonoBehaviour
{
    [SerializeField] private CharacterHealth health;
    [SerializeField] private bool skipMiniBoss;

    public void Configure(bool skipMiniBossKill)
    {
        skipMiniBoss = skipMiniBossKill;
    }

    void Awake()
    {
        if (health == null)
        {
            health = GetComponent<CharacterHealth>();
        }
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.Died += HandleDied;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleDied;
        }
    }

    void HandleDied(CharacterHealth _)
    {
        if (skipMiniBoss && GetComponent<MiniBossMarker>() != null)
        {
            return;
        }

        ZoneObjectiveManager.Instance?.NotifyEnemyKilled();
    }
}