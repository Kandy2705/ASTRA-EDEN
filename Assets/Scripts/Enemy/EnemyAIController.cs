using System.Collections.Generic;
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
    [Tooltip("Tối thiểu giữa các flinch (Hurt) liên tiếp — chống spam đứng đơ khi bị combo nhỏ.")]
    [SerializeField, Min(0f)] private float hurtCooldown = 0.6f;
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
    [Tooltip("Bật để log mọi state transition + lý do vào/thoát Attack/Chase ra Console.")]
    [SerializeField] private bool debugLogStateMachine = false;
    [Tooltip("Bật để log distance, cooldown, agent.remainingDistance mỗi frame trong Chase. Spam nhiều — chỉ bật khi cần.")]
    [SerializeField] private bool debugLogChaseTick = false;

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
    float lastHitReactionTime;
    AttackPatternData currentAttack;
    float attackPhaseTimer;
    enum AttackPhase { None, Windup, Active, Recovery }
    AttackPhase attackPhase = AttackPhase.None;
    bool hitResolvedThisSwing;
    float nextHurtAllowedAt;
    float effectiveAttackRange = 2f;

    public AIState State => currentState;
    public EnemyData Data => enemyData;

    /// <summary>Gọi ngay sau Instantiate, trước Start(), để gán EnemyData + patrol từ spawn point.</summary>
    public void ApplySpawnConfiguration(EnemyData data, Transform[] patrolPts)
    {
        if (data != null)
        {
            enemyData = data;
            RecalculateAttackRange();
        }

        if (patrolPts != null && patrolPts.Length > 0)
        {
            patrolPoints = patrolPts;
        }
    }
    public float MoveSpeed => enemyData != null && enemyData.baseStats != null ? enemyData.baseStats.moveSpeed : agent != null ? agent.speed : 3f;
    /// <summary>Engage range = max(EnemyData.attackRange, max maxRange của attack patterns).
    /// Tránh case attackRange data nhỏ hơn reach thực của animation → enemy phải chạy sát mới đánh.</summary>
    public float AttackRange => effectiveAttackRange;

    void RecalculateAttackRange()
    {
        float baseRange = enemyData != null ? enemyData.attackRange : 2f;
        if (enemyData == null || enemyData.attackPatterns == null)
        {
            effectiveAttackRange = baseRange;
            return;
        }

        float maxPatternRange = 0f;
        foreach (var ap in enemyData.attackPatterns)
        {
            if (ap != null && ap.maxRange > maxPatternRange) maxPatternRange = ap.maxRange;
        }

        effectiveAttackRange = Mathf.Max(baseRange, maxPatternRange);
    }
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

    /// Registry cho các hệ thống cần duyệt enemy đang sống (minimap markers, v.v.).
    public static readonly List<EnemyAIController> Active = new List<EnemyAIController>();

    void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
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
        RecalculateAttackRange();
        ApplyEnemyDataToAgent();
        currentPoise = MaxPoise;

        originPosition = transform.position;
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        if (health != null)
        {
            if (initializeFromEnemyData && enemyData != null && enemyData.baseStats != null)
            {
                health.ApplyEnemyStats(enemyData.baseStats);
            }

            health.Died -= OnDied;
            health.Died += OnDied;
            health.Changed -= OnHealthChanged;
            health.Changed += OnHealthChanged;
            lastKnownHP = health.RuntimeStats != null ? health.RuntimeStats.currentHP : float.NaN;
        }

        if (sensor != null) sensor.Configure(enemyData);

        if (debugLogStateMachine)
        {
            Debug.Log($"[AI:{name}] STARTED | enemyData={(enemyData != null ? enemyData.enemyId : "<null>")} agent={(agent != null)} health={(health != null)} sensor={(sensor != null)} hitbox={(attackHitbox != null)} animator={(animator != null)}", this);
        }

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
        // Stop earlier hơn AttackRange một chút để enemy đứng trong tầm đánh, không bị "đứng rìa" thoát range khi animation đẩy.
        agent.stoppingDistance = Mathf.Max(0.1f, AttackRange * 0.9f);
        if (flipForward180) agent.updateRotation = false;
        if (debugLogStateMachine)
            Debug.Log($"[AI:{name}] ApplyEnemyData | speed={agent.speed:F2} angularSpeed={agent.angularSpeed:F0} stopDist={agent.stoppingDistance:F2} effectiveAtkRange={AttackRange:F2} (data.attackRange={enemyData.attackRange:F2}) atkCooldown={AttackCooldown:F2} attackPatterns={(enemyData.attackPatterns != null ? enemyData.attackPatterns.Count : 0)}", this);
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

        ApplyMovementFacing();
        UpdateAnimatorMovement(GetTargetBlendForState());
    }

    /// <summary>Xoay model theo hướng velocity của agent. Cần khi flipForward180 = true (đã tắt agent.updateRotation),
    /// hoặc khi state Patrol/Return/Retreat không có target để FaceTarget.</summary>
    void ApplyMovementFacing()
    {
        if (agent == null) return;
        if (agent.velocity.sqrMagnitude <= movingThreshold * movingThreshold) return;

        // Khi đang chase/attack/detect → có player, đã có FaceTarget xử lý.
        bool wantsFaceTarget = currentState == AIState.Chase || currentState == AIState.Attack || currentState == AIState.Detect;
        if (wantsFaceTarget && player != null) return;

        Vector3 dir = agent.velocity;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.0001f) return;
        dir.Normalize();

        Vector3 facing = flipForward180 ? -dir : dir;
        Quaternion target = Quaternion.LookRotation(facing);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 10f * Time.deltaTime);
    }

    // ---------- State transitions ----------
    void EnterState(AIState next)
    {
        if (debugLogStateMachine)
        {
            float dist = player != null ? HorizontalDistance(player.position) : -1f;
            Debug.Log($"[AI:{name}] {currentState} → {next} | dist={dist:F2} attackRange={AttackRange:F2} cooldownLeft={attackCooldownTimer:F2} stopDist={(agent != null ? agent.stoppingDistance : 0f):F2}", this);
        }
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
                CancelActiveAttack();
                hitStunTimer = hitStunDuration;
                PlayHitAnimation();
                break;
            case AIState.Stagger:
                StopAgent();
                CancelActiveAttack();
                staggerTimer = staggerDuration;
                if (animator != null && HasParam(StaggerHash, AnimatorControllerParameterType.Trigger))
                {
                    animator.ResetTrigger(AttackHash);
                    animator.SetTrigger(StaggerHash);
                }
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

        if (debugLogChaseTick)
        {
            float rem = (agent != null && !agent.pathPending) ? agent.remainingDistance : -1f;
            float vel = agent != null ? agent.velocity.magnitude : 0f;
            bool inRange = distance <= AttackRange;
            bool cdReady = attackCooldownTimer <= 0f;
            Debug.Log($"[AI:{name}] Chase tick | dist={distance:F2} atkRange={AttackRange:F2} inRange={inRange} cdReady={cdReady} cd={attackCooldownTimer:F2} agentVel={vel:F2} remDist={rem:F2} canSee={canSee}", this);
        }

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

        // Đã trong tầm đánh nhưng cooldown chưa hết → đứng yên face player, không chạy lung tung.
        // Tránh case "cắn xong chạy thêm vài bước rồi mới cắn tiếp".
        if (distance <= AttackRange)
        {
            StopAgent();
            FaceTarget();
            return;
        }

        SetDestinationSafe(player.position);
        FaceTarget();
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
        }
        else if (attackPhase == AttackPhase.Active && !hitResolvedThisSwing)
        {
            float activeDuration = currentAttack != null ? currentAttack.activeTime : 0.2f;
            float elapsed = activeDuration - attackPhaseTimer;
            if (elapsed >= activeDuration * 0.55f)
            {
                ResolveHit();
            }
        }

        if (attackPhase == AttackPhase.Active && attackPhaseTimer <= 0f)
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
        // Hard timeout — phòng trường hợp hitStunTimer bị reset bởi knockback/dame liên tiếp.
        if (hitStunTimer <= 0f || stateTimer >= hitStunDuration + 0.1f) EnterState(AIState.Chase);
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
        if (debugLogStateMachine)
        {
            string atkName = currentAttack != null ? currentAttack.displayName : "<null>";
            float cd = currentAttack != null ? currentAttack.cooldown : AttackCooldown;
            float wu = currentAttack != null ? currentAttack.windup : 0.3f;
            float act = currentAttack != null ? currentAttack.activeTime : 0.2f;
            float rec = currentAttack != null ? currentAttack.recovery : 0.4f;
            Debug.Log($"[AI:{name}] BeginAttack '{atkName}' | windup={wu:F2} active={act:F2} recovery={rec:F2} cdAfter={cd:F2}", this);
        }
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
        // Luôn trừ poise nếu có poise system, để combo cuối cùng vỡ poise.
        if (MaxPoise > 0f)
        {
            currentPoise = Mathf.Max(0f, currentPoise - dmg);
            if (currentPoise <= 0f)
            {
                EnterState(AIState.Stagger);
                return;
            }
        }

        // Với damage rất nhỏ liên tục (chiêu R continuous, DoT) thì bỏ qua hit reaction để tránh spam anim
        if (dmg < 1f) return;

        if (currentState == AIState.Hurt)
        {
            CancelActiveAttack();
            hitStunTimer = hitStunDuration;
            if (Time.time - lastHitReactionTime > 0.35f)
            {
                PlayHitAnimation();
                lastHitReactionTime = Time.time;
            }
            return;
        }

        if (currentState == AIState.Stagger) return;
        if (currentState == AIState.Dead) return;
        if (Time.time < nextHurtAllowedAt) return;

        nextHurtAllowedAt = Time.time + hurtCooldown;
        lastHitReactionTime = Time.time;
        EnterState(AIState.Hurt);
    }

    void CancelActiveAttack()
    {
        attackPhase = AttackPhase.None;
        attackPhaseTimer = 0f;
        currentAttack = null;
        hitResolvedThisSwing = true;

        if (animator != null && HasParam(AttackHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(AttackHash);
        }
    }

    void PlayHitAnimation()
    {
        if (!useHitAnimation || animator == null)
        {
            return;
        }

        if (HasParam(AttackHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(AttackHash);
        }

        if (HasParam(HitHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(HitHash);
            animator.SetTrigger(HitHash);
        }

        // Ưu tiên Hit hơn Attack — ép chạy state Hit ngay, không chờ exit attack clip.
        animator.Play("Hit", 0, 0f);
        lastHitReactionTime = Time.time;
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

    // Animation Event hooks (gọi từ animator clip hoặc EnemyAnimationEventRelay)
    public void Anim_OnAttackStart()
    {
        if (attackHitbox != null)
        {
            if (attackHitbox.TargetLayer.value == 0) attackHitbox.SetTargetLayer(playerLayer);
            attackHitbox.BeginSwing();
        }

        hitResolvedThisSwing = false;
    }

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
