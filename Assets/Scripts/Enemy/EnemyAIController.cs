using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Data-driven enemy AI FSM theo briefing ASTRA EDEN.
/// State: Spawn → Idle → Patrol → Detect → Chase → Attack → Hurt → Stagger → Retreat → ReturnToOrigin → Dead.
/// Cấu hình lấy từ EnemyData SO. Hợp tác với CharacterHealth, EnemyAttackHitbox, EnemySensor.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CharacterHealth))]
[DisallowMultipleComponent]
public class EnemyAIController : MonoBehaviour
{
    public enum AIState
    {
        Spawn,
        Idle,
        Patrol,
        Detect,
        Chase,
        Attack,
        Hurt,
        Stagger,
        Retreat,
        ReturnToOrigin,
        Dead,
    }

    static readonly int BlendHash = Animator.StringToHash("Blend");
    static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    static readonly int VerticalHash = Animator.StringToHash("Vertical");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int HitHash = Animator.StringToHash("Hit");
    static readonly int StaggerHash = Animator.StringToHash("Stagger");
    static readonly int DieHash = Animator.StringToHash("Die");
    static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    [Header("Data")]
    [SerializeField] private EnemyData enemyData;
    [Tooltip("Tự apply HP/atk/def/moveSpeed từ EnemyData lúc Awake.")]
    [SerializeField] private bool initializeFromEnemyData = true;

    [Header("References")]
    [SerializeField] private EnemySensor sensor;
    [SerializeField] private CharacterHealth health;
    [SerializeField] private CharacterKnockback knockback;
    [SerializeField] private RagdollOnDeath ragdoll;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyAttackHitbox attackHitbox;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField, Min(0f)] private float idleDuration = 2f;
    [SerializeField, Min(0f)] private float patrolPointTolerance = 0.5f;

    [Header("Behaviour Tuning")]
    [Tooltip("Sau khi mất sight, giữ chase thêm bao nhiêu giây.")]
    [SerializeField, Min(0f)] private float loseTargetTime = 6f;
    [Tooltip("Khoảng cách xa hơn origin thì Return.")]
    [SerializeField, Min(0f)] private float returnDistance = 30f;
    [Tooltip("Tốc độ khi Patrol (ratio so với moveSpeed).")]
    [SerializeField, Range(0.1f, 1f)] private float patrolSpeedRatio = 0.5f;
    [Tooltip("Tốc độ khi Retreat — ranged enemy kéo khoảng cách (ratio so với moveSpeed).")]
    [SerializeField, Range(0.5f, 2f)] private float retreatSpeedRatio = 1.1f;
    [Tooltip("Ngưỡng kích hoạt Retreat — Ranged/Caster khi player gần hơn min của max attack range.")]
    [SerializeField, Min(0f)] private float retreatMinRange = 4f;

    [Header("Hurt / Stagger")]
    [Tooltip("Bật anim Hit khi mất HP nhưng poise còn.")]
    [SerializeField] private bool useHitAnimation = true;
    [SerializeField, Min(0f)] private float hitStunDuration = 0.25f;
    [Tooltip("Stagger duration khi poise vỡ.")]
    [SerializeField, Min(0f)] private float staggerDuration = 1.2f;
    [Tooltip("Poise hồi/giây sau khi stagger kết thúc.")]
    [SerializeField, Min(0f)] private float poiseRegenAfterStagger = 0f;

    [Header("Death")]
    [SerializeField] private bool useDeathAnimation = true;
    [SerializeField, Min(0f)] private float deathAnimationDuration = 2f;

    [Header("Model Orientation")]
    [Tooltip("Tick nếu model forward thực ra là -Z.")]
    [SerializeField] private bool flipForward180 = false;
    [SerializeField, Min(0f)] private float animatorDampTime = 0.1f;
    [SerializeField, Min(0f)] private float movingThreshold = 0.05f;

    [Header("Targeting")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Debug")]
    [SerializeField] private AIState debugState;
    [SerializeField] private float debugPoise;
    [SerializeField] private float debugLastKnownHP;
    [SerializeField] private bool drawAttackGizmo = true;

    NavMeshAgent agent;
    Transform player;
    Vector3 originPosition;
    AIState currentState;

    int patrolIndex;
    float idleTimer;
    float stateTimer;
    float lostSightTimer;
    float attackCooldownTimer;
    float hitStunTimer;
    float staggerTimer;
    float deathTimer;
    bool deathSequenceFinished;
    float lastKnownHP = float.NaN;
    float currentPoise;
    AttackPatternData currentAttack;
    float attackPhaseTimer;
    enum AttackPhase { None, Windup, Active, Recovery }
    AttackPhase attackPhase = AttackPhase.None;
    bool hitResolvedThisSwing;

    public AIState State => currentState;
    public EnemyData Data => enemyData;
    public float MoveSpeed => enemyData != null && enemyData.baseStats != null ? enemyData.baseStats.moveSpeed : agent != null ? agent.speed : 3f;
    public float AttackRange => enemyData != null ? enemyData.attackRange : 2f;
    public float AggroKeepRange => enemyData != null ? enemyData.aggroKeepRange : 22f;
    public float AttackCooldown => enemyData != null ? enemyData.attackCooldown : 2f;
    public float MaxPoise => enemyData != null && enemyData.baseStats != null ? enemyData.baseStats.poise : 0f;

    void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<CharacterHealth>();
        knockback = GetComponent<CharacterKnockback>();
        ragdoll = GetComponent<RagdollOnDeath>();
        sensor = GetComponentInChildren<EnemySensor>();
        animator = GetComponentInChildren<Animator>();
        attackHitbox = GetComponentInChildren<EnemyAttackHitbox>();
    }

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (health == null) health = GetComponent<CharacterHealth>();
        if (knockback == null) knockback = GetComponent<CharacterKnockback>();
        if (ragdoll == null) ragdoll = GetComponent<RagdollOnDeath>();
        if (sensor == null) sensor = GetComponentInChildren<EnemySensor>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (attackHitbox == null) attackHitbox = GetComponentInChildren<EnemyAttackHitbox>();

        if (playerLayer.value == 0) playerLayer = LayerMask.GetMask("Player");
    }

    void Start()
    {
        ApplyEnemyDataToAgent();
        currentPoise = MaxPoise;

        originPosition = transform.position;
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        if (health != null)
        {
            health.Died -= OnDied;
            health.Died += OnDied;
            health.Changed -= OnHealthChanged;
            health.Changed += OnHealthChanged;
            lastKnownHP = health.RuntimeStats != null ? health.RuntimeStats.currentHP : float.NaN;
        }

        if (sensor != null) sensor.Configure(enemyData);

        EnterState(AIState.Spawn);
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= OnDied;
            health.Changed -= OnHealthChanged;
        }
    }

    void ApplyEnemyDataToAgent()
    {
        if (!initializeFromEnemyData || enemyData == null || enemyData.baseStats == null || agent == null) return;
        agent.speed = enemyData.baseStats.moveSpeed;
        agent.angularSpeed = enemyData.baseStats.turnSpeed;
        agent.stoppingDistance = Mathf.Max(0.1f, enemyData.attackRange * 0.85f);
        if (flipForward180) agent.updateRotation = false;
    }

    void Update()
    {
        debugState = currentState;
        debugPoise = currentPoise;
        debugLastKnownHP = lastKnownHP;

        stateTimer += Time.deltaTime;
        if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;

        if (currentState == AIState.Dead)
        {
            TickDead();
            return;
        }

        if (health != null && health.IsDead)
        {
            EnterState(AIState.Dead);
            return;
        }

        // Knockback override: chỉ để physics đẩy, không update AI.
        if (knockback != null && knockback.IsKnockedBack)
        {
            StopAgent();
            UpdateAnimatorMovement(0f);
            return;
        }

        if (hitStunTimer > 0f) hitStunTimer -= Time.deltaTime;
        if (staggerTimer > 0f) staggerTimer -= Time.deltaTime;

        switch (currentState)
        {
            case AIState.Spawn:          TickSpawn(); break;
            case AIState.Idle:           TickIdle(); break;
            case AIState.Patrol:         TickPatrol(); break;
            case AIState.Detect:         TickDetect(); break;
            case AIState.Chase:          TickChase(); break;
            case AIState.Attack:         TickAttack(); break;
            case AIState.Hurt:           TickHurt(); break;
            case AIState.Stagger:        TickStagger(); break;
            case AIState.Retreat:        TickRetreat(); break;
            case AIState.ReturnToOrigin: TickReturn(); break;
        }

        UpdateAnimatorMovement(GetTargetBlendForState());
    }

    // ---------- State transitions ----------
    void EnterState(AIState next)
    {
        currentState = next;
        stateTimer = 0f;

        switch (next)
        {
            case AIState.Spawn:
                StopAgent();
                break;
            case AIState.Idle:
                StopAgent();
                idleTimer = idleDuration;
                break;
            case AIState.Patrol:
                if (agent != null) agent.speed = MoveSpeed * patrolSpeedRatio;
                GoToNextPatrolPoint();
                break;
            case AIState.Detect:
                StopAgent();
                FaceTarget();
                break;
            case AIState.Chase:
                if (agent != null) agent.speed = MoveSpeed;
                lostSightTimer = 0f;
                break;
            case AIState.Attack:
                StopAgent();
                BeginAttackPattern();
                break;
            case AIState.Hurt:
                StopAgent();
                hitStunTimer = hitStunDuration;
                if (useHitAnimation && animator != null && HasParam(HitHash, AnimatorControllerParameterType.Trigger))
                    animator.SetTrigger(HitHash);
                break;
            case AIState.Stagger:
                StopAgent();
                staggerTimer = staggerDuration;
                if (animator != null && HasParam(StaggerHash, AnimatorControllerParameterType.Trigger))
                    animator.SetTrigger(StaggerHash);
                break;
            case AIState.Retreat:
                if (agent != null) agent.speed = MoveSpeed * retreatSpeedRatio;
                break;
            case AIState.ReturnToOrigin:
                if (agent != null)
                {
                    agent.speed = MoveSpeed;
                    SetDestinationSafe(originPosition);
                }
                break;
            case AIState.Dead:
                EnterDead();
                break;
        }
    }

    // ---------- State ticks ----------
    void TickSpawn()
    {
        if (stateTimer >= 0.25f) EnterState(AIState.Idle);
    }

    void TickIdle()
    {
        if (CheckDetect()) return;
        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f && patrolPoints != null && patrolPoints.Length > 0)
            EnterState(AIState.Patrol);
    }

    void TickPatrol()
    {
        if (CheckDetect()) return;
        if (agent == null || patrolPoints == null || patrolPoints.Length == 0)
        {
            EnterState(AIState.Idle);
            return;
        }
        if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, patrolPointTolerance))
        {
            EnterState(AIState.Idle);
        }
    }

    void TickDetect()
    {
        FaceTarget();
        if (stateTimer >= 0.35f) EnterState(AIState.Chase);
    }

    void TickChase()
    {
        if (player == null) { EnterState(AIState.ReturnToOrigin); return; }

        float distance = HorizontalDistance(player.position);
        bool canSee = sensor != null && sensor.CanSense(player, out _);

        if (canSee) lostSightTimer = 0f;
        else
        {
            lostSightTimer += Time.deltaTime;
            if (lostSightTimer >= loseTargetTime || distance > AggroKeepRange)
            {
                EnterState(AIState.ReturnToOrigin);
                return;
            }
        }

        // Ranged/Caster lùi nếu player áp sát.
        if (NeedsRetreat(distance))
        {
            EnterState(AIState.Retreat);
            return;
        }

        if (CanStartAttack(distance))
        {
            EnterState(AIState.Attack);
            return;
        }

        SetDestinationSafe(player.position);
    }

    void TickAttack()
    {
        if (player != null) FaceTarget();
        attackPhaseTimer -= Time.deltaTime;

        if (attackPhase == AttackPhase.Windup && attackPhaseTimer <= 0f)
        {
            attackPhase = AttackPhase.Active;
            attackPhaseTimer = currentAttack != null ? currentAttack.activeTime : 0.2f;
            hitResolvedThisSwing = false;
            if (attackHitbox != null) attackHitbox.BeginSwing();
            ResolveHit();
        }
        else if (attackPhase == AttackPhase.Active && attackPhaseTimer <= 0f)
        {
            attackPhase = AttackPhase.Recovery;
            attackPhaseTimer = currentAttack != null ? currentAttack.recovery : 0.4f;
        }
        else if (attackPhase == AttackPhase.Recovery && attackPhaseTimer <= 0f)
        {
            attackPhase = AttackPhase.None;
            attackCooldownTimer = currentAttack != null ? currentAttack.cooldown : AttackCooldown;
            EnterState(AIState.Chase);
        }
    }

    void TickHurt()
    {
        if (hitStunTimer <= 0f) EnterState(AIState.Chase);
    }

    void TickStagger()
    {
        if (staggerTimer <= 0f)
        {
            currentPoise = MaxPoise;
            EnterState(AIState.Chase);
        }
        else if (poiseRegenAfterStagger > 0f)
        {
            currentPoise = Mathf.Min(MaxPoise, currentPoise + poiseRegenAfterStagger * Time.deltaTime);
        }
    }

    void TickRetreat()
    {
        if (player == null) { EnterState(AIState.ReturnToOrigin); return; }
        float distance = HorizontalDistance(player.position);

        if (distance >= AttackRange * 0.9f)
        {
            EnterState(CanStartAttack(distance) ? AIState.Attack : AIState.Chase);
            return;
        }

        Vector3 away = transform.position + (transform.position - player.position).normalized * 2.5f;
        SetDestinationSafe(away);
    }

    void TickReturn()
    {
        if (CheckDetect()) return;
        if (agent == null) { EnterState(AIState.Idle); return; }
        if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, patrolPointTolerance))
        {
            EnterState(AIState.Idle);
        }
    }

    void TickDead()
    {
        if (deathSequenceFinished) return;
        deathTimer -= Time.deltaTime;
        if (deathTimer <= 0f)
        {
            deathSequenceFinished = true;
            if (ragdoll != null) ragdoll.EnableRagdoll();
            else if (agent != null) agent.enabled = false;
        }
    }

    void EnterDead()
    {
        StopAgent();
        attackPhase = AttackPhase.None;

        if (animator != null)
        {
            if (HasParam(IsDeadHash, AnimatorControllerParameterType.Bool)) animator.SetBool(IsDeadHash, true);
            if (useDeathAnimation && HasParam(DieHash, AnimatorControllerParameterType.Trigger))
            {
                animator.ResetTrigger(AttackHash);
                animator.ResetTrigger(HitHash);
                animator.SetTrigger(DieHash);
            }
        }

        if (ragdoll != null && useDeathAnimation) ragdoll.BeginDeathPhysics();
        deathTimer = useDeathAnimation ? Mathf.Max(0.05f, deathAnimationDuration) : 0f;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
        }
    }

    // ---------- Helpers ----------
    bool CheckDetect()
    {
        if (player == null || sensor == null) return false;
        if (!sensor.CanSense(player, out _)) return false;
        EnterState(AIState.Detect);
        return true;
    }

    bool CanStartAttack(float distance)
    {
        if (attackCooldownTimer > 0f) return false;
        if (distance > AttackRange) return false;
        return true;
    }

    bool NeedsRetreat(float distance)
    {
        if (enemyData == null) return false;
        bool isRanged = enemyData.archetype == EnemyArchetype.Ranged || enemyData.archetype == EnemyArchetype.CasterSupport;
        if (!isRanged) return false;
        return distance < retreatMinRange;
    }

    void BeginAttackPattern()
    {
        currentAttack = PickAttackPattern();
        attackPhase = AttackPhase.Windup;
        attackPhaseTimer = currentAttack != null ? currentAttack.windup : 0.3f;
        hitResolvedThisSwing = false;
        if (animator != null) animator.SetTrigger(AttackHash);
    }

    AttackPatternData PickAttackPattern()
    {
        if (enemyData == null || enemyData.attackPatterns == null || enemyData.attackPatterns.Count == 0) return null;
        if (player == null) return enemyData.attackPatterns[0];

        float distance = HorizontalDistance(player.position);
        AttackPatternData best = null;
        int bestScore = -1;
        foreach (var ap in enemyData.attackPatterns)
        {
            if (ap == null) continue;
            int score = 0;
            if (distance >= ap.minRange && distance <= ap.maxRange) score += 2;
            if (best == null || score > bestScore) { best = ap; bestScore = score; }
        }
        return best;
    }

    void ResolveHit()
    {
        if (hitResolvedThisSwing || attackHitbox == null || currentAttack == null) return;
        hitResolvedThisSwing = true;

        if (attackHitbox.TargetLayer.value == 0) attackHitbox.SetTargetLayer(playerLayer);
        float baseAtk = enemyData != null && enemyData.baseStats != null ? enemyData.baseStats.attack : 10f;
        float damage = baseAtk * currentAttack.damageMultiplier;
        attackHitbox.PerformHit(damage);
    }

    // ---------- Damage / Poise hooks ----------
    void OnHealthChanged(CharacterHealth h)
    {
        if (h == null || h.RuntimeStats == null) return;
        float current = h.RuntimeStats.currentHP;

        if (!float.IsNaN(lastKnownHP) && current < lastKnownHP - 0.001f && currentState != AIState.Dead && !h.IsDead)
        {
            float dmg = lastKnownHP - current;
            ApplyPoiseDamage(dmg);
        }
        lastKnownHP = current;
    }

    void ApplyPoiseDamage(float dmg)
    {
        if (MaxPoise <= 0f)
        {
            // No poise system → flinch tạm thời.
            if (currentState != AIState.Attack && currentState != AIState.Hurt) EnterState(AIState.Hurt);
            return;
        }

        currentPoise = Mathf.Max(0f, currentPoise - dmg);
        if (currentPoise <= 0f) EnterState(AIState.Stagger);
        else if (currentState != AIState.Attack && currentState != AIState.Hurt) EnterState(AIState.Hurt);
    }

    void OnDied(CharacterHealth h)
    {
        EnterState(AIState.Dead);
    }

    // ---------- Movement utilities ----------
    void GoToNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        var target = patrolPoints[patrolIndex];
        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        if (target != null) SetDestinationSafe(target.position);
    }

    void SetDestinationSafe(Vector3 pos)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.SetDestination(pos);
    }

    void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
    }

    void FaceTarget()
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.0001f) return;
        Vector3 facing = flipForward180 ? -dir : dir;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing.normalized), 12f * Time.deltaTime);
    }

    float HorizontalDistance(Vector3 worldPos)
    {
        Vector3 d = worldPos - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    float GetTargetBlendForState()
    {
        switch (currentState)
        {
            case AIState.Chase:
            case AIState.Retreat:
            case AIState.ReturnToOrigin:
                return 2f;
            case AIState.Patrol:
                return 1f;
            default:
                return 0f;
        }
    }

    void UpdateAnimatorMovement(float targetBlend)
    {
        if (animator == null) return;

        Vector3 local = Vector3.zero;
        if (agent != null && agent.velocity.sqrMagnitude > movingThreshold * movingThreshold)
        {
            local = transform.InverseTransformDirection(agent.velocity.normalized);
            if (flipForward180) { local.x = -local.x; local.z = -local.z; }
        }
        animator.SetFloat(BlendHash, targetBlend, animatorDampTime, Time.deltaTime);
        animator.SetFloat(HorizontalHash, local.x, animatorDampTime, Time.deltaTime);
        animator.SetFloat(VerticalHash, local.z, animatorDampTime, Time.deltaTime);
    }

    bool HasParam(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (var p in animator.parameters)
            if (p.nameHash == hash && p.type == type) return true;
        return false;
    }

    // Animation Event hooks (gọi từ animator clip nếu muốn timing chính xác)
    public void Anim_OnAttackHit() => ResolveHit();
    public void Anim_OnAttackEnd()
    {
        if (currentState == AIState.Attack) attackPhaseTimer = 0f;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawAttackGizmo) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, AggroKeepRange);
    }
}
