using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawn enemy tại các <see cref="EnemySpawnPoint"/> con khi scene load.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Defaults")]
    [Tooltip("Prefab enemy đầy đủ component (AI, loot, health...).")]
    [SerializeField] private GameObject defaultEnemyPrefab;
    [Tooltip("Dùng khi spawn point không gán EnemyData.")]
    [SerializeField] private EnemyData defaultEnemyData;

    [Header("Spawn Points")]
    [Tooltip("Để trống = tự lấy mọi EnemySpawnPoint trong children.")]
    [SerializeField] private EnemySpawnPoint[] spawnPoints;

    [Header("Behaviour")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private Transform spawnedEnemiesParent;
    [SerializeField] private bool logSpawns = true;

    readonly List<GameObject> spawnedInstances = new List<GameObject>();

    public IReadOnlyList<GameObject> SpawnedInstances => spawnedInstances;

    void Awake()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            spawnPoints = GetComponentsInChildren<EnemySpawnPoint>(true);
        }
    }

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnAll();
        }
    }

    [ContextMenu("Spawn All Enemies")]
    public void SpawnAll()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"[EnemySpawn] '{name}' không có EnemySpawnPoint nào.", this);
            return;
        }

        ClearSpawned();

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            EnemySpawnPoint point = spawnPoints[i];
            if (point == null || !point.EnabledForSpawn)
            {
                continue;
            }

            SpawnAtPoint(point);
        }

        if (logSpawns)
        {
            Debug.Log($"[EnemySpawn] '{name}' spawned {spawnedInstances.Count} enemy(s).", this);
        }
    }

    public GameObject SpawnAtPoint(EnemySpawnPoint point)
    {
        if (point == null)
        {
            return null;
        }

        EnemyData data = point.EnemyData != null ? point.EnemyData : defaultEnemyData;
        GameObject prefab = ResolvePrefab(point, data, defaultEnemyPrefab);
        if (prefab == null)
        {
            Debug.LogError(
                $"[EnemySpawn] '{name}' không resolve được prefab cho spawn point '{point.name}'. " +
                "Gán PrefabOverride, EnemyData.enemyPrefab, hoặc defaultEnemyPrefab.",
                this);
            return null;
        }

        GameObject instance = Instantiate(prefab, point.SpawnPosition, point.SpawnRotation);
        if (spawnedEnemiesParent != null)
        {
            instance.transform.SetParent(spawnedEnemiesParent, true);
        }

        EnemySpawnConfigurator.Configure(
            instance,
            data,
            point.SpawnPosition,
            point.SpawnRotation,
            point.ResolvePatrolPoints(),
            point.IsMiniBoss);

        spawnedInstances.Add(instance);
        return instance;
    }

    [ContextMenu("Clear Spawned Enemies")]
    public void ClearSpawned()
    {
        for (int i = spawnedInstances.Count - 1; i >= 0; i--)
        {
            if (spawnedInstances[i] != null)
            {
                Destroy(spawnedInstances[i]);
            }
        }

        spawnedInstances.Clear();
    }

    /// <summary>
    /// Priority: spawn-point override → EnemyData.enemyPrefab → spawner default.
    /// </summary>
    static GameObject ResolvePrefab(EnemySpawnPoint point, EnemyData data, GameObject fallbackPrefab)
    {
        if (point != null && point.PrefabOverride != null)
        {
            return point.PrefabOverride;
        }

        if (data != null && data.enemyPrefab != null)
        {
            return data.enemyPrefab;
        }

        return fallbackPrefab;
    }
}