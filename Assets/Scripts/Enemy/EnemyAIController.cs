using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

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
    static readonly int TackleHash = Animator.StringToHash("Tackle");

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

    [Header("Tackle Push")]
    [SerializeField] private bool useTackle = true;
    [Tooltip("Số lần enemy cắn xong trước khi tiếp tục bằng tackle đẩy player.")]
    [FormerlySerializedAs("hitsRequiredForTackle")]
    [SerializeField, Min(1)] private int attacksBeforeTackle = 2;
    [SerializeField, Min(0f)] private float tackleRange = 3.2f;
    [Tooltip("Cooldown tối thiểu giữa hai lần tackle.")]
    [SerializeField, Min(0f)] private float tackleCooldown = 6f;

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
    float nextTackleTime;
    int attacksSinceLastTackle;
    bool isTackling;
    float effectiveAttackRange = 2f;

    public AIState State => currentState;
    public EnemyData Data => enemyData;

    /// <summary>
    /// Spawn gắn vào Patrol (child local 0,0,0). Agent chỉ bật khi mesh sát + không teleport.
    /// </summary>
    bool spawnPinnedToPatrol;
    bool agentModeActive;
    Transform homePatrolPoint;
    Transform cageParent;
    Vector3 lockedSpawnPosition;
    Vector3 lastFrameWorldPos;
    int pinHoldFrames = 20;
    int transformPatrolIndex;
    /// <summary>Hysteresis: đứng trong tầm đánh, chỉ chase lại khi player ra khỏi + buffer.</summary>
    bool holdingInAttackRange;
    float nextAgentRecoverTime;
    /// <summary>Frame này có bước transform (fallback) — dùng cho anim blend.</summary>
    bool transformMovedThisFrame;

    const float StrictAgentSnap = 0.75f;
    /// <summary>Chase/combat: cho phép snap NavMesh rộng hơn spawn (vẫn &lt; rìa 12–24m).</summary>
    const float CombatAgentSnap = 2.5f;
    const float AntiTeleportDistance = 4f;
    /// <summary>Stopping distance khi patrol/return — nhỏ, tránh phanh sớm rồi giật.</summary>
    const float PatrolStoppingDistance = 0.3f;
    /// <summary>Stopping distance khi chase — range thật do TickChase kiểm soát (hysteresis).</summary>
    const float ChaseStoppingDistance = 0.15f;
    /// <summary>Buffer ra khỏi AttackRange mới chạy lại — chống stop/start giựt giựt.</summary>
    const float AttackRangeHoldExitBuffer = 0.45f;
    const float AgentRecoverCooldown = 0.35f;

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

    /// <summary>
    /// Enemy đang là child của Patrol (local 0,0,0).
    /// Giữ pin vài frame, rồi unparent ra cage + bật agent (snap chặt) để tuần tra.
    /// </summary>
    public void BindSpawnToPatrolPoint(Transform patrolPoint, Transform moveParent, Transform[] allPatrols)
    {
        homePatrolPoint = patrolPoint;
        cageParent = moveParent != null ? moveParent : patrolPoint != null ? patrolPoint.parent : null;
        spawnPinnedToPatrol = true;
        pinHoldFrames = 20;
        agentModeActive = false;

        if (allPatrols != null && allPatrols.Length > 0)
        {
            patrolPoints = allPatrols;
        }

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        ForceDisableAgent();

        if (patrolPoint != null)
        {
            transform.SetParent(patrolPoint, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        lockedSpawnPosition = transform.position;
        lastFrameWorldPos = lockedSpawnPosition;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = true;
        }
    }

    /// <summary>Khóa theo world (fallback random spawn).</summary>
    public void LockSpawnPosition(Vector3 worldPosition)
    {
        spawnPinnedToPatrol = false;
        lockedSpawnPosition = worldPosition;
        transform.position = worldPosition;
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        agentModeActive = TryActivateAgentStrict(StrictAgentSnap);
    }

    // Compat cũ
    public void LockSpawnLocal(Transform parent, Vector3 localPosition)
    {
        if (parent != null)
        {
            // Tìm patrol gần local này? Fallback: parent + local.
            transform.SetParent(parent, false);
            transform.localPosition = localPosition;
            BindSpawnToPatrolPoint(null, parent, patrolPoints);
            transform.SetParent(parent, false);
            transform.localPosition = localPosition;
            lockedSpawnPosition = transform.position;
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

        if (spawnPinnedToPatrol)
        {
            // Giữ pin dưới Patrol (local 0) — agent vẫn tắt.
            PinToHomePatrol();
            originPosition = transform.position;
            ForceDisableAgent();
        }
        else
        {
            EnsureAgentOnNavMesh(logIfFailed: true);
            agentModeActive = agent != null && agent.enabled && agent.isOnNavMesh;
            originPosition = transform.position;
        }

        lastFrameWorldPos = transform.position;

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

        Debug.Log(
            $"[AI:{name}] STARTED | pinned={spawnPinnedToPatrol} parent={(transform.parent != null ? transform.parent.name : "null")} " +
            $"local={transform.localPosition} world={transform.position} " +
            $"agentMode={agentModeActive} patrol={(patrolPoints != null ? patrolPoints.Length : 0)}",
            this);

        EnterState(AIState.Spawn);
    }

    void LateUpdate()
    {
        // 1) Giữ pin dưới Patrol vài frame đầu (local = 0,0,0).
        if (spawnPinnedToPatrol && pinHoldFrames > 0)
        {
            pinHoldFrames--;
            ForceDisableAgent();
            PinToHomePatrol();
            lastFrameWorldPos = transform.position;
            return;
        }

        // 2) Hết pin hold → unparent ra cage + bật agent (snap chặt) 1 lần.
        if (spawnPinnedToPatrol && pinHoldFrames <= 0 && !agentModeActive)
        {
            ReleasePinAndTryAgent();
        }

        // 3) Anti-teleport: agent nhảy > 4m / frame → hủy agent, kéo lại.
        // Không re-pin về Patrol (sẽ đứng giật / bị ép local 0) — chỉ giữ chỗ đang đứng.
        if (agentModeActive && agent != null && agent.enabled)
        {
            float jump = Vector3.Distance(transform.position, lastFrameWorldPos);
            if (jump > AntiTeleportDistance)
            {
                Debug.LogWarning(
                    $"[AI:{name}] Anti-teleport: nhảy {jump:F1}m → tắt agent, trả về trước đó.",
                    this);
                transform.position = lastFrameWorldPos;
                ForceDisableAgent();
            }
        }

        lastFrameWorldPos = transform.position;
    }

    void PinToHomePatrol()
    {
        if (homePatrolPoint == null)
        {
            return;
        }

        if (transform.parent != homePatrolPoint)
        {
            transform.SetParent(homePatrolPoint, false);
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        lockedSpawnPosition = transform.position;
    }

    /// <summary>
    /// Bỏ pin khỏi Patrol, parent về cage, bật agent nếu mesh sát.
    /// </summary>
    void ReleasePinAndTryAgent()
    {
        spawnPinnedToPatrol = false;
        Vector3 world = transform.position;

        if (cageParent != null)
        {
            transform.SetParent(cageParent, true);
        }
        else
        {
            transform.SetParent(null, true);
        }

        transform.position = world;
        lockedSpawnPosition = world;
        originPosition = world;

        agentModeActive = TryActivateAgentStrict(StrictAgentSnap);
        if (agentModeActive)
        {
            Debug.Log($"[AI:{name}] Released pin → Agent mode ON @ {transform.position}", this);
        }
        else
        {
            ForceDisableAgent();
            Debug.LogWarning(
                $"[AI:{name}] Released pin, không có NavMesh sát → transform patrol.",
                this);
        }

        lastFrameWorldPos = transform.position;
    }

    bool UseTransformOnlyMovement()
    {
        return spawnPinnedToPatrol
               || !agentModeActive
               || agent == null
               || !agent.enabled
               || !agent.isOnNavMesh;
    }

    /// <summary>
    /// True only when NavMeshAgent APIs like remainingDistance / pathPending are safe to call.
    /// </summary>
    bool IsAgentNavigable()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh;
    }

    void ForceDisableAgent()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (agent != null && agent.enabled)
        {
            agent.enabled = false;
        }

        agentModeActive = false;
    }

    /// <summary>Bật agent + Warp chỉ khi mesh lệch ≤ maxSnap. Không sample rộng.</summary>
    bool TryActivateAgentStrict(float maxSnap, bool startStopped = true)
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (agent == null)
        {
            return false;
        }

        Vector3 world = transform.position;
        if (!NavMesh.SamplePosition(world, out NavMeshHit hit, maxSnap, NavMesh.AllAreas))
        {
            agent.enabled = false;
            agentModeActive = false;
            return false;
        }

        Vector3 flat = hit.position - world;
        flat.y = 0f;
        if (flat.magnitude > maxSnap || Mathf.Abs(hit.position.y - world.y) > maxSnap)
        {
            agent.enabled = false;
            agentModeActive = false;
            return false;
        }

        agent.enabled = true;
        agent.Warp(hit.position);

        // Nếu Warp kéo xa → hủy.
        if (Vector3.Distance(transform.position, world) > maxSnap + 0.1f)
        {
            transform.position = world;
            agent.enabled = false;
            agentModeActive = false;
            return false;
        }

        if (!agent.isOnNavMesh)
        {
            agent.enabled = false;
            agentModeActive = false;
            return false;
        }

        agentModeActive = true;
        agent.isStopped = startStopped;
        agent.updatePosition = true;
        // AI tự xoay (FaceTarget / ApplyMovementFacing) — tắt agent rotation để khỏi đụng/giật.
        agent.updateRotation = false;
        agent.autoBraking = true;
        float spd = MoveSpeed;
        if (spd > 0.01f)
        {
            agent.speed = spd;
        }

        return true;
    }

    bool TryEnableAgentForCombat(float maxSnap = CombatAgentSnap)
    {
        if (spawnPinnedToPatrol)
        {
            ReleasePinAndTryAgent();
        }

        // agentModeActive có thể stale (true nhưng agent đã disable) — luôn check isOnNavMesh.
        if (IsAgentNavigable())
        {
            agentModeActive = true;
            spawnPinnedToPatrol = false;
            agent.updatePosition = true;
            agent.isStopped = false;
            if (MoveSpeed > 0.01f)
            {
                agent.speed = MoveSpeed;
            }

            return true;
        }

        bool ok = TryActivateAgentStrict(maxSnap, startStopped: false);
        if (ok)
        {
            spawnPinnedToPatrol = false;
            agent.isStopped = false;
            agent.updatePosition = true;
            if (MoveSpeed > 0.01f)
            {
                agent.speed = MoveSpeed;
            }
        }
        else
        {
            agentModeActive = false;
        }

        return ok;
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
        if (!initializeFromEnemyData || enemyData == null || enemyData.baseStats == null || agent == null)
        {
            return;
        }

        // Không bật agent ở đây — PlaceExactlyAt/LockSpawnPosition quyết định enable.
        bool wasEnabled = agent.enabled;
        agent.speed = enemyData.baseStats.moveSpeed;
        agent.angularSpeed = enemyData.baseStats.turnSpeed;
        // KHÔNG set stoppingDistance = AttackRange: agent phanh sớm + TickChase Stop → giựt giựt.
        agent.stoppingDistance = ChaseStoppingDistance;
        agent.updateRotation = false;
        agent.autoBraking = true;

        // Giữ nguyên trạng thái enable (tránh bật lại → snap rìa).
        agent.enabled = wasEnabled;
    }

    void ConfigureAgentStoppingForState(AIState state)
    {
        if (agent == null)
        {
            return;
        }

        switch (state)
        {
            case AIState.Patrol:
            case AIState.ReturnToOrigin:
                agent.stoppingDistance = PatrolStoppingDistance;
                break;
            case AIState.Chase:
            case AIState.Retreat:
                agent.stoppingDistance = ChaseStoppingDistance;
                break;
            default:
                agent.stoppingDistance = ChaseStoppingDistance;
                break;
        }
    }

    void Update()
    {
        debugState = currentState;
        debugPoise = currentPoise;
        debugLastKnownHP = lastKnownHP;
        transformMovedThisFrame = false;

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

        if (isTackling && currentState != AIState.Dead)
        {
            if (currentState == AIState.Hurt || currentState == AIState.Stagger)
            {
                EndTackle();
            }
            else
            {
                StopAgent();
                FaceTarget();
                UpdateAnimatorMovement(0f);
                return;
            }
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
        if (!IsAgentNavigable()) return;
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
                if (spawnPinnedToPatrol)
                {
                    PinToHomePatrol();
                }

                break;
            case AIState.Idle:
                holdingInAttackRange = false;
                StopAgent();
                idleTimer = idleDuration;
                break;
            case AIState.Patrol:
                holdingInAttackRange = false;
                ConfigureAgentStoppingForState(AIState.Patrol);
                if (IsAgentNavigable())
                {
                    agent.speed = MoveSpeed * patrolSpeedRatio;
                    agent.isStopped = false;
                    GoToNextPatrolPoint();
                }
                // else: TickPatrolByTransform
                break;
            case AIState.Detect:
                holdingInAttackRange = false;
                TryEnableAgentForCombat(CombatAgentSnap);

                if (IsAgentNavigable())
                {
                    StopAgent();
                }

                FaceTarget();
                break;
            case AIState.Chase:
                holdingInAttackRange = false;
                ConfigureAgentStoppingForState(AIState.Chase);
                // Luôn thử bật agent (kể cả khi agentModeActive stale).
                TryEnableAgentForCombat(CombatAgentSnap);

                if (IsAgentNavigable())
                {
                    agent.speed = Mathf.Max(0.1f, MoveSpeed);
                    agent.isStopped = false;
                    agent.updatePosition = true;
                }

                lostSightTimer = 0f;
                break;
            case AIState.Attack:
                holdingInAttackRange = true;
                HoldAgentStill();
                BeginAttackPattern();
                break;
            case AIState.Hurt:
                EndTackle();
                holdingInAttackRange = false;
                HoldAgentStill();
                CancelActiveAttack();
                hitStunTimer = hitStunDuration;
                PlayHitAnimation();
                break;
            case AIState.Stagger:
                holdingInAttackRange = false;
                HoldAgentStill();
                CancelActiveAttack();
                staggerTimer = staggerDuration;
                if (animator != null && HasParam(StaggerHash, AnimatorControllerParameterType.Trigger))
                {
                    animator.ResetTrigger(AttackHash);
                    animator.SetTrigger(StaggerHash);
                }
                break;
            case AIState.Retreat:
                holdingInAttackRange = false;
                ConfigureAgentStoppingForState(AIState.Retreat);
                if (IsAgentNavigable())
                {
                    agent.speed = MoveSpeed * retreatSpeedRatio;
                    agent.isStopped = false;
                }
                break;
            case AIState.ReturnToOrigin:
                holdingInAttackRange = false;
                ConfigureAgentStoppingForState(AIState.ReturnToOrigin);
                if (!IsAgentNavigable() && Time.time >= nextAgentRecoverTime)
                {
                    nextAgentRecoverTime = Time.time + AgentRecoverCooldown;
                    TryEnableAgentForCombat(StrictAgentSnap);
                }

                if (IsAgentNavigable())
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

        // Agent mode: recover snap chặt, throttle — Warp mỗi frame = giựt.
        if (agentModeActive && agent != null && agent.enabled && !agent.isOnNavMesh
            && Time.time >= nextAgentRecoverTime)
        {
            nextAgentRecoverTime = Time.time + AgentRecoverCooldown;
            if (!TryActivateAgentStrict(StrictAgentSnap))
            {
                agentModeActive = false;
            }
        }

        // Idle: giữ đứng yên, không để residual path/velocity kéo.
        if (IsAgentNavigable() && (!agent.isStopped || agent.hasPath))
        {
            StopAgent();
        }

        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f && patrolPoints != null && patrolPoints.Length > 0)
        {
            EnterState(AIState.Patrol);
        }
    }

    void TickPatrol()
    {
        if (CheckDetect()) return;
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            EnterState(AIState.Idle);
            return;
        }

        // Ưu tiên NavMeshAgent khi agentModeActive.
        if (IsAgentNavigable())
        {
            float arriveTol = Mathf.Max(agent.stoppingDistance, patrolPointTolerance);

            if (!agent.pathPending
                && (agent.pathStatus == NavMeshPathStatus.PathInvalid
                    || (agent.pathStatus == NavMeshPathStatus.PathPartial && agent.remainingDistance < 0.05f)))
            {
                GoToNextPatrolPoint();
                return;
            }

            if (!agent.pathPending && agent.hasPath
                && agent.remainingDistance <= arriveTol)
            {
                EnterState(AIState.Idle);
                return;
            }

            // Hết path / không path nhưng đã gần điểm patrol (transform).
            if (!agent.pathPending && !agent.hasPath && patrolPoints != null && patrolPoints.Length > 0)
            {
                Transform nearest = patrolPoints[(patrolIndex + patrolPoints.Length - 1) % patrolPoints.Length];
                if (nearest != null && HorizontalDistance(nearest.position) <= arriveTol)
                {
                    EnterState(AIState.Idle);
                }
            }

            return;
        }

        // Fallback: transform (không có mesh sát).
        TickPatrolByTransform();
    }

    /// <summary>Patrol không cần NavMesh — di chuyển thẳng tới điểm (khi agent off-mesh).</summary>
    void TickPatrolByTransform()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return;
        }

        ForceDisableAgent();

        Transform target = patrolPoints[transformPatrolIndex % patrolPoints.Length];
        if (target == null)
        {
            transformPatrolIndex = (transformPatrolIndex + 1) % patrolPoints.Length;
            return;
        }

        // Đi theo world của Patrol (local của enemy sẽ đổi — đúng vì đang di chuyển).
        Vector3 dest = target.position;
        Vector3 pos = transform.position;
        Vector3 flat = dest - pos;
        flat.y = 0f;
        float dist = flat.magnitude;
        float speed = MoveSpeed * Mathf.Max(0.15f, patrolSpeedRatio);

        if (dist <= Mathf.Max(patrolPointTolerance, 0.35f))
        {
            // Snap đúng điểm đích (cùng parent hoặc world).
            if (target.parent == transform.parent)
            {
                transform.localPosition = target.localPosition;
            }
            else
            {
                transform.position = target.position;
            }

            transformPatrolIndex = (transformPatrolIndex + 1) % patrolPoints.Length;
            EnterState(AIState.Idle);
            return;
        }

        Vector3 step = flat.normalized * (speed * Time.deltaTime);
        if (step.magnitude > dist)
        {
            step = flat;
        }

        transform.position = pos + step;
        transformMovedThisFrame = step.sqrMagnitude > 0.000001f;

        if (flat.sqrMagnitude > 0.001f)
        {
            Vector3 facing = flipForward180 ? -flat : flat;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(facing.normalized),
                10f * Time.deltaTime);
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
            float rem = (IsAgentNavigable() && !agent.pathPending) ? agent.remainingDistance : -1f;
            float vel = IsAgentNavigable() ? agent.velocity.magnitude : 0f;
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
            holdingInAttackRange = false;
            EnterState(AIState.Attack);
            return;
        }

        // Hysteresis: vào hold khi <= AttackRange, chỉ chase lại khi > AttackRange + buffer.
        // Tránh StopAgent/SetDestination liên tục → giựt giựt như bị ép đứng.
        float holdExit = AttackRange + AttackRangeHoldExitBuffer;
        if (holdingInAttackRange)
        {
            if (distance > holdExit)
            {
                holdingInAttackRange = false;
            }
            else
            {
                HoldAgentStill();
                FaceTarget();
                return;
            }
        }
        else if (distance <= AttackRange)
        {
            holdingInAttackRange = true;
            HoldAgentStill();
            FaceTarget();
            return;
        }

        // --- Di chuyển thật về phía player ---
        ChaseMoveTowardPlayer(distance);
        FaceTarget();
    }

    /// <summary>
    /// Chase: ưu tiên NavMeshAgent; nếu off-mesh / isStopped kẹt / path hỏng → đi bằng transform.
    /// Tránh case "chỉ chạy animation, đứng yên".
    /// </summary>
    void ChaseMoveTowardPlayer(float distanceToPlayer)
    {
        if (player == null)
        {
            return;
        }

        if (!IsAgentNavigable())
        {
            if (Time.time >= nextAgentRecoverTime)
            {
                nextAgentRecoverTime = Time.time + AgentRecoverCooldown;
                TryEnableAgentForCombat(CombatAgentSnap);
            }
        }

        if (IsAgentNavigable())
        {
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.speed = Mathf.Max(0.1f, MoveSpeed);
            if (agent.isStopped)
            {
                agent.isStopped = false;
            }

            // SetDestination trực tiếp (không throttle fail im lặng).
            if (!agent.SetDestination(player.position))
            {
                // Path fail → transform bước.
                MoveTransformTowards(player.position, MoveSpeed);
                return;
            }

            // Agent on mesh nhưng đứng im + path hỏng: fallback transform.
            // PathPartial vẫn để agent tự xử lý — không ForceDisable.
            bool stuck = !agent.pathPending
                         && agent.velocity.sqrMagnitude < 0.04f
                         && distanceToPlayer > AttackRange + 0.25f;
            if (stuck && (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid))
            {
                MoveTransformTowards(player.position, MoveSpeed);
            }

            return;
        }

        // Không có NavMesh: vẫn dí player bằng transform (anim + vị trí khớp).
        MoveTransformTowards(player.position, MoveSpeed);
    }

    /// <summary>Di chuyển thẳng world (không agent). Dùng khi off-mesh hoặc path fail.</summary>
    void MoveTransformTowards(Vector3 worldTarget, float speed)
    {
        // Agent bật + updatePosition sẽ kéo transform về — tắt agent trước khi step.
        if (agent != null && agent.enabled)
        {
            ForceDisableAgent();
        }

        Vector3 pos = transform.position;
        Vector3 flat = worldTarget - pos;
        flat.y = 0f;
        float dist = flat.magnitude;
        if (dist < 0.001f)
        {
            return;
        }

        float stepLen = Mathf.Max(0.1f, speed) * Time.deltaTime;
        Vector3 step = flat.normalized * stepLen;
        if (step.magnitude > dist)
        {
            step = flat;
        }

        transform.position = pos + step;
        transformMovedThisFrame = step.sqrMagnitude > 0.000001f;
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
            RegisterCompletedAttackForTackle();
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
        if (IsAgentNavigable())
        {
            SetDestinationSafe(away, CombatAgentSnap);
        }
        else
        {
            MoveTransformTowards(away, MoveSpeed * retreatSpeedRatio);
        }
    }

    void TickReturn()
    {
        if (CheckDetect()) return;

        float arriveTol = Mathf.Max(PatrolStoppingDistance, patrolPointTolerance);

        // NavMesh path: only query remainingDistance when agent is active + on mesh.
        if (IsAgentNavigable())
        {
            if (!agent.pathPending
                && ((!agent.hasPath && HorizontalDistance(originPosition) <= arriveTol)
                    || (agent.hasPath && agent.remainingDistance <= arriveTol)))
            {
                EnterState(AIState.Idle);
            }

            return;
        }

        // Off-mesh: throttle re-activate (Warp mỗi frame = giựt), else walk by transform.
        if (Time.time >= nextAgentRecoverTime)
        {
            nextAgentRecoverTime = Time.time + AgentRecoverCooldown;
            if (TryActivateAgentStrict(StrictAgentSnap))
            {
                agent.speed = MoveSpeed;
                ConfigureAgentStoppingForState(AIState.ReturnToOrigin);
                SetDestinationSafe(originPosition);
                return;
            }

            ForceDisableAgent();
        }

        Vector3 pos = transform.position;
        Vector3 flat = originPosition - pos;
        flat.y = 0f;
        float dist = flat.magnitude;
        if (dist <= arriveTol)
        {
            EnterState(AIState.Idle);
            return;
        }

        Vector3 step = flat.normalized * (MoveSpeed * Time.deltaTime);
        if (step.magnitude > dist)
        {
            step = flat;
        }

        transform.position = pos + step;
        transformMovedThisFrame = step.sqrMagnitude > 0.000001f;
        if (flat.sqrMagnitude > 0.001f)
        {
            Vector3 facing = flipForward180 ? -flat : flat;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(facing.normalized),
                10f * Time.deltaTime);
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

        // Combat: thử bật agent tại chỗ (không kéo xa).
        if (spawnPinnedToPatrol || !agentModeActive || (agent != null && !agent.enabled))
        {
            TryEnableAgentForCombat(1.25f);
        }

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

    void RegisterCompletedAttackForTackle()
    {
        if (!useTackle || isTackling || currentState == AIState.Dead || currentState == AIState.Stagger)
        {
            EnterState(AIState.Chase);
            return;
        }

        attacksSinceLastTackle++;

        if (attacksSinceLastTackle >= attacksBeforeTackle && TryStartTackleAfterAttacks())
        {
            return;
        }

        EnterState(AIState.Chase);
    }

    void ApplyPoiseDamage(float dmg)
    {
        bool interruptingAttack = currentState == AIState.Attack || attackPhase != AttackPhase.None;

        if (MaxPoise > 0f)
        {
            currentPoise = Mathf.Max(0f, currentPoise - dmg);
            if (currentPoise <= 0f)
            {
                EnterState(AIState.Stagger);
                return;
            }
        }

        if (currentState == AIState.Stagger || currentState == AIState.Dead)
        {
            return;
        }

        // Đang căn đòn / đang Attack → luôn ưu tiên Hit, bỏ qua cooldown và ngưỡng damage nhỏ.
        if (interruptingAttack)
        {
            EnterState(AIState.Hurt);
            return;
        }

        if (dmg < 1f)
        {
            return;
        }

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

        if (Time.time < nextHurtAllowedAt)
        {
            return;
        }

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

        if (animator == null)
        {
            return;
        }

        if (HasParam(AttackHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(AttackHash);
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Basic Attack") || state.IsName("Attack"))
        {
            animator.Play("Hit", 0, 0f);
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

        animator.Play("Hit", 0, 0f);
        animator.Update(0f);
        lastHitReactionTime = Time.time;
    }

    void OnDied(CharacterHealth h)
    {
        EnterState(AIState.Dead);
    }

    // ---------- Movement utilities ----------
    void GoToNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return;
        }

        for (int attempt = 0; attempt < patrolPoints.Length; attempt++)
        {
            Transform target = patrolPoints[patrolIndex];
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            if (target == null)
            {
                continue;
            }

            // Destination: đúng Patrol, chỉ snap NavMesh rất gần (≤1.25m) — không kéo ra rìa.
            Vector3 dest = target.position;
            if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 1.25f, NavMesh.AllAreas))
            {
                Vector3 flat = hit.position - dest;
                flat.y = 0f;
                if (flat.magnitude <= 1.25f && Mathf.Abs(hit.position.y - dest.y) <= 1.5f)
                {
                    dest = hit.position;
                }
            }

            if (agentModeActive && agent != null && agent.enabled && agent.isOnNavMesh)
            {
                SetDestinationSafe(dest);
            }

            return;
        }
    }

    void SetDestinationSafe(Vector3 pos, float recoverSnap = -1f)
    {
        if (agent == null)
        {
            return;
        }

        if (!IsAgentNavigable())
        {
            // Chỉ recover snap chặt — tuyệt đối không sample 12–24m. Throttle để khỏi Warp giật.
            if (Time.time < nextAgentRecoverTime)
            {
                return;
            }

            nextAgentRecoverTime = Time.time + AgentRecoverCooldown;
            float snap = recoverSnap > 0f ? recoverSnap : StrictAgentSnap;
            if (!TryActivateAgentStrict(snap, startStopped: false) || !IsAgentNavigable())
            {
                return;
            }
        }

        agent.updatePosition = true;
        agent.isStopped = false;
        if (agent.speed < 0.05f && MoveSpeed > 0.05f)
        {
            agent.speed = MoveSpeed;
        }

        agent.SetDestination(pos);
    }

    /// <summary>
    /// Dừng hẳn + xóa path (Idle / Return xong / Knockback).
    /// </summary>
    void StopAgent()
    {
        if (!IsAgentNavigable())
        {
            return;
        }

        if (agent.hasPath)
        {
            agent.ResetPath();
        }

        agent.velocity = Vector3.zero;
        agent.isStopped = true;
    }

    /// <summary>
    /// Đứng yên trong combat (Chase hold / Attack) — không ResetPath mỗi frame (gây giựt vị trí).
    /// </summary>
    void HoldAgentStill()
    {
        if (!IsAgentNavigable())
        {
            return;
        }

        if (!agent.isStopped)
        {
            agent.isStopped = true;
        }

        if (agent.velocity.sqrMagnitude > 0.0001f)
        {
            agent.velocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Warp agent lên NavMesh gần nhất. Trả về true nếu isOnNavMesh.
    /// </summary>
    public bool EnsureAgentOnNavMesh(bool logIfFailed, float maxHorizontalPull = 3.5f)
    {
        if (agent == null)
        {
            return false;
        }

        if (!agent.enabled)
        {
            agent.enabled = true;
        }

        if (agent.isOnNavMesh)
        {
            return true;
        }

        Vector3 origin = transform.position;
        // Không sample quá rộng — tránh kéo enemy từ Patrol ra rìa nhà/terrain.
        float[] radii = maxHorizontalPull <= 1.5f
            ? new[] { 0.5f, 1f, 1.25f }
            : new[] { 1f, 2f, 3.5f };
        NavMeshHit best = default;
        bool found = false;
        float bestScore = float.MaxValue;
        float maxVerticalPull = maxHorizontalPull <= 1.5f ? 1.25f : 2f;

        for (int i = 0; i < radii.Length; i++)
        {
            if (!NavMesh.SamplePosition(origin, out NavMeshHit hit, radii[i], NavMesh.AllAreas))
            {
                continue;
            }

            float dy = Mathf.Abs(hit.position.y - origin.y);
            Vector3 flat = hit.position - origin;
            flat.y = 0f;
            if (dy > maxVerticalPull || flat.magnitude > maxHorizontalPull)
            {
                continue;
            }

            float score = dy * 4f + flat.magnitude;
            if (score < bestScore)
            {
                bestScore = score;
                best = hit;
                found = true;
            }

            if (flat.magnitude <= 1f)
            {
                break;
            }
        }

        if (!found)
        {
            if (logIfFailed)
            {
                Debug.LogWarning(
                    $"[AI:{name}] Không tìm thấy NavMesh gần {origin} (≤{maxHorizontalPull}m). " +
                    "Đứng im — bake NavMesh / đặt Patrol trên mesh xanh.",
                    this);
            }

            return false;
        }

        agent.Warp(best.position);
        transform.position = best.position;

        if (!agent.isOnNavMesh && logIfFailed)
        {
            Debug.LogWarning($"[AI:{name}] Warp xong vẫn !isOnNavMesh @ {best.position}.", this);
        }

        return agent.isOnNavMesh;
    }

    static float HorizontalDistanceXZ(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
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
        // Blend theo vận tốc / bước thật — không fake run khi đứng im.
        float moving = 0f;
        if (IsAgentNavigable() && !agent.isStopped)
        {
            float spd = Mathf.Max(0.1f, agent.speed);
            moving = Mathf.Clamp01(agent.velocity.magnitude / spd);
        }
        else if (transformMovedThisFrame)
        {
            moving = currentState == AIState.Patrol ? 0.55f : 0.9f;
        }

        if (moving < 0.05f)
        {
            return 0f;
        }

        switch (currentState)
        {
            case AIState.Chase:
            case AIState.Retreat:
            case AIState.ReturnToOrigin:
                return Mathf.Lerp(0.5f, 2f, moving);
            case AIState.Patrol:
                return Mathf.Lerp(0.25f, 1f, moving);
            default:
                return 0f;
        }
    }

    void UpdateAnimatorMovement(float targetBlend)
    {
        if (animator == null) return;

        Vector3 local = Vector3.zero;
        if (IsAgentNavigable() && !agent.isStopped
            && agent.velocity.sqrMagnitude > movingThreshold * movingThreshold)
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

    public void Anim_OnTackleFinished()
    {
        if (!isTackling)
        {
            return;
        }

        EndTackle();

        if (currentState != AIState.Dead && currentState != AIState.Hurt && currentState != AIState.Stagger)
        {
            EnterState(AIState.Chase);
        }
    }

    bool TryStartTackleAfterAttacks()
    {
        if (!useTackle || isTackling || animator == null || player == null)
        {
            return false;
        }

        if (Time.time < nextTackleTime)
        {
            return false;
        }

        float distance = HorizontalDistance(player.position);
        if (distance > tackleRange)
        {
            return false;
        }

        if (!HasParam(TackleHash, AnimatorControllerParameterType.Trigger))
        {
            return false;
        }

        attacksSinceLastTackle = 0;
        nextTackleTime = Time.time + tackleCooldown;
        isTackling = true;
        CancelActiveAttack();
        hitStunTimer = 0f;
        StopAgent();
        FaceTarget();
        animator.ResetTrigger(TackleHash);
        animator.SetTrigger(TackleHash);
        return true;
    }

    void EndTackle()
    {
        isTackling = false;

        EnemyPushHitbox pushHitbox = GetComponentInChildren<EnemyPushHitbox>(true);
        if (pushHitbox != null)
        {
            pushHitbox.CloseHitbox();
        }

        if (animator != null && HasParam(TackleHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(TackleHash);
        }
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
