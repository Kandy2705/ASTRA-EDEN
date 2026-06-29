using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gắn dữ liệu runtime lên enemy vừa spawn (stats, AI, loot, NavMesh).
/// </summary>
public static class EnemySpawnConfigurator
{
    public static GameObject Configure(
        GameObject instance,
        EnemyData data,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        Transform[] patrolPoints,
        bool isMiniBoss = false)
    {
        if (instance == null)
        {
            return null;
        }

        instance.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        if (!TryWarpToNavMesh(instance, spawnPosition, out Vector3 navPos))
        {
            Debug.LogWarning(
                $"[EnemySpawn] '{instance.name}' spawn ngoài NavMesh tại {spawnPosition}. " +
                "Enemy có thể đứng yên — kiểm tra NavMesh bake hoặc dời spawn point.",
                instance);
        }
        else
        {
            instance.transform.position = navPos;
        }

        if (data != null)
        {
            instance.name = string.IsNullOrEmpty(data.displayName) ? data.enemyId : data.displayName;

            CharacterHealth health = instance.GetComponent<CharacterHealth>();
            if (health != null && data.baseStats != null)
            {
                health.ApplyEnemyStats(data.baseStats);
            }

            EnemyAIController ai = instance.GetComponent<EnemyAIController>();
            if (ai != null)
            {
                ai.ApplySpawnConfiguration(data, patrolPoints);
            }

            EnemySensor sensor = instance.GetComponentInChildren<EnemySensor>();
            if (sensor != null)
            {
                sensor.Configure(data);
            }

            LootDropSpawner loot = instance.GetComponent<LootDropSpawner>();
            if (loot != null)
            {
                loot.ConfigureFromEnemyData(data);
            }
        }
        else if (patrolPoints != null && patrolPoints.Length > 0)
        {
            EnemyAIController ai = instance.GetComponent<EnemyAIController>();
            if (ai != null)
            {
                ai.ApplySpawnConfiguration(null, patrolPoints);
            }
        }

        EnsureEnemyKillTracker(instance, isMiniBoss);

        if (isMiniBoss)
        {
            EnsureMiniBossMarker(instance, data);
        }

        return instance;
    }

    static void EnsureEnemyKillTracker(GameObject instance, bool isMiniBoss)
    {
        EnemyKillTracker tracker = instance.GetComponent<EnemyKillTracker>();
        if (tracker == null)
        {
            tracker = instance.AddComponent<EnemyKillTracker>();
        }

        tracker.Configure(isMiniBoss);
    }

    static void EnsureMiniBossMarker(GameObject instance, EnemyData data)
    {
        MiniBossMarker marker = instance.GetComponent<MiniBossMarker>();
        if (marker == null)
        {
            marker = instance.AddComponent<MiniBossMarker>();
        }

        CharacterHealth health = instance.GetComponent<CharacterHealth>();
        string displayName = data != null && !string.IsNullOrEmpty(data.displayName)
            ? data.displayName
            : "Mini Boss";
        marker.Configure(displayName, health);
    }

    static bool TryWarpToNavMesh(GameObject instance, Vector3 spawnPosition, out Vector3 navPosition)
    {
        navPosition = spawnPosition;

        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            return false;
        }

        const float sampleRadius = 4f;
        if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            navPosition = hit.position;
            if (agent.enabled)
            {
                agent.Warp(navPosition);
            }

            return true;
        }

        return false;
    }
}