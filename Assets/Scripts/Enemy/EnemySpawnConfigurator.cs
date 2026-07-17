using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gắn dữ liệu runtime lên enemy vừa spawn (stats, AI, loot, NavMesh).
/// </summary>
public static class EnemySpawnConfigurator
{
    /// <param name="preferExactSpawnPosition">
    /// true = spawn tại đúng điểm (At Patrol Points): chỉ snap NavMesh rất gần,
    /// không kéo ra rìa mesh xa.
    /// </param>
    public static GameObject Configure(
        GameObject instance,
        EnemyData data,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        Transform[] patrolPoints,
        bool isMiniBoss = false,
        bool preferExactSpawnPosition = false)
    {
        if (instance == null)
        {
            return null;
        }

        instance.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();

        // ROOT CAUSE FIX: NavMeshAgent enable/Warp ngoài mesh → Unity hút ra rìa mesh.
        // At Patrol Points: tắt agent → đặt đúng chỗ → chỉ bật lại nếu mesh nằm ≤0.75m.
        if (preferExactSpawnPosition)
        {
            PlaceExactlyAt(instance, agent, spawnPosition);
        }
        else if (!TryWarpToNavMesh(instance, spawnPosition, out Vector3 navPos, preferExactSpawnPosition: false))
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

    /// <summary>
    /// Đặt enemy đúng world position. Chỉ bật NavMeshAgent nếu có mesh sát (≤0.75m).
    /// Tránh agent auto-snap ra rìa building/terrain.
    /// </summary>
    public static void PlaceExactlyAt(GameObject instance, NavMeshAgent agent, Vector3 worldPos)
    {
        if (instance == null)
        {
            return;
        }

        if (agent != null)
        {
            agent.enabled = false;
        }

        instance.transform.position = worldPos;

        if (agent == null)
        {
            return;
        }

        const float maxSnap = 0.75f;
        if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, maxSnap, NavMesh.AllAreas))
        {
            Vector3 flat = hit.position - worldPos;
            flat.y = 0f;
            float dy = Mathf.Abs(hit.position.y - worldPos.y);
            if (flat.magnitude <= maxSnap && dy <= 1.25f)
            {
                agent.enabled = true;
                agent.Warp(hit.position);
                instance.transform.position = hit.position;
                return;
            }
        }

        // Không có mesh sát: giữ agent TẮT — transform đứng đúng patrol.
        // AI sẽ bật agent sau nếu tìm được mesh gần, hoặc đi bằng transform.
        agent.enabled = false;
        instance.transform.position = worldPos;
    }

    static bool TryWarpToNavMesh(
        GameObject instance,
        Vector3 spawnPosition,
        out Vector3 navPosition,
        bool preferExactSpawnPosition)
    {
        navPosition = spawnPosition;

        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            return false;
        }

        if (preferExactSpawnPosition)
        {
            PlaceExactlyAt(instance, agent, spawnPosition);
            navPosition = instance.transform.position;
            return agent.enabled && agent.isOnNavMesh;
        }

        if (!agent.enabled)
        {
            agent.enabled = true;
        }

        float[] radii = { 1.5f, 3f, 6f, 12f, 20f };
        NavMeshHit best = default;
        bool found = false;
        float bestScore = float.MaxValue;

        for (int i = 0; i < radii.Length; i++)
        {
            if (!NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, radii[i], NavMesh.AllAreas))
            {
                continue;
            }

            float dy = Mathf.Abs(hit.position.y - spawnPosition.y);
            Vector3 flat = hit.position - spawnPosition;
            flat.y = 0f;
            float horiz = flat.magnitude;

            if (dy > 2.75f)
            {
                continue;
            }

            float score = horiz + dy * 2f;
            if (score < bestScore)
            {
                bestScore = score;
                best = hit;
                found = true;
            }

            if (horiz <= 0.5f)
            {
                break;
            }
        }

        if (found)
        {
            navPosition = best.position;
            agent.Warp(navPosition);
            instance.transform.position = navPosition;
            return agent.isOnNavMesh;
        }

        agent.enabled = false;
        instance.transform.position = spawnPosition;
        return false;
    }
}