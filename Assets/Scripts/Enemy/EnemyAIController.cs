using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// Data-driven enemy AI FSM theo briefing ASTRA EDEN.
/// State: Spawn → Idle → Patrol → Detect → Chase → Attack → Hurt → Stagger → Retreat/Evade → ReturnToOrigin → Dead.
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
        // Append only: giữ nguyên numeric value của các state cũ đã serialize.
        Evade,
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

    [Header("Combat Response")]
    [Tooltip("Bị combo đủ số hit trong một cửa sổ ngắn thì xếp hàng né sau Hurt/Stagger.")]
    [SerializeField] private bool enablePressureEvade = true;
    [SerializeField, Min(1)] private int hitsToEvade = 3;
    [SerializeField, Min(0.05f)] private float hitPressureWindow = 1.1f;
    [SerializeField, Range(0f, 1f)] private float evadeChance = 0.7f;
    [SerializeField, Min(0.25f)] private float evadeDistance = 2.8f;
    [SerializeField, Min(0.1f)] private float evadeDuration = 0.55f;
    [SerializeField, Min(0f)] private float evadeCooldown = 2.5f;
    [SerializeField, Range(0.5f, 2.5f)] private float evadeSpeedRatio = 1.35f;

    [Tooltip("HP thấp thì rút ra xa, quan sát một lúc rồi quay lại chiến đấu.")]
    [SerializeField] private bool enableLowHealthRetreat = true;
    [SerializeField, Range(0.01f, 0.95f)] private float lowHealthThreshold = 0.25f;
    [SerializeField, Min(1f)] private float lowHealthRetreatDistance = 6f;
    [SerializeField, Min(0.1f)] private float lowHealthRetreatDuration = 2f;
    [SerializeField, Min(0f)] private float lowHealthRetreatCooldown = 8f;
    [SerializeField] private bool lowHealthRetreatOnlyOnce = true;

    [Header("Hurt / Stagger")]
    [Tooltip("Bật anim Hit khi mất HP nhưng poise còn.")]
    [SerializeField] private bool useHitAnimation = true;
    [SerializeField, Min(0f)] private float hitStunDuration = 0.1f;
    [Tooltip("Tối thiểu giữa các flinch (Hurt) liên tiếp — chống spam đứng đơ khi bị combo nhỏ.")]
    [SerializeField, Min(0f)] private float hurtCooldown = 0f;
    [Tooltip("Stagger duration khi poise vỡ.")]
    [SerializeField, Min(0f)] private float staggerDuration = 0.1f;
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
    [Tooltip("Tìm lại Player định kỳ nếu reference bị mất (scene load / respawn).")]
    [SerializeField, Min(0.25f)] private float playerResolveInterval = 0.75f;
    [Tooltip("Dùng Physics.CheckSphere (kiểu tutorial) làm fallback khi EnemySensor null hoặc player sát.")]
    [SerializeField] private bool useProximitySphereFallback = true;

    [Header("Free Patrol (khi không có patrol points)")]
    [Tooltip("Khi không gán patrol points, tuần tra random quanh origin giống tutorial walkPoint.")]
    [SerializeField] private bool enableRandomWalkWhenNoPatrol = true;
    [SerializeField, Min(1f)] private float randomWalkRange = 8f;
    [SerializeField, Min(0.5f)] private float randomWalkArriveDistance = 1f;

    [Header("Stability")]
    [Tooltip("Timeout cứng cho state Attack — tránh kẹt animation/phase.")]
    [SerializeField, Min(1f)] private float attackStateTimeout = 6f;
    [Tooltip("Khoảng cách tối thiểu giữa 2 lần SetDestination khi chase (giảm giật NavMesh).")]
    [SerializeField, Min(0.05f)] private float chaseDestinationInterval = 0.12f;

    [Header("Debug")]
    [SerializeField] private AIState debugState;
    [SerializeField] private float debugPoise;
    [SerializeField] private float debugLastKnownHP;
    [SerializeField] private bool drawAttackGizmo = true;
    [Tooltip("Bật để log mọi state transition + lý do vào/thoát Attack/Chase ra Console.")]
    [SerializeField] private bool debugLogStateMachine = false;
    [Tooltip("Bật để log distance, cooldown, agent.remainingDistance mỗi frame trong Chase. Spam nhiều — chỉ bật khi cần.")]
    [SerializeField] private bool debugLogChaseTick = false;
    [Tooltip("Log đếm combo, quyết định né và retreat HP thấp.")]
    [SerializeField] private bool debugLogCombatResponse = false;

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
    float nextPlayerResolveTime;
    float nextChaseDestinationTime;
    Vector3 randomWalkPoint;
    bool randomWalkPointSet;
    bool playerInSightRange;
    bool playerInAttackRange;

    // Combat response runtime state.
    int pressureHitCount;
    float pressureWindowExpiresAt;
    bool evadeQueued;
    float nextEvadeAllowedAt;
    Vector3 evadeDestination;
    bool evadeDestinationValid;

    bool lowHealthRetreatActive;
    bool lowHealthRetreatCompleted;
    bool lowHealthRetreatHolding;
    float lowHealthRetreatHoldUntil;
    float lowHealthRetreatStartedAt;
    float nextLowHealthRetreatAllowedAt;
    float nextRetreatDestinationRefreshAt;
    Vector3 lowHealthRetreatDestination;
    bool lowHealthRetreatDestinationValid;

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

        TryResolvePlayer(force: true);

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

        if (sensor != null)
        {
            sensor.Configure(enemyData);
            // Model -Z forward: sensor FOV + Aillieo LOS2D facing phải cùng hướng nhìn thực tế.
            sensor.SetFlipForward(flipForward180);
        }

        // Optional Aillieo package bridge (if present / enabled on sensor).
        EnemyLOS2DBridge los2d = GetComponent<EnemyLOS2DBridge>();
        if (los2d != null)
        {
            los2d.ConfigureFromSensor(sensor, flipForward180);
        }

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
            case AIState.Evade:
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

        TryResolvePlayer(force: false);
        RefreshRangeFlags();

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

        // Player chết → ngừng combat (attack/chase/detect/retreat/tackle).
        if (IsPlayerDeadForAi()
            && currentState != AIState.Dead
            && currentState != AIState.Hurt
            && currentState != AIState.Stagger
            && currentState != AIState.Idle
            && currentState != AIState.Patrol
            && currentState != AIState.ReturnToOrigin
            && currentState != AIState.Spawn)
        {
            EndTackle();
            CancelActiveAttack();
            holdingInAttackRange = false;
            EnterState(AIState.Idle);
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
                // Giống tutorial AttackPlayer: đứng yên + quay về player trong lúc tackle.
                HoldAgentStill();
                FaceTarget();
                UpdateAnimatorMovement(0f);
                return;
            }
        }

        if (hitStunTimer > 0f) hitStunTimer -= Time.deltaTime;
        if (staggerTimer > 0f) staggerTimer -= Time.deltaTime;

        switch (currentState)
        {
            case AIState.Spawn: TickSpawn(); break;
            case AIState.Idle: TickIdle(); break;
            case AIState.Patrol: TickPatrol(); break;
            case AIState.Detect: TickDetect(); break;
            case AIState.Chase: TickChase(); break;
            case AIState.Attack: TickAttack(); break;
            case AIState.Hurt: TickHurt(); break;
            case AIState.Stagger: TickStagger(); break;
            case AIState.Retreat: TickRetreat(); break;
            case AIState.Evade: TickEvade(); break;
            case AIState.ReturnToOrigin: TickReturn(); break;
        }

        ApplyMovementFacing();
        UpdateAnimatorMovement(GetTargetBlendForState());
    }

    /// <summary>Tìm Player theo tag. Tutorial dùng Find("PlayerObj") — project dùng tag "Player".</summary>
    void TryResolvePlayer(bool force)
    {
        // Unity overloaded == handles destroyed objects as null.
        if (!force && player != null)
        {
            if (!player.gameObject.activeInHierarchy)
            {
                player = null;
            }
            else
            {
                return;
            }
        }

        if (!force && Time.time < nextPlayerResolveTime)
        {
            return;
        }

        nextPlayerResolveTime = Time.time + playerResolveInterval;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    /// <summary>
    /// Cập nhật flag sight/attack range kiểu tutorial (CheckSphere + distance).
    /// Dùng cho debug gizmo + fallback detect.
    /// </summary>
    void RefreshRangeFlags()
    {
        playerInSightRange = false;
        playerInAttackRange = false;

        if (player == null)
        {
            return;
        }

        float distance = HorizontalDistance(player.position);
        playerInAttackRange = distance <= AttackRange;
        playerInSightRange = distance <= SightRangeForDetection();

        // Sphere layer check (tutorial): chính xác hơn khi player có collider trên layer Player.
        if (useProximitySphereFallback && playerLayer.value != 0)
        {
            if (Physics.CheckSphere(transform.position, AttackRange, playerLayer, QueryTriggerInteraction.Ignore))
            {
                playerInAttackRange = true;
                playerInSightRange = true;
            }
            else if (Physics.CheckSphere(transform.position, SightRangeForDetection(), playerLayer, QueryTriggerInteraction.Ignore))
            {
                playerInSightRange = true;
            }
        }
    }

    float SightRangeForDetection()
    {
        if (sensor != null)
        {
            return Mathf.Max(sensor.SightRange, sensor.HearingRange);
        }

        return enemyData != null ? enemyData.sightRange : 14f;
    }

    /// <summary>Xoay model theo hướng velocity của agent. Cần khi flipForward180 = true (đã tắt agent.updateRotation),
    /// hoặc khi state Patrol/Return/Retreat không có target để FaceTarget.</summary>
    void ApplyMovementFacing()
    {
        if (!IsAgentNavigable()) return;
        if (agent.velocity.sqrMagnitude <= movingThreshold * movingThreshold) return;

        // Khi đang chase/attack/detect → có player, đã có FaceTarget xử lý.
        bool wantsFaceTarget = currentState == AIState.Chase
                               || currentState == AIState.Attack
                               || currentState == AIState.Detect
                               || currentState == AIState.Evade;
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
                randomWalkPointSet = false;
                ConfigureAgentStoppingForState(AIState.Patrol);
                if (IsAgentNavigable())
                {
                    agent.speed = MoveSpeed * patrolSpeedRatio;
                    agent.isStopped = false;
                    if (patrolPoints != null && patrolPoints.Length > 0)
                    {
                        GoToNextPatrolPoint();
                    }
                }
                // else: TickPatrolByTransform / TickRandomWalkPatrol
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
                TryEnableAgentForCombat(CombatAgentSnap);
                if (IsAgentNavigable())
                {
                    agent.speed = MoveSpeed * retreatSpeedRatio;
                    agent.isStopped = false;
                }
                break;
            case AIState.Evade:
                EnterEvade();
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
                evadeQueued = false;
                evadeDestinationValid = false;
                lowHealthRetreatActive = false;
                lowHealthRetreatDestinationValid = false;
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
        // Tutorial: !sight && !attack → Patrol; sight → Chase/Detect.
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
        if (idleTimer > 0f)
        {
            return;
        }

        bool hasPatrolPoints = patrolPoints != null && patrolPoints.Length > 0;
        if (hasPatrolPoints || enableRandomWalkWhenNoPatrol)
        {
            EnterState(AIState.Patrol);
        }
        else
        {
            // Không có điểm tuần tra → idle lại, tránh spam transition.
            idleTimer = idleDuration;
        }
    }

    void TickPatrol()
    {
        if (CheckDetect()) return;

        bool hasPatrolPoints = patrolPoints != null && patrolPoints.Length > 0;
        if (!hasPatrolPoints)
        {
            if (!enableRandomWalkWhenNoPatrol)
            {
                EnterState(AIState.Idle);
                return;
            }

            TickRandomWalkPatrol();
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

    /// <summary>
    /// Patrol random quanh origin (tutorial SearchWalkPoint) khi không có waypoint.
    /// </summary>
    void TickRandomWalkPatrol()
    {
        if (!randomWalkPointSet)
        {
            SearchRandomWalkPoint();
        }

        if (!randomWalkPointSet)
        {
            EnterState(AIState.Idle);
            return;
        }

        float dist = HorizontalDistance(randomWalkPoint);
        if (dist <= randomWalkArriveDistance)
        {
            randomWalkPointSet = false;
            EnterState(AIState.Idle);
            return;
        }

        if (IsAgentNavigable())
        {
            if (agent.isStopped)
            {
                agent.isStopped = false;
            }

            agent.speed = MoveSpeed * patrolSpeedRatio;
            SetDestinationSafe(randomWalkPoint);
            return;
        }

        MoveTransformTowards(randomWalkPoint, MoveSpeed * patrolSpeedRatio);
    }

    void SearchRandomWalkPoint()
    {
        Vector3 center = originPosition.sqrMagnitude > 0.01f ? originPosition : transform.position;
        for (int i = 0; i < 8; i++)
        {
            float randomZ = Random.Range(-randomWalkRange, randomWalkRange);
            float randomX = Random.Range(-randomWalkRange, randomWalkRange);
            Vector3 candidate = new Vector3(center.x + randomX, center.y, center.z + randomZ);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                randomWalkPoint = hit.position;
                randomWalkPointSet = true;
                return;
            }

            // Fallback raycast xuống ground (tutorial style).
            if (Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 6f))
            {
                randomWalkPoint = groundHit.point;
                randomWalkPointSet = true;
                return;
            }
        }

        randomWalkPointSet = false;
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
        if (player == null)
        {
            EnterState(AIState.ReturnToOrigin);
            return;
        }

        float distance = HorizontalDistance(player.position);
        bool canSee = CanSensePlayer(out _);

        if (debugLogChaseTick)
        {
            float rem = (IsAgentNavigable() && !agent.pathPending) ? agent.remainingDistance : -1f;
            float vel = IsAgentNavigable() ? agent.velocity.magnitude : 0f;
            bool inRange = distance <= AttackRange || playerInAttackRange;
            bool cdReady = attackCooldownTimer <= 0f;
            Debug.Log($"[AI:{name}] Chase tick | dist={distance:F2} atkRange={AttackRange:F2} inRange={inRange} cdReady={cdReady} cd={attackCooldownTimer:F2} agentVel={vel:F2} remDist={rem:F2} canSee={canSee} sphereSight={playerInSightRange}", this);
        }

        // Chỉ reset lost-sight khi sensor thật sự còn thấy (FOV+LOS / hearing+LOS / contact).
        // Không dùng sphere range — tránh “nhớ” player sau tường.
        if (canSee)
        {
            lostSightTimer = 0f;
        }
        else
        {
            lostSightTimer += Time.deltaTime;
            // Mất LOS (núp tường): quên nhanh hơn full loseTargetTime (tối đa 1.25s hoặc 35% lose time).
            float loseBehindCover = Mathf.Min(loseTargetTime, Mathf.Max(0.75f, loseTargetTime * 0.35f));
            bool losBlocked = sensor != null && !sensor.HasLineOfSightTo(player);
            float limit = losBlocked ? loseBehindCover : loseTargetTime;
            if (lostSightTimer >= limit || distance > AggroKeepRange)
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

        // Tutorial: playerInAttackRange && playerInSightRange → Attack.
        if (CanStartAttack(distance) || (playerInAttackRange && attackCooldownTimer <= 0f && distance <= AttackRange + 0.35f))
        {
            holdingInAttackRange = false;
            EnterState(AIState.Attack);
            return;
        }

        // Hysteresis: vào hold khi <= AttackRange, chỉ chase lại khi > AttackRange + buffer.
        // Tránh StopAgent/SetDestination liên tục → giựt giựt như bị ép đứng.
        // Khi đang cooldown: đứng + LookAt player (giống alreadyAttacked trong tutorial).
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
        else if (distance <= AttackRange || playerInAttackRange)
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

            // Throttle SetDestination — gọi mỗi frame dễ giật path / velocity reset.
            bool needNewPath = !agent.hasPath
                               || agent.pathStatus == NavMeshPathStatus.PathInvalid
                               || Time.time >= nextChaseDestinationTime;
            if (needNewPath)
            {
                nextChaseDestinationTime = Time.time + chaseDestinationInterval;
                if (!agent.SetDestination(player.position))
                {
                    // Path fail → transform bước.
                    MoveTransformTowards(player.position, MoveSpeed);
                    return;
                }
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
        // Tutorial AttackPlayer: agent đứng yên + LookAt player suốt swing.
        HoldAgentStill();
        if (player != null)
        {
            FaceTarget();
        }

        // Failsafe: kẹt Attack quá lâu (anim event mất / phase timer lỗi).
        if (stateTimer >= attackStateTimeout)
        {
            if (debugLogStateMachine)
            {
                Debug.LogWarning($"[AI:{name}] Attack timeout ({attackStateTimeout:F1}s) → force Chase.", this);
            }

            CancelActiveAttack();
            attackCooldownTimer = Mathf.Max(attackCooldownTimer, AttackCooldown * 0.5f);
            EnterState(AIState.Chase);
            return;
        }

        // Mất player giữa đòn → kết thúc sớm, không spam.
        if (player == null)
        {
            CancelActiveAttack();
            attackCooldownTimer = AttackCooldown;
            EnterState(AIState.ReturnToOrigin);
            return;
        }

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
            attackCooldownTimer = 0f;
            RegisterCompletedAttackForTackle();
        }
        else if (attackPhase == AttackPhase.None && attackPhaseTimer <= 0f)
        {
            // Pattern null / phase hỏng → không đứng Attack mãi.
            attackCooldownTimer = Mathf.Max(attackCooldownTimer, AttackCooldown * 0.35f);
            EnterState(AIState.Chase);
        }
    }

    void TickHurt()
    {
        // Hard timeout — phòng trường hợp hitStunTimer bị reset bởi knockback/dame liên tiếp.
        if (hitStunTimer <= 0f || stateTimer >= hitStunDuration + 0.1f)
        {
            EnterPostHitResponse();
        }
    }

    void TickStagger()
    {
        if (staggerTimer <= 0f)
        {
            currentPoise = MaxPoise;
            EnterPostHitResponse();
        }
        else if (poiseRegenAfterStagger > 0f)
        {
            currentPoise = Mathf.Min(MaxPoise, currentPoise + poiseRegenAfterStagger * Time.deltaTime);
        }
    }

    void TickRetreat()
    {
        if (lowHealthRetreatActive)
        {
            TickLowHealthRetreat();
            return;
        }

        TickStandardRetreat();
    }

    void TickStandardRetreat()
    {
        if (player == null)
        {
            EnterState(AIState.ReturnToOrigin);
            return;
        }

        float distance = HorizontalDistance(player.position);
        if (distance >= AttackRange * 0.9f)
        {
            EnterState(CanStartAttack(distance) ? AIState.Attack : AIState.Chase);
            return;
        }

        Vector3 awayDirection = transform.position - player.position;
        awayDirection.y = 0f;
        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = -transform.forward;
        }

        Vector3 away = transform.position + awayDirection.normalized * 2.5f;
        if (IsAgentNavigable())
        {
            SetDestinationSafe(away, CombatAgentSnap);
        }
        else if (IsDirectMovementClear(away))
        {
            MoveTransformTowards(away, MoveSpeed * retreatSpeedRatio);
        }

        FaceTarget();
    }

    void TickEvade()
    {
        if (player == null)
        {
            EnterState(AIState.ReturnToOrigin);
            return;
        }

        FaceTarget();

        bool arrived = evadeDestinationValid && HorizontalDistance(evadeDestination) <= 0.25f;
        if (!arrived && IsAgentNavigable() && evadeDestinationValid && !agent.pathPending)
        {
            arrived = !agent.hasPath || agent.remainingDistance <= Mathf.Max(0.25f, agent.stoppingDistance + 0.1f);
        }

        if (stateTimer >= evadeDuration || arrived)
        {
            if (debugLogCombatResponse)
            {
                Debug.Log($"[AI:{name}] Evade hoàn tất → Chase.", this);
            }

            evadeDestinationValid = false;
            EnterState(AIState.Chase);
            return;
        }

        if (!evadeDestinationValid)
        {
            return;
        }

        if (IsAgentNavigable())
        {
            agent.speed = Mathf.Max(0.1f, MoveSpeed * evadeSpeedRatio);
            if (agent.isStopped) agent.isStopped = false;
        }
        else if (IsDirectMovementClear(evadeDestination))
        {
            MoveTransformTowards(evadeDestination, MoveSpeed * evadeSpeedRatio);
        }
    }

    void EnterEvade()
    {
        EndTackle();
        holdingInAttackRange = false;
        CancelActiveAttack();
        ConfigureAgentStoppingForState(AIState.Evade);
        TryEnableAgentForCombat(CombatAgentSnap);

        if (IsAgentNavigable())
        {
            agent.speed = Mathf.Max(0.1f, MoveSpeed * evadeSpeedRatio);
            agent.isStopped = false;
        }

        evadeDestinationValid = TryChooseEvadeDestination(out evadeDestination);
        if (evadeDestinationValid && IsAgentNavigable())
        {
            SetDestinationSafe(evadeDestination, CombatAgentSnap);
        }

        if (debugLogCombatResponse)
        {
            string destination = evadeDestinationValid ? evadeDestination.ToString("F2") : "không tìm thấy điểm hợp lệ";
            Debug.Log($"[AI:{name}] Evade bắt đầu | destination={destination}", this);
        }
    }

    bool TryChooseEvadeDestination(out Vector3 destination)
    {
        destination = transform.position;
        if (player == null) return false;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude <= 0.0001f)
        {
            toPlayer = flipForward180 ? -transform.forward : transform.forward;
        }
        toPlayer.Normalize();

        Vector3 away = -toPlayer;
        Vector3 right = Vector3.Cross(Vector3.up, toPlayer).normalized;
        float sideSign = Random.value < 0.5f ? -1f : 1f;

        Vector3[] directions =
        {
            (right * sideSign * 0.85f + away * 0.35f).normalized,
            (right * -sideSign * 0.85f + away * 0.35f).normalized,
            (right * sideSign * 0.45f + away * 0.75f).normalized,
            away,
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 candidate = ClampDestinationToCombatLeash(transform.position + directions[i] * evadeDistance);
            if (TryGetSafeCombatDestination(candidate, 1.25f, out destination))
            {
                return true;
            }
        }

        return false;
    }

    void EnterPostHitResponse()
    {
        if (health != null && health.IsDead)
        {
            EnterState(AIState.Dead);
            return;
        }

        if (player == null)
        {
            EnterState(AIState.ReturnToOrigin);
            return;
        }

        // Retreat HP thấp đang chạy mà bị đánh tiếp thì tiếp tục retreat.
        if (lowHealthRetreatActive)
        {
            EnterState(AIState.Retreat);
            return;
        }

        if (ShouldStartLowHealthRetreat())
        {
            StartLowHealthRetreat();
            return;
        }

        if (enablePressureEvade && evadeQueued)
        {
            evadeQueued = false;
            EnterState(AIState.Evade);
            return;
        }

        EnterState(AIState.Chase);
    }

    bool ShouldStartLowHealthRetreat()
    {
        if (!enableLowHealthRetreat || lowHealthRetreatActive || player == null || isTackling) return false;
        if (health == null || health.IsDead || health.RuntimeStats == null) return false;
        if (lowHealthRetreatOnlyOnce && lowHealthRetreatCompleted) return false;
        if (Time.time < nextLowHealthRetreatAllowedAt) return false;

        float maxHp = Mathf.Max(0.01f, health.RuntimeStats.maxHP);
        float hpRatio = Mathf.Clamp01(health.RuntimeStats.currentHP / maxHp);
        return hpRatio <= lowHealthThreshold;
    }

    void StartLowHealthRetreat()
    {
        lowHealthRetreatActive = true;
        lowHealthRetreatHolding = false;
        lowHealthRetreatDestinationValid = false;
        lowHealthRetreatStartedAt = Time.time;
        nextRetreatDestinationRefreshAt = 0f;
        nextLowHealthRetreatAllowedAt = Time.time + lowHealthRetreatCooldown;
        evadeQueued = false;

        if (debugLogCombatResponse)
        {
            float maxHp = health != null && health.RuntimeStats != null ? Mathf.Max(0.01f, health.RuntimeStats.maxHP) : 1f;
            float hpRatio = health != null && health.RuntimeStats != null ? health.RuntimeStats.currentHP / maxHp : 0f;
            Debug.Log($"[AI:{name}] Low-health Retreat kích hoạt | HP={hpRatio:P0}", this);
        }

        EnterState(AIState.Retreat);
    }

    void TickLowHealthRetreat()
    {
        if (player == null)
        {
            FinishLowHealthRetreat(AIState.ReturnToOrigin);
            return;
        }

        float distance = HorizontalDistance(player.position);
        float resumeDistance = Mathf.Max(0.5f, lowHealthRetreatDistance - 0.75f);

        if (lowHealthRetreatHolding && distance < resumeDistance)
        {
            lowHealthRetreatHolding = false;
            lowHealthRetreatDestinationValid = false;
            nextRetreatDestinationRefreshAt = 0f;
        }

        if (!lowHealthRetreatHolding && distance >= lowHealthRetreatDistance)
        {
            lowHealthRetreatHolding = true;
            lowHealthRetreatHoldUntil = Time.time + lowHealthRetreatDuration;
            lowHealthRetreatDestinationValid = false;
            HoldAgentStill();

            if (debugLogCombatResponse)
            {
                Debug.Log($"[AI:{name}] Đã tạo khoảng cách {distance:F1}m, chờ {lowHealthRetreatDuration:F1}s.", this);
            }
        }

        if (lowHealthRetreatHolding)
        {
            HoldAgentStill();
            FaceTarget();
            if (Time.time >= lowHealthRetreatHoldUntil)
            {
                FinishLowHealthRetreat(AIState.Chase);
            }
            return;
        }

        // Failsafe: địa hình không cho lùi đủ xa thì không kẹt Retreat mãi.
        float hardTimeout = Mathf.Max(4f, lowHealthRetreatDuration + 4f);
        if (Time.time >= lowHealthRetreatStartedAt + hardTimeout)
        {
            FinishLowHealthRetreat(AIState.Chase);
            return;
        }

        bool needsDestination = !lowHealthRetreatDestinationValid || Time.time >= nextRetreatDestinationRefreshAt;
        if (!needsDestination && IsAgentNavigable() && !agent.pathPending)
        {
            needsDestination = agent.pathStatus == NavMeshPathStatus.PathInvalid
                               || (!agent.hasPath && HorizontalDistance(lowHealthRetreatDestination) > 0.35f)
                               || HorizontalDistance(lowHealthRetreatDestination) <= 0.35f;
        }

        if (needsDestination)
        {
            nextRetreatDestinationRefreshAt = Time.time + 0.4f;
            lowHealthRetreatDestinationValid = TryChooseLowHealthRetreatDestination(distance, out lowHealthRetreatDestination);
            if (lowHealthRetreatDestinationValid && IsAgentNavigable())
            {
                agent.speed = Mathf.Max(0.1f, MoveSpeed * retreatSpeedRatio);
                SetDestinationSafe(lowHealthRetreatDestination, CombatAgentSnap);
            }
        }

        if (lowHealthRetreatDestinationValid)
        {
            if (IsAgentNavigable())
            {
                agent.speed = Mathf.Max(0.1f, MoveSpeed * retreatSpeedRatio);
                if (agent.isStopped) agent.isStopped = false;
            }
            else if (IsDirectMovementClear(lowHealthRetreatDestination))
            {
                MoveTransformTowards(lowHealthRetreatDestination, MoveSpeed * retreatSpeedRatio);
            }
        }
    }

    bool TryChooseLowHealthRetreatDestination(float currentDistance, out Vector3 destination)
    {
        destination = transform.position;
        if (player == null) return false;

        Vector3 away = transform.position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude <= 0.0001f) away = -transform.forward;
        away.Normalize();

        float needed = Mathf.Max(2.5f, lowHealthRetreatDistance - currentDistance + 1f);
        float travel = Mathf.Min(lowHealthRetreatDistance, needed);
        Vector3[] directions =
        {
            away,
            Quaternion.Euler(0f, 30f, 0f) * away,
            Quaternion.Euler(0f, -30f, 0f) * away,
            Quaternion.Euler(0f, 60f, 0f) * away,
            Quaternion.Euler(0f, -60f, 0f) * away,
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 candidate = ClampDestinationToCombatLeash(transform.position + directions[i] * travel);
            if (TryGetSafeCombatDestination(candidate, 1.75f, out destination))
            {
                return true;
            }
        }

        return false;
    }

    void FinishLowHealthRetreat(AIState nextState)
    {
        lowHealthRetreatActive = false;
        lowHealthRetreatHolding = false;
        lowHealthRetreatDestinationValid = false;
        lowHealthRetreatCompleted = true;

        if (debugLogCombatResponse)
        {
            Debug.Log($"[AI:{name}] Low-health Retreat hoàn tất → {nextState}.", this);
        }

        EnterState(nextState);
    }

    Vector3 ClampDestinationToCombatLeash(Vector3 candidate)
    {
        float leash = Mathf.Min(Mathf.Max(2f, returnDistance), Mathf.Max(2f, AggroKeepRange));
        float safeRadius = Mathf.Max(2f, leash * 0.85f);
        Vector3 fromOrigin = candidate - originPosition;
        fromOrigin.y = 0f;
        if (fromOrigin.magnitude > safeRadius)
        {
            Vector3 clamped = originPosition + fromOrigin.normalized * safeRadius;
            clamped.y = candidate.y;
            return clamped;
        }

        return candidate;
    }

    bool TryGetSafeCombatDestination(Vector3 desired, float sampleRadius, out Vector3 destination)
    {
        destination = transform.position;
        if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            return false;
        }

        if (HorizontalDistanceXZ(transform.position, hit.position) < 0.2f)
        {
            return false;
        }

        // Né là bước ngắn: không chọn điểm nằm xuyên qua collider/tường động.
        if (!IsDirectMovementClear(hit.position))
        {
            return false;
        }

        if (IsAgentNavigable())
        {
            NavMeshPath path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }
        }

        destination = hit.position;
        return true;
    }

    bool IsDirectMovementClear(Vector3 destination)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 target = destination + Vector3.up * 0.5f;
        Vector3 delta = target - origin;
        float distance = delta.magnitude;
        if (distance <= 0.05f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, delta.normalized, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].collider != null ? hits[i].collider.transform : null;
            if (hitTransform == null) continue;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            if (player != null && (hitTransform == player || hitTransform.IsChildOf(player) || player.IsChildOf(hitTransform))) continue;
            return false;
        }

        return true;
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
    /// <summary>
    /// Perception: chỉ tin EnemySensor (FOV + LOS tường + hearing có LOS).
    /// Không CheckSphere xuyên tường.
    /// </summary>
    bool IsPlayerDeadForAi()
    {
        if (PlayerDeathController.IsPlayerDead)
        {
            return true;
        }

        if (player == null)
        {
            return false;
        }

        CharacterHealth ph = player.GetComponent<CharacterHealth>();
        if (ph == null)
        {
            ph = player.GetComponentInParent<CharacterHealth>();
        }

        return ph != null && ph.IsDead;
    }

    bool CanSensePlayer(out float distance)
    {
        distance = float.PositiveInfinity;
        if (player == null)
        {
            return false;
        }

        // Không target player đã chết.
        if (IsPlayerDeadForAi())
        {
            return false;
        }

        distance = HorizontalDistance(player.position);

        if (sensor != null)
        {
            return sensor.CanSense(player, out distance);
        }

        // Sensor thiếu: chỉ contact + LOS ray đơn giản (không sphere full sight).
        if (distance <= 1.35f)
        {
            return true;
        }

        if (distance > SightRangeForDetection())
        {
            return false;
        }

        Vector3 eye = transform.position + Vector3.up * 1.4f;
        Vector3 aim = player.position + Vector3.up * 1f;
        Vector3 delta = aim - eye;
        float rayDist = delta.magnitude;
        if (rayDist <= 0.01f)
        {
            return true;
        }

        if (Physics.Raycast(eye, delta.normalized, out RaycastHit hit, rayDist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == player || hit.transform.IsChildOf(player) || player.IsChildOf(hit.transform))
            {
                return true;
            }

            return false; // tường chặn
        }

        return true;
    }

    bool CheckDetect()
    {
        if (player == null)
        {
            return false;
        }

        if (!CanSensePlayer(out _))
        {
            return false;
        }

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
        if (IsPlayerDeadForAi()) return false;
        if (attackCooldownTimer > 0f) return false;
        // Nhẹ nhàng nới range (hitbox/body size) — tránh đứng sát mà không đánh.
        float range = AttackRange + 0.2f;
        if (distance > range && !playerInAttackRange) return false;
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
            RegisterPressureHit();
            ApplyPoiseDamage(dmg);
        }
        lastKnownHP = current;
    }

    void RegisterPressureHit()
    {
        if (!enablePressureEvade || health == null || health.IsDead) return;

        if (Time.time > pressureWindowExpiresAt)
        {
            pressureHitCount = 0;
        }

        pressureHitCount++;
        pressureWindowExpiresAt = Time.time + hitPressureWindow;

        if (debugLogCombatResponse)
        {
            Debug.Log($"[AI:{name}] Pressure hit {pressureHitCount}/{Mathf.Max(1, hitsToEvade)}", this);
        }

        if (pressureHitCount < Mathf.Max(1, hitsToEvade)) return;

        pressureHitCount = 0;
        pressureWindowExpiresAt = 0f;

        if (evadeQueued)
        {
            return;
        }

        if (Time.time < nextEvadeAllowedAt)
        {
            if (debugLogCombatResponse)
            {
                Debug.Log($"[AI:{name}] Evade bị từ chối: cooldown còn {nextEvadeAllowedAt - Time.time:F1}s.", this);
            }
            return;
        }

        if (Random.value > evadeChance)
        {
            if (debugLogCombatResponse)
            {
                Debug.Log($"[AI:{name}] Evade bị từ chối bởi chance ({evadeChance:P0}).", this);
            }
            return;
        }

        evadeQueued = true;
        nextEvadeAllowedAt = Time.time + evadeCooldown;
        if (debugLogCombatResponse)
        {
            Debug.Log($"[AI:{name}] Evade đã được xếp hàng sau Hurt/Stagger.", this);
        }
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
                // EnterState(AIState.Stagger);
                currentPoise = MaxPoise;
                EnterState(AIState.Hurt);
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
            case AIState.Evade:
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

        // Tutorial style: attack = red, sight = yellow, aggro keep = dark red.
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AttackRange);

        Gizmos.color = Color.yellow;
        float sight = Application.isPlaying ? SightRangeForDetection()
            : (enemyData != null ? enemyData.sightRange : 14f);
        Gizmos.DrawWireSphere(transform.position, sight);

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, AggroKeepRange);

        if (randomWalkPointSet)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(randomWalkPoint, 0.25f);
            Gizmos.DrawLine(transform.position, randomWalkPoint);
        }
    }
}
