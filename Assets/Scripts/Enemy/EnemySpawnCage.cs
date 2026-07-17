using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Điểm spawn trong zone/màn.
///
/// Cách dùng (đúng design ASTRA EDEN):
/// 1) Gán Entries: mỗi dòng = 1 loại EnemyData + số lượng
/// 2) Gán Patrol Points: kéo các empty Transform — enemy đi tuần theo các điểm này
/// 3) Đặt object này trong zone → khi vào màn / player gần → sinh đúng số lượng
///
/// Respawn (chết hết sinh lại) là tùy chọn, mặc định TẮT (chỉ spawn 1 lần).
/// </summary>
[DisallowMultipleComponent]
public class EnemySpawnCage : MonoBehaviour
{
    public enum SpawnWhen
    {
        /// <summary>Sinh ngay khi scene/zone load (Start).</summary>
        OnZoneStart = 0,
        /// <summary>Chỉ sinh khi player vào bán kính kích hoạt (vào vùng spawn).</summary>
        WhenPlayerEntersRange = 1,
    }

    public enum RespawnMode
    {
        /// <summary>Chỉ spawn 1 lần — không tự hồi (mặc định, đúng màn chơi).</summary>
        None = 0,
        /// <summary>Chết hết wave → chờ delay → sinh lại.</summary>
        AfterWaveCleared = 1,
        /// <summary>Mỗi con chết → chờ delay → sinh bù đúng loại.</summary>
        PerEnemy = 2,
    }

    public enum SpawnPlacement
    {
        /// <summary>Random trong bán kính quanh cage (cần NavMesh).</summary>
        RandomInRadius = 0,
        /// <summary>Lần lượt đặt lên các Patrol Points (lặp nếu count > số điểm).</summary>
        AtPatrolPoints = 1,
    }

    [Serializable]
    public class SpawnEntry
    {
        [Tooltip("Loại enemy (SO). Prefab lấy từ enemyData.enemyPrefab nếu không override.")]
        public EnemyData enemyData;

        [Tooltip("Prefab riêng. Để trống = dùng EnemyData.enemyPrefab → Default Prefab.")]
        public GameObject prefabOverride;

        [Tooltip("Số lượng enemy loại này sinh ra.")]
        [Min(1)]
        public int count = 1;

        [Tooltip("Mini-boss (HUD + kill tracker riêng).")]
        public bool isMiniBoss;
    }

    [Header("1. Enemy sinh ra (loại + số lượng)")]
    [SerializeField] private SpawnEntry[] entries = Array.Empty<SpawnEntry>();
    [SerializeField] private GameObject defaultEnemyPrefab;

    [Header("2. Cách đi đứng (patrol)")]
    [Tooltip("Kéo các empty Transform vào đây. Enemy AI tuần tra theo thứ tự các điểm này.")]
    [SerializeField] private Transform[] patrolPoints;

    [Tooltip("Nếu không gán patrol: tự tạo vòng tròn quanh cage.")]
    [SerializeField] private bool autoCreatePatrolIfEmpty = true;
    [SerializeField, Min(0f)] private float autoPatrolRadius = 10f;
    [SerializeField, Range(2, 12)] private int autoPatrolCount = 4;

    [Header("3. Chỗ sinh ra")]
    [Tooltip("AtPatrolPoints = sinh đúng từng Patrol_1,2,3... (khuyến nghị).")]
    [SerializeField] private SpawnPlacement spawnPlacement = SpawnPlacement.AtPatrolPoints;
    [Tooltip("Bán kính random spawn (khi RandomInRadius).")]
    [SerializeField, Min(0.5f)] private float spawnRadius = 8f;
    [SerializeField, Min(0.5f)] private float navMeshSampleRadius = 4f;
    [SerializeField] private float spawnHeightOffset = 0.1f;
    [Tooltip("Không snap NavMesh nếu lệch cao độ quá mức này (tránh tụt xuống đất ngoài nhà).")]
    [SerializeField, Min(0.1f)] private float maxVerticalSnap = 2.5f;
    [Tooltip("Cho phép spawn đúng tọa độ điểm (trong nhà) dù chưa có NavMesh. Agent có thể đứng yên đến khi bake floor.")]
    [SerializeField] private bool allowSpawnOffNavMesh = true;

    [Header("4. Khi nào sinh")]
    [SerializeField] private SpawnWhen spawnWhen = SpawnWhen.OnZoneStart;
    [Tooltip("Chỉ dùng khi Spawn When = WhenPlayerEntersRange.")]
    [SerializeField, Min(1f)] private float playerEnterRadius = 25f;
    [SerializeField] private string playerTag = "Player";

    [Header("5. Respawn (tuỳ chọn)")]
    [SerializeField] private RespawnMode respawnMode = RespawnMode.None;
    [SerializeField, Min(0f)] private float respawnDelay = 30f;

    [Header("Hierarchy / Debug")]
    [SerializeField] private Transform spawnedParent;
    [SerializeField] private bool logSpawns = true;
    [SerializeField] private bool drawGizmos = true;

    readonly List<AliveEnemy> alive = new List<AliveEnemy>(16);
    readonly List<PendingRespawn> pending = new List<PendingRespawn>(8);

    Transform[] resolvedPatrol;
    Transform player;
    bool hasSpawnedOnce;
    float waveRespawnAt = -1f;
    int nextPatrolSpawnIndex;

    struct AliveEnemy
    {
        public GameObject instance;
        public CharacterHealth health;
        public int entryIndex;
    }

    struct PendingRespawn
    {
        public int entryIndex;
        public float readyAt;
    }

    public int AliveCount
    {
        get
        {
            PruneDead(schedulePerEnemy: false);
            return alive.Count;
        }
    }

    void Awake()
    {
        if (spawnedParent == null)
        {
            var go = new GameObject("SpawnedEnemies");
            go.transform.SetParent(transform, false);
            spawnedParent = go.transform;
        }
    }

    void Start()
    {
        CachePlayer();
        resolvedPatrol = ResolvePatrolPoints();

        if (spawnWhen == SpawnWhen.OnZoneStart)
        {
            SpawnWave();
        }
    }

    void Update()
    {
        // Vào vùng → sinh 1 lần
        if (!hasSpawnedOnce && spawnWhen == SpawnWhen.WhenPlayerEntersRange)
        {
            if (IsPlayerInEnterRange())
            {
                SpawnWave();
            }
            else if (Time.frameCount % 30 == 0 && logSpawns && Time.time < 3f)
            {
                // tránh spam
            }

            return;
        }

        // Retry nếu OnZoneStart fail NavMesh
        if (!hasSpawnedOnce && spawnWhen == SpawnWhen.OnZoneStart)
        {
            if (Time.frameCount % 30 == 0)
            {
                SpawnWave();
            }

            return;
        }

        if (respawnMode == RespawnMode.None)
        {
            return;
        }

        PruneDead(schedulePerEnemy: respawnMode == RespawnMode.PerEnemy);

        if (respawnMode == RespawnMode.AfterWaveCleared)
        {
            TickWaveRespawn();
        }
        else if (respawnMode == RespawnMode.PerEnemy)
        {
            TickPerEnemyRespawn();
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < alive.Count; i++)
        {
            Unhook(alive[i]);
        }

        alive.Clear();
    }

    /// <summary>Sinh đúng entries (loại + số lượng), gán patrol cho AI đi tuần.</summary>
    [ContextMenu("Spawn Wave Now")]
    public void SpawnWave()
    {
        ClearAliveInstances();
        pending.Clear();
        waveRespawnAt = -1f;
        nextPatrolSpawnIndex = 0;

        if (entries == null || entries.Length == 0)
        {
            Debug.LogWarning(
                $"[ZoneSpawn] '{name}' chưa gán Entries (loại enemy + số lượng).",
                this);
            return;
        }

        // Luôn resolve lại mỗi wave (tránh cache rỗng / điểm đã destroy).
        resolvedPatrol = ResolvePatrolPoints();

        if (spawnPlacement == SpawnPlacement.AtPatrolPoints
            && (resolvedPatrol == null || resolvedPatrol.Length == 0))
        {
            Debug.LogError(
                $"[ZoneSpawn] '{name}' Spawn Placement = At Patrol Points nhưng KHÔNG có Patrol Points. " +
                "Gán Patrol_1..n vào mảng Patrol Points (hoặc chạy tool Fix Spawn Cages). " +
                "Không spawn random rìa.",
                this);
            return;
        }

        if ((resolvedPatrol == null || resolvedPatrol.Length == 0) && logSpawns)
        {
            Debug.LogWarning(
                $"[ZoneSpawn] '{name}' không có Patrol Points — enemy spawn random, không có lộ trình tuần tra.",
                this);
        }

        if (logSpawns && resolvedPatrol != null)
        {
            for (int i = 0; i < resolvedPatrol.Length; i++)
            {
                if (resolvedPatrol[i] != null)
                {
                    Debug.Log(
                        $"[ZoneSpawn] '{name}' patrol[{i}] = '{resolvedPatrol[i].name}' @ {resolvedPatrol[i].position}",
                        resolvedPatrol[i]);
                }
            }
        }

        int spawned = 0;
        for (int e = 0; e < entries.Length; e++)
        {
            SpawnEntry entry = entries[e];
            if (entry == null || entry.count <= 0)
            {
                continue;
            }

            for (int n = 0; n < entry.count; n++)
            {
                if (TrySpawnOne(e))
                {
                    spawned++;
                }
            }
        }

        if (spawned > 0)
        {
            hasSpawnedOnce = true;
        }

        if (logSpawns)
        {
            if (spawned > 0)
            {
                Debug.Log(
                    $"[ZoneSpawn] '{name}' sinh {spawned} enemy | patrol points = " +
                    $"{(resolvedPatrol != null ? resolvedPatrol.Length : 0)}",
                    this);
            }
            else
            {
                Debug.LogWarning(
                    $"[ZoneSpawn] '{name}' spawn 0. Check prefab/EnemyData + NavMesh (pos={transform.position}).",
                    this);
            }
        }
    }

    [ContextMenu("Clear Spawned")]
    public void ClearAliveInstances()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            Unhook(alive[i]);
            if (alive[i].instance != null)
            {
                Destroy(alive[i].instance);
            }
        }

        alive.Clear();
        pending.Clear();
        waveRespawnAt = -1f;
    }

    bool TrySpawnOne(int entryIndex)
    {
        SpawnEntry entry = entries[entryIndex];
        GameObject prefab = ResolvePrefab(entry);
        if (prefab == null)
        {
            Debug.LogError(
                $"[ZoneSpawn] '{name}' entry[{entryIndex}] thiếu prefab " +
                $"(gán EnemyData.enemyPrefab hoặc Prefab Override hoặc Default).",
                this);
            return false;
        }

        bool exactPatrolSpawn = spawnPlacement == SpawnPlacement.AtPatrolPoints;
        GameObject instance;

        if (exactPatrolSpawn)
        {
            // ============================================================
            // SPAWN CHẮC CHẮN ĐÚNG ĐIỂM: parent TRỰC TIẾP vào Patrol_x
            // local = (0,0,0) → pivot enemy = đúng gizmo Patrol (nhìn Hierarchy).
            // ============================================================
            if (!TryPickNextPatrol(out Transform patrolPt))
            {
                Debug.LogWarning($"[ZoneSpawn] '{name}' không có Patrol point hợp lệ.", this);
                return false;
            }

            instance = Instantiate(prefab);

            // Tắt agent TRƯỚC mọi thứ — agent bật sớm = hút ra rìa NavMesh.
            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }

            var rb = instance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            // Parent vào đúng Patrol → local (0,0,0) = đứng đúng điểm.
            instance.transform.SetParent(patrolPt, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            // scale giữ prefab

            Vector3 worldPos = instance.transform.position;
            Quaternion worldRot = instance.transform.rotation;

            // Configure: không để nó SetPosition lung tung — preferExact + agent off.
            EnemySpawnConfigurator.Configure(
                instance,
                entry.enemyData,
                worldPos,
                worldRot,
                resolvedPatrol,
                entry.isMiniBoss,
                preferExactSpawnPosition: true);

            // Ép lại sau Configure (Configure có thể đụng transform).
            instance.transform.SetParent(patrolPt, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            if (agent != null)
            {
                agent.enabled = false;
            }

            // Cage parent (để AI unparent sau khi bắt đầu tuần tra / agent).
            Transform cageParent = patrolPt.parent != null ? patrolPt.parent : transform;

            EnemyAIController ai = instance.GetComponent<EnemyAIController>();
            if (ai != null)
            {
                ai.BindSpawnToPatrolPoint(patrolPt, cageParent, resolvedPatrol);
            }

            if (logSpawns)
            {
                Debug.Log(
                    $"[ZoneSpawn] PIN '{instance.name}' → child of '{patrolPt.name}' " +
                    $"local={instance.transform.localPosition} world={instance.transform.position} " +
                    $"(expect local=0,0,0)",
                    instance);
            }
        }
        else
        {
            if (!TryGetSpawnPose(out Vector3 pos, out Quaternion rot))
            {
                Debug.LogWarning(
                    $"[ZoneSpawn] '{name}' không resolve được vị trí spawn.",
                    this);
                return false;
            }

            instance = Instantiate(prefab, pos, rot);
            if (spawnedParent != null)
            {
                instance.transform.SetParent(spawnedParent, true);
            }

            EnemySpawnConfigurator.Configure(
                instance,
                entry.enemyData,
                pos,
                rot,
                resolvedPatrol,
                entry.isMiniBoss,
                preferExactSpawnPosition: false);
        }

        CharacterHealth health = instance.GetComponent<CharacterHealth>();
        if (health != null)
        {
            health.Died += HandleEnemyDied;
        }

        alive.Add(new AliveEnemy
        {
            instance = instance,
            health = health,
            entryIndex = entryIndex,
        });
        return true;
    }

    bool TryPickNextPatrol(out Transform patrolPt)
    {
        patrolPt = null;
        if (resolvedPatrol == null || resolvedPatrol.Length == 0)
        {
            return false;
        }

        for (int guard = 0; guard < resolvedPatrol.Length; guard++)
        {
            Transform pt = resolvedPatrol[nextPatrolSpawnIndex % resolvedPatrol.Length];
            nextPatrolSpawnIndex++;
            if (pt != null)
            {
                patrolPt = pt;
                return true;
            }
        }

        return false;
    }

    bool TryGetSpawnPose(out Vector3 pos, out Quaternion rot)
    {
        rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

        if (spawnPlacement == SpawnPlacement.AtPatrolPoints)
        {
            // AtPatrol xử lý trong TrySpawnOne (local match).
            pos = transform.position;
            return false;
        }

        // Random quanh cage: ưu tiên cùng độ cao với cage (sàn nhà), không tụt xuống terrain.
        if (TryRandomSpawnPosition(out pos))
        {
            return true;
        }

        if (allowSpawnOffNavMesh)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * spawnRadius;
            pos = transform.position + new Vector3(circle.x, spawnHeightOffset, circle.y);
            if (logSpawns)
            {
                Debug.LogWarning(
                    $"[ZoneSpawn] '{name}' spawn off-NavMesh gần cage (có thể trong nhà chưa bake mesh). " +
                    "Window → AI → Navigation: bake sàn Outpost.",
                    this);
            }

            return true;
        }

        return false;
    }

    bool TryRandomSpawnPosition(out Vector3 result)
    {
        result = transform.position;
        const int attempts = 20;
        float sampleR = Mathf.Max(navMeshSampleRadius, 1.5f);

        // Chỉ lệch Y nhẹ — KHÔNG sample ±20/40m (sẽ dính terrain dưới Outpost).
        float[] yOffsets = { 0f, 0.5f, 1f, -0.5f, 1.5f, -1f, 2f };

        for (int i = 0; i < attempts; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * spawnRadius;
            for (int y = 0; y < yOffsets.Length; y++)
            {
                Vector3 candidate = transform.position
                    + new Vector3(circle.x, yOffsets[y] + spawnHeightOffset, circle.y);

                if (!TrySnapSameFloor(candidate, out Vector3 snapped, sampleR))
                {
                    continue;
                }

                Vector3 flat = snapped - transform.position;
                flat.y = 0f;
                float maxDist = spawnRadius + 1.5f;
                if (flat.sqrMagnitude <= maxDist * maxDist)
                {
                    result = snapped;
                    return true;
                }
            }
        }

        // Cùng tầng ngay dưới/cạnh cage
        return TrySnapSameFloor(transform.position + Vector3.up * spawnHeightOffset, out result, sampleR);
    }

    /// <summary>
    /// Snap NavMesh chỉ khi điểm tìm được không lệch tầng (Y) so với vị trí mong muốn.
    /// Tránh SamplePosition bán kính lớn kéo enemy xuống đất ngoài/dưới nhà.
    /// </summary>
    bool TrySnapSameFloor(Vector3 desired, out Vector3 result, float sampleRadius = -1f)
    {
        if (sampleRadius < 0f)
        {
            sampleRadius = Mathf.Max(navMeshSampleRadius, 1.5f);
        }

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            if (Mathf.Abs(hit.position.y - desired.y) <= maxVerticalSnap
                && HorizontalDistance(hit.position, desired) <= sampleRadius + 0.01f)
            {
                result = hit.position;
                return true;
            }
        }

        result = desired;
        return false;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    GameObject ResolvePrefab(SpawnEntry entry)
    {
        if (entry.prefabOverride != null)
        {
            return entry.prefabOverride;
        }

        if (entry.enemyData != null && entry.enemyData.enemyPrefab != null)
        {
            return entry.enemyData.enemyPrefab;
        }

        return defaultEnemyPrefab;
    }

    Transform[] ResolvePatrolPoints()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            var valid = new List<Transform>(patrolPoints.Length);
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                {
                    valid.Add(patrolPoints[i]);
                }
            }

            if (valid.Count > 0)
            {
                return valid.ToArray();
            }
        }

        // Child empty (trừ SpawnedEnemies / _AutoPatrol sẽ rebuild)
        var fromChildren = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == spawnedParent)
            {
                continue;
            }

            if (child.name.StartsWith("Spawned", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (child.name == "_AutoPatrol")
            {
                continue;
            }

            fromChildren.Add(child);
        }

        if (fromChildren.Count > 0)
        {
            return fromChildren.ToArray();
        }

        if (autoCreatePatrolIfEmpty)
        {
            return BuildAutoPatrolRing();
        }

        return Array.Empty<Transform>();
    }

    Transform[] BuildAutoPatrolRing()
    {
        if (autoPatrolCount <= 0 || autoPatrolRadius <= 0f)
        {
            return Array.Empty<Transform>();
        }

        Transform ringRoot = transform.Find("_AutoPatrol");
        if (ringRoot != null && ringRoot.childCount == autoPatrolCount)
        {
            var existing = new Transform[ringRoot.childCount];
            for (int i = 0; i < ringRoot.childCount; i++)
            {
                existing[i] = ringRoot.GetChild(i);
            }

            return existing;
        }

        if (ringRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(ringRoot.gameObject);
            }
            else
            {
                DestroyImmediate(ringRoot.gameObject);
            }
        }

        var root = new GameObject("_AutoPatrol");
        root.transform.SetParent(transform, false);

        var points = new Transform[autoPatrolCount];
        for (int i = 0; i < autoPatrolCount; i++)
        {
            float angle = (360f / autoPatrolCount) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * autoPatrolRadius;
            var go = new GameObject($"Patrol_{i + 1}");
            go.transform.SetParent(root.transform, false);
            go.transform.position = transform.position + offset;
            points[i] = go.transform;
        }

        return points;
    }

    void HandleEnemyDied(CharacterHealth health)
    {
        RemoveAliveByHealth(health, schedulePerEnemy: respawnMode == RespawnMode.PerEnemy);
    }

    void TickWaveRespawn()
    {
        PruneDead(schedulePerEnemy: false);

        if (alive.Count > 0)
        {
            waveRespawnAt = -1f;
            return;
        }

        if (!hasSpawnedOnce)
        {
            return;
        }

        if (waveRespawnAt < 0f)
        {
            waveRespawnAt = Time.time + respawnDelay;
            if (logSpawns)
            {
                Debug.Log($"[ZoneSpawn] '{name}' wave clear → respawn sau {respawnDelay:0.#}s.", this);
            }
        }

        if (Time.time >= waveRespawnAt)
        {
            SpawnWave();
        }
    }

    void TickPerEnemyRespawn()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (Time.time < pending[i].readyAt)
            {
                continue;
            }

            int entryIndex = pending[i].entryIndex;
            pending.RemoveAt(i);

            int living = CountAliveOfEntry(entryIndex);
            int want = entries != null && entryIndex >= 0 && entryIndex < entries.Length
                ? Mathf.Max(1, entries[entryIndex].count)
                : 1;

            if (living < want)
            {
                TrySpawnOne(entryIndex);
            }
        }
    }

    void PruneDead(bool schedulePerEnemy)
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            AliveEnemy rec = alive[i];
            bool dead = rec.instance == null || (rec.health != null && rec.health.IsDead);
            if (!dead)
            {
                continue;
            }

            Unhook(rec);
            if (schedulePerEnemy)
            {
                QueuePerEnemyRespawn(rec.entryIndex);
            }

            alive.RemoveAt(i);
        }
    }

    void RemoveAliveByHealth(CharacterHealth health, bool schedulePerEnemy)
    {
        if (health == null)
        {
            return;
        }

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i].health != health)
            {
                continue;
            }

            int entryIndex = alive[i].entryIndex;
            Unhook(alive[i]);
            alive.RemoveAt(i);
            if (schedulePerEnemy)
            {
                QueuePerEnemyRespawn(entryIndex);
            }

            break;
        }
    }

    void QueuePerEnemyRespawn(int entryIndex)
    {
        pending.Add(new PendingRespawn
        {
            entryIndex = entryIndex,
            readyAt = Time.time + respawnDelay,
        });
    }

    int CountAliveOfEntry(int entryIndex)
    {
        int n = 0;
        for (int i = 0; i < alive.Count; i++)
        {
            if (alive[i].entryIndex == entryIndex && alive[i].instance != null)
            {
                n++;
            }
        }

        return n;
    }

    void Unhook(AliveEnemy rec)
    {
        if (rec.health != null)
        {
            rec.health.Died -= HandleEnemyDied;
        }
    }

    void CachePlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null)
        {
            player = p.transform;
        }
    }

    bool IsPlayerInEnterRange()
    {
        if (player == null)
        {
            CachePlayer();
            if (player == null)
            {
                return false;
            }
        }

        Vector3 a = transform.position;
        Vector3 b = player.position;
        a.y = 0f;
        b.y = 0f;
        return (a - b).sqrMagnitude <= playerEnterRadius * playerEnterRadius;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);

        if (spawnPlacement == SpawnPlacement.RandomInRadius)
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.5f);
            DrawWireCircle(transform.position, spawnRadius, 32);
        }

        if (spawnWhen == SpawnWhen.WhenPlayerEntersRange)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.35f);
            DrawWireCircle(transform.position, playerEnterRadius, 40);
        }

        // Patrol path
        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.95f);
        Transform[] pts = patrolPoints;
        if (pts != null && pts.Length > 0)
        {
            Vector3 prev = default;
            bool hasPrev = false;
            for (int i = 0; i < pts.Length; i++)
            {
                if (pts[i] == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(pts[i].position, 0.3f);
                Gizmos.DrawLine(transform.position, pts[i].position);
                if (hasPrev)
                {
                    Gizmos.DrawLine(prev, pts[i].position);
                }

                prev = pts[i].position;
                hasPrev = true;
            }

            // đóng vòng tuần tra
            if (hasPrev && pts[0] != null)
            {
                Gizmos.DrawLine(prev, pts[0].position);
            }
        }
        else if (autoCreatePatrolIfEmpty && autoPatrolRadius > 0f)
        {
            Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.3f);
            DrawWireCircle(transform.position, autoPatrolRadius, 28);
        }
    }

    static void DrawWireCircle(Vector3 center, float radius, int segments)
    {
        float step = 360f / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = step * i * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
