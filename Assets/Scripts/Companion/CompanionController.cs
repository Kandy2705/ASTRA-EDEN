using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Companion follow player + combat theo lệnh.
/// Không auto-aggro: chỉ "thấy" enemy khi player bấm lệnh (T attack / G skill)
/// qua Physics.OverlapSphere tìm CharacterHealth.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class CompanionController : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform owner;
    [SerializeField] private float followDistance = 3f;
    [SerializeField] private float stopDistance = 2f;
    [SerializeField] private float teleportDistance = 18f;
    [SerializeField] private float followSpeed = 4.5f;

    [Header("Combat")]
    [Tooltip("Tầm quét tìm enemy khi bấm lệnh attack (T).")]
    [SerializeField] private float commandAttackDamage = 35f;
    [SerializeField] private float commandAttackRange = 3f;
    [SerializeField] private float commandDetectRange = 8f;
    [SerializeField] private float commandCooldown = 8f;
    [SerializeField] private float skillDamage = 80f;
    [SerializeField] private float skillRadius = 4f;
    [SerializeField] private float skillCooldown = 20f;

    [Header("State")]
    [Tooltip("Bật khi đã có owner (player). Prefab để false — Summon/Start sẽ bật.")]
    [SerializeField] private bool isActive;

    NavMeshAgent agent;
    float commandTimer;
    float skillTimer;

    public bool IsActive => isActive;
    public float CommandCooldownRemaining => commandTimer;
    public float SkillCooldownRemaining => skillTimer;

    public void Initialize(Transform player)
    {
        owner = player;
        isActive = owner != null;
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = followSpeed;
            agent.stoppingDistance = stopDistance;
        }
    }

    void Start()
    {
        PlayerLoadoutRuntime.ActivePlayerChanged += HandleActivePlayerChanged;
        // Clone kéo tay vào scene / chưa qua Summon: tự gắn Player.
        if (!isActive || owner == null)
        {
            TryAutoBindPlayer();
        }
    }

    void OnDestroy()
    {
        PlayerLoadoutRuntime.ActivePlayerChanged -= HandleActivePlayerChanged;
    }

    void HandleActivePlayerChanged(PlayerLoadoutRuntime activePlayer)
    {
        if (activePlayer != null) Initialize(activePlayer.transform);
    }

    void TryAutoBindPlayer()
    {
        if (owner != null)
        {
            isActive = true;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Initialize(player.transform);
        }
    }

    void Update()
    {
        if (!isActive || owner == null)
        {
            return;
        }

        if (commandTimer > 0f) commandTimer -= Time.deltaTime;
        if (skillTimer > 0f) skillTimer -= Time.deltaTime;

        TickFollow();
    }

    void TickFollow()
    {
        float dist = Vector3.Distance(transform.position, owner.position);

        if (dist > teleportDistance)
        {
            Vector3 behind = owner.position - owner.forward * 1.5f;
            if (NavMesh.SamplePosition(behind, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            return;
        }

        if (dist > followDistance)
        {
            agent.isStopped = false;
            agent.SetDestination(owner.position);
        }
        else
        {
            agent.isStopped = true;
        }
    }

    public bool TryCommandAttack()
    {
        if (!isActive || commandTimer > 0f)
        {
            return false;
        }

        // "Thấy" enemy chỉ lúc này — quét collider trong tầm, không có AI look liên tục.
        float detectRange = Mathf.Max(commandDetectRange, commandAttackRange * 2f);
        Transform target = FindNearestEnemy(detectRange);
        if (target == null)
        {
            return false;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(toTarget.normalized);
        }

        // Tiến gần nếu còn xa hơn attack range
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > commandAttackRange && agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }

        CharacterHealth health = target.GetComponentInParent<CharacterHealth>();
        if (health != null && !health.IsDead && dist <= commandAttackRange + 0.75f)
        {
            health.TakeDamage(commandAttackDamage);
            commandTimer = commandCooldown;
            return true;
        }

        // Đã lock target nhưng chưa vào tầm — không tốn full cooldown, cho thử lại.
        return false;
    }

    public bool TryUseSkill()
    {
        if (!isActive || skillTimer > 0f)
        {
            return false;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, skillRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            CharacterHealth health = hits[i].GetComponentInParent<CharacterHealth>();
            if (health == null || health.IsDead || health.CompareTag("Player"))
            {
                continue;
            }

            health.TakeDamage(skillDamage);
        }

        skillTimer = skillCooldown;
        return true;
    }

    Transform FindNearestEnemy(float range)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            CharacterHealth health = hits[i].GetComponentInParent<CharacterHealth>();
            if (health == null || health.IsDead || health.CompareTag("Player"))
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, health.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = health.transform;
            }
        }

        return best;
    }
}
