using UnityEngine;
using UnityEngine.AI;

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
    [SerializeField] private float commandAttackDamage = 35f;
    [SerializeField] private float commandAttackRange = 3f;
    [SerializeField] private float commandCooldown = 8f;
    [SerializeField] private float skillDamage = 80f;
    [SerializeField] private float skillRadius = 4f;
    [SerializeField] private float skillCooldown = 20f;

    NavMeshAgent agent;
    float commandTimer;
    float skillTimer;
    bool isActive;

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
        agent.speed = followSpeed;
        agent.stoppingDistance = stopDistance;
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

        Transform target = FindNearestEnemy(commandAttackRange * 2f);
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

        CharacterHealth health = target.GetComponentInParent<CharacterHealth>();
        if (health != null && !health.IsDead)
        {
            health.TakeDamage(commandAttackDamage);
        }

        commandTimer = commandCooldown;
        return true;
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