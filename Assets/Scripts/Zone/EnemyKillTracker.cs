using UnityEngine;

/// <summary>
/// Gắn lên enemy prefab (hoặc thêm runtime khi spawn) để đếm kill cho zone objective.
/// </summary>
[DisallowMultipleComponent]
public class EnemyKillTracker : MonoBehaviour
{
    [SerializeField] private CharacterHealth health;
    [SerializeField] private bool skipMiniBoss;
    [SerializeField] private EnemyData enemyData;
    bool rewardGranted;

    public void Configure(bool skipMiniBossKill, EnemyData data = null)
    {
        skipMiniBoss = skipMiniBossKill;
        if (data != null)
        {
            enemyData = data;
        }
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
        GrantExperience();

        if (skipMiniBoss && GetComponent<MiniBossMarker>() != null)
        {
            return;
        }

        ZoneObjectiveManager.Instance?.NotifyEnemyKilled();
    }

    void GrantExperience()
    {
        if (rewardGranted)
        {
            return;
        }

        rewardGranted = true;
        if (enemyData == null)
        {
            EnemyAIController ai = GetComponent<EnemyAIController>();
            enemyData = ai != null ? ai.Data : null;
        }

        int reward = enemyData != null ? Mathf.Max(0, enemyData.expReward) : 0;
        if (reward <= 0)
        {
            return;
        }

        PlayerProgression progression = PlayerProgression.FindForPlayer();
        if (progression != null)
        {
            progression.AddExperience(reward);
        }
    }
}
