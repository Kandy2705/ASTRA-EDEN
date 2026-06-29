using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đánh dấu vị trí spawn enemy. Đặt làm con của <see cref="EnemySpawner"/>.
/// Patrol: gán thủ công, hoặc tạo child empty, hoặc để trống để tự sinh vòng quanh điểm spawn.
/// </summary>
[DisallowMultipleComponent]
public class EnemySpawnPoint : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] private EnemyData enemyData;
    [Tooltip("Bỏ qua prefab mặc định của spawner. Để trống = dùng default prefab.")]
    [SerializeField] private GameObject prefabOverride;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [Tooltip("Tự sinh patrol ring khi không có patrol point nào.")]
    [SerializeField, Min(0f)] private float patrolRadius = 6f;
    [SerializeField, Range(2, 8)] private int autoPatrolCount = 4;

    [Header("Behaviour")]
    [SerializeField] private bool enabledForSpawn = true;
    [Tooltip("Đánh dấu spawn point này là mini-boss encounter.")]
    [SerializeField] private bool isMiniBoss;

    public bool EnabledForSpawn => enabledForSpawn;
    public bool IsMiniBoss => isMiniBoss;
    public EnemyData EnemyData => enemyData;
    public GameObject PrefabOverride => prefabOverride;

    public Vector3 SpawnPosition => transform.position;
    public Quaternion SpawnRotation => transform.rotation;

    public Transform[] ResolvePatrolPoints()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            return patrolPoints;
        }

        var fromChildren = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<EnemySpawnPoint>() != null)
            {
                continue;
            }

            fromChildren.Add(child);
        }

        if (fromChildren.Count > 0)
        {
            return fromChildren.ToArray();
        }

        if (Application.isPlaying)
        {
            return BuildAutoPatrolRing();
        }

        return System.Array.Empty<Transform>();
    }

    Transform[] BuildAutoPatrolRing()
    {
        if (autoPatrolCount <= 0 || patrolRadius <= 0f)
        {
            return System.Array.Empty<Transform>();
        }

        var points = new Transform[autoPatrolCount];
        Vector3 center = transform.position;

        for (int i = 0; i < autoPatrolCount; i++)
        {
            float angle = (360f / autoPatrolCount) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * patrolRadius;
            var go = new GameObject($"Patrol_{i + 1}");
            go.transform.SetParent(transform, false);
            go.transform.position = center + offset;
            points[i] = go.transform;
        }

        return points;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);

        Transform[] patrols = patrolPoints != null && patrolPoints.Length > 0
            ? patrolPoints
            : null;

        if (patrols == null || patrols.Length == 0)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, patrolRadius);
            return;
        }

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.85f);
        Vector3 origin = transform.position;
        for (int i = 0; i < patrols.Length; i++)
        {
            if (patrols[i] == null) continue;
            Gizmos.DrawSphere(patrols[i].position, 0.25f);
            Gizmos.DrawLine(origin, patrols[i].position);
        }
    }
}