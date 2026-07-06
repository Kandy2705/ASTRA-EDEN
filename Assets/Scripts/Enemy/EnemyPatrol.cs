using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterHealth))]
public class EnemyPatrol : MonoBehaviour
{
    private static readonly int BlendHash = Animator.StringToHash("Blend");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DieHash = Animator.StringToHash("Die");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly RaycastHit[] LosHitBuffer = new RaycastHit[8];

    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterHealth enemyHealth;
    [SerializeField] private CharacterKnockback knockback;
    [SerializeField] private RagdollOnDeath ragdollOnDeath;

    [Header("Debug")]
    [SerializeField] private float debugCurrentHP;
    [SerializeField] private float debugMaxHP;

    [Header("Chase Settings")]
    [SerializeField] private float detectRange = 14f; //khoang cach toi da nhan dien player (sight range)
    [SerializeField] private float loseRange = 22f; //khoang cach buoc enemy quen player (aggro keep range)
    [SerializeField] private float attackRange = 2f; //khoang cach de tan cong
    [SerializeField] private float patrolSpeed = 2f; //toc do di chuyen khi dieu tra
    [SerializeField] private float chaseSpeed = 4f; //toc do di chuyen khi truy cap
    [SerializeField] private float animatorDampTime = 0.1f; //thoi gian damper cho animator
    [SerializeField] private float movingThreshold = 0.05f; //nguong di chuyen

    [Header("Perception (FOV + LOS)")]
    [Tooltip("Goc nhin tong (do). 90-120 la hop ly cho enemy thuong.")]
    [Range(10f, 360f)]
    [SerializeField] private float sightAngle = 110f;
    [Tooltip("Transform mat/eye sensor de raycast LOS. Bo trong se dung transform.position + eyeHeight.")]
    [SerializeField] private Transform eyeSensor;
    [Tooltip("Cao do mat khi khong gan eyeSensor (de raycast tu day).")]
    [SerializeField] private float eyeHeight = 1.6f;
    [Tooltip("Cao do nguc player de raycast den (tranh mat dat).")]
    [SerializeField] private float targetChestHeight = 1.0f;
    [Tooltip("Layer cua vat can che tam nhin (Default, Wall, Terrain...). KHONG bao gom Player.")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [Tooltip("Sau khi mat dau player, giu aggro them bao nhieu giay roi moi tro ve patrol.")]
    [SerializeField] private float loseTargetTime = 6f;
    [Tooltip("Khi mat aggro va ngoai range nay, quay ve diem patrol goc.")]
    [SerializeField] private bool returnToPatrolOnLost = true;
    [Tooltip("Bat de bo qua FOV/LOS khi player vao sat (gan hon detectRange * thi nay) - mo phong nghe/cam giac.")]
    [Range(0f, 1f)]
    [SerializeField] private float proximityAlertRatio = 0.3f;

    [Header("Hit / Death Animation")]
    [Tooltip("Bật để chạy anim Die (trigger Die + bool IsDead) khi chết, sau đó mới gọi Ragdoll.")]
    [SerializeField] private bool useDeathAnimation = true;
    [Tooltip("Thời gian chờ anim Die chạy xong rồi mới bật ragdoll/destroy (giây).")]
    [SerializeField] private float deathAnimationDuration = 2.0f;
    [Tooltip("Bật để trigger anim Hit (Animator param 'Hit') khi enemy bị đánh dính.")]
    [SerializeField] private bool useHitAnimation = true;
    [Tooltip("Thời gian khóa di chuyển khi bị đánh dính, để anim Hit chạy không bị cắt.")]
    [SerializeField] private float hitStunDuration = 0.25f;

    [Header("Model Orientation")]
    [Tooltip("Tick nếu model bị lật ngược 180° (forward thật ra là -Z). Sẽ đảo logic xoay + animator velocity.")]
    [SerializeField] private bool flipForward180 = false;

    [Header("Attack")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float attackLockDuration = 0.9f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private EnemyAttackHitbox attackHitbox;
    [Tooltip("Bật để KHÔNG dùng Animation Event mà tự tính giờ ra dame theo % của attackLockDuration.")]
    [SerializeField] private bool useTimedFallback = false;
    [Tooltip("Tỉ lệ thời điểm gây dame trong attackLockDuration (0..1). Chỉ dùng khi useTimedFallback = true.")]
    [Range(0f, 1f)]
    [SerializeField] private float timedHitNormalized = 0.45f;

    private NavMeshAgent agent;
    private Transform currentPatrolTarget;
    private CharacterHealth playerHealth;
    private float attackLockTimer;
    private float nextAttackTime;
    private bool isDead;
    private bool swingActive;
    private bool swingHitResolved;
    private float swingElapsed;
    private float hitStunTimer;
    private float deathTimer;
    private bool deathSequenceFinished;
    private float lastKnownHP = float.NaN;
    private float lostSightTimer;
    private bool hasLineOfSight;
    private float lastHitReactionTime;

    private enum EnemyState
    {
        Patrol, //trang thai dieu tra
        Chase //trang thai truy cap
    }

    private EnemyState currentState = EnemyState.Patrol;

    private bool IsAttacking => attackLockTimer > 0f;
    public float AttackDamage => attackDamage;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null && flipForward180)
        {
            agent.updateRotation = false;
        }
        AssignDefaultPlayerLayer();
        EnsureEnemyHealth();
        EnsureKnockback();
        EnsureRagdoll();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponentInParent<CharacterHealth>();
            if (playerHealth == null)
            {
                playerHealth = playerObj.AddComponent<CharacterHealth>();
            }
        }

        currentPatrolTarget = pointA;
        if (agent != null)
        {
            agent.speed = patrolSpeed;

            if (currentPatrolTarget != null)
            {
                agent.SetDestination(currentPatrolTarget.position);
            }
        }

        RefreshDebugHealth();
    }

    void Update()
    {
        RefreshDebugHealth();

        if (isDead || (enemyHealth != null && enemyHealth.IsDead))
        {
            HandleDeath();
            return;
        }

        if (agent == null)
        {
            return;
        }

        TickAttackLock();

        if (hitStunTimer > 0f)
        {
            hitStunTimer -= Time.deltaTime;
            StopAgent();
            UpdateAnimator(0f);
            return;
        }

        if (IsAttacking || (knockback != null && knockback.IsKnockedBack))
        {
            StopAgent();
            UpdateAnimator(0f);
            return;
        }

        if (player == null || pointA == null || pointB == null)
        {
            UpdateAnimator(0f);
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        bool canSeePlayer = CanSeePlayer(distanceToPlayer);
        hasLineOfSight = canSeePlayer;

        if (currentState == EnemyState.Patrol)
        {
            if (canSeePlayer)
            {
                currentState = EnemyState.Chase;
                agent.speed = chaseSpeed;
                lostSightTimer = 0f;
            }
        }
        else // Chase
        {
            if (canSeePlayer)
            {
                lostSightTimer = 0f;
            }
            else
            {
                lostSightTimer += Time.deltaTime;
                bool tooFar = distanceToPlayer >= loseRange;
                if (tooFar || lostSightTimer >= loseTargetTime)
                {
                    currentState = EnemyState.Patrol;
                    agent.speed = patrolSpeed;
                    lostSightTimer = 0f;
                    if (returnToPatrolOnLost)
                    {
                        GoToCurrentPatrolPoint();
                    }
                }
            }
        }

        if (currentState == EnemyState.Patrol)
            HandlePatrol();
        else
            HandleChase();

        ApplyFlippedFacing();
        UpdateAnimator(GetTargetBlend());
    }

    private void ApplyFlippedFacing()
    {
        if (!flipForward180 || agent == null) return;
        if (agent.velocity.sqrMagnitude <= movingThreshold * movingThreshold) return;

        Vector3 dir = agent.velocity;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(-dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 10f * Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.Died -= HandleHealthDied;
            enemyHealth.Changed -= HandleHealthChanged;
        }
    }

    void HandlePatrol()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            currentPatrolTarget = (currentPatrolTarget == pointA) ? pointB : pointA;
            agent.SetDestination(currentPatrolTarget.position);
        }
    }

    void HandleChase()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        if (distanceToPlayer > attackRange)
        {
            agent.SetDestination(player.position);
        }
        else
        {
            StopAgent();
            TryStartAttack();
        }
    }


    /// <summary>FOV + LOS perception. Returns true neu player nam trong sight range, sight angle va khong bi vat can che.</summary>
    private bool CanSeePlayer(float distanceToPlayer)
    {
        if (player == null) return false;
        if (distanceToPlayer > detectRange) return false;

        Vector3 eyePos = GetEyePosition();
        Vector3 targetPos = player.position + Vector3.up * targetChestHeight;
        Vector3 toTarget = targetPos - eyePos;
        float planarDist = new Vector2(toTarget.x, toTarget.z).magnitude;
        if (planarDist < 0.001f) planarDist = 0.001f;

        // Proximity alert: rat gan thi bo qua FOV (mo phong nghe/cam giac)
        bool insideProximity = distanceToPlayer <= detectRange * proximityAlertRatio;

        if (!insideProximity)
        {
            Vector3 forward = transform.forward;
            if (flipForward180) forward = -forward;
            Vector3 toTargetPlanar = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            Vector3 forwardPlanar = new Vector3(forward.x, 0f, forward.z).normalized;
            float dot = Vector3.Dot(forwardPlanar, toTargetPlanar);
            float halfAngleCos = Mathf.Cos(sightAngle * 0.5f * Mathf.Deg2Rad);
            if (dot < halfAngleCos) return false;
        }

        // LOS raycast tu mat den nguc player, bo qua collider cua chinh enemy
        float rayDist = toTarget.magnitude;
        int count = Physics.RaycastNonAlloc(eyePos, toTarget.normalized, LosHitBuffer, rayDist, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform t = LosHitBuffer[i].transform;
            if (t == null) continue;
            if (t == transform || t.IsChildOf(transform)) continue; // chinh minh
            if (t == player || t.IsChildOf(player) || player.IsChildOf(t)) continue; // muc tieu
            return false; // co vat can khac chan
        }
        return true;
    }

    private Vector3 GetEyePosition()
    {
        if (eyeSensor != null) return eyeSensor.position;
        return transform.position + Vector3.up * eyeHeight;
    }

    void GoToCurrentPatrolPoint()
    {
        if (currentPatrolTarget == null) currentPatrolTarget = pointA;
        agent.SetDestination(currentPatrolTarget.position);
    }

    private float GetTargetBlend()
    {
        if (agent.velocity.sqrMagnitude <= movingThreshold * movingThreshold)
        {
            return 0f;
        }

        return currentState == EnemyState.Chase ? 2f : 1f;
    }

    private void UpdateAnimator(float targetBlend)
    {
        if (animator == null)
        {
            return;
        }

        Vector3 localVelocity = Vector3.zero;
        if (agent != null && agent.velocity.sqrMagnitude > movingThreshold * movingThreshold)
        {
            localVelocity = transform.InverseTransformDirection(agent.velocity.normalized);
            if (flipForward180)
            {
                localVelocity.x = -localVelocity.x;
                localVelocity.z = -localVelocity.z;
            }
        }

        animator.SetFloat(BlendHash, targetBlend, animatorDampTime, Time.deltaTime);
        animator.SetFloat(HorizontalHash, localVelocity.x, animatorDampTime, Time.deltaTime);
        animator.SetFloat(VerticalHash, localVelocity.z, animatorDampTime, Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAttackCollisionTarget(other.transform);
    }

    private void OnTriggerStay(Collider other)
    {
        TryAttackCollisionTarget(other.transform);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryAttackCollisionTarget(collision.transform);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryAttackCollisionTarget(collision.transform);
    }

    private void TryAttackCollisionTarget(Transform hitTransform)
    {
        if (isDead || enemyHealth == null || enemyHealth.IsDead)
        {
            return;
        }

        if (!IsPlayerLayer(hitTransform))
        {
            return;
        }

        TryStartAttack();
    }

    private void TryStartAttack()
    {
        if (isDead || enemyHealth == null || enemyHealth.IsDead)
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        StartAttack();
    }

    private void StartAttack()
    {
        attackLockTimer = attackLockDuration;
        nextAttackTime = Time.time + attackCooldown;

        StopAgent();
        FacePlayer();

        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }

        BeginAttackSwing();
    }

    private void TickAttackLock()
    {
        if (attackLockTimer > 0f)
        {
            attackLockTimer -= Time.deltaTime;
        }

        if (swingActive)
        {
            swingElapsed += Time.deltaTime;

            if (useTimedFallback && !swingHitResolved && swingElapsed >= attackLockDuration * timedHitNormalized)
            {
                PerformAttackHit();
            }

            if (swingElapsed >= attackLockDuration)
            {
                EndAttackSwing();
            }
        }
    }

    /// <summary>Bắt đầu 1 lượt swing. Gọi nội bộ từ StartAttack hoặc từ Animation Event OnAttackStart.</summary>
    public void BeginAttackSwing()
    {
        swingActive = true;
        swingHitResolved = false;
        swingElapsed = 0f;

        if (attackHitbox != null)
        {
            if (attackHitbox.TargetLayer.value == 0)
            {
                attackHitbox.SetTargetLayer(playerLayer);
            }
            attackHitbox.BeginSwing();
        }
    }

    /// <summary>Quét hitbox tại frame impact. Gọi từ Animation Event OnAttackHit (hoặc fallback theo timing).</summary>
    public void PerformAttackHit()
    {
        if (swingHitResolved) return;
        if (isDead || enemyHealth == null || enemyHealth.IsDead) return;

        swingHitResolved = true;

        if (attackHitbox != null)
        {
            attackHitbox.PerformHit(attackDamage);
        }
        else
        {
            // Không có hitbox -> không ra dame. Tránh fallback dame full-radius gây bug "đánh sau lưng vẫn trúng".
            Debug.LogWarning($"{name}: EnemyAttackHitbox chưa được gán — đòn đánh không gây dame.", this);
        }
    }

    private void EndAttackSwing()
    {
        swingActive = false;
        swingHitResolved = false;
        swingElapsed = 0f;
    }

    private void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    private void FacePlayer()
    {
        if (player == null)
        {
            return;
        }

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Vector3 facing = flipForward180 ? -direction : direction;
            transform.rotation = Quaternion.LookRotation(facing);
        }
    }

    private bool IsPlayerLayer(Transform hitTransform)
    {
        while (hitTransform != null)
        {
            int hitLayerMask = 1 << hitTransform.gameObject.layer;
            if ((playerLayer.value & hitLayerMask) != 0)
            {
                return true;
            }

            hitTransform = hitTransform.parent;
        }

        return false;
    }

    private void AssignDefaultPlayerLayer()
    {
        if (playerLayer.value != 0)
        {
            return;
        }

        int defaultPlayerLayer = LayerMask.GetMask("Player");
        if (defaultPlayerLayer != 0)
        {
            playerLayer = defaultPlayerLayer;
        }
    }

    private void EnsureEnemyHealth()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<CharacterHealth>();
        }

        if (enemyHealth == null)
        {
            enemyHealth = gameObject.AddComponent<CharacterHealth>();
        }

        enemyHealth.Died -= HandleHealthDied;
        enemyHealth.Died += HandleHealthDied;
        enemyHealth.Changed -= HandleHealthChanged;
        enemyHealth.Changed += HandleHealthChanged;
        isDead = enemyHealth.IsDead;
        lastKnownHP = enemyHealth.RuntimeStats != null ? enemyHealth.RuntimeStats.currentHP : float.NaN;
    }

    private void HandleHealthChanged(CharacterHealth h)
    {
        if (h == null || h.RuntimeStats == null) return;
        float currentHP = h.RuntimeStats.currentHP;

        if (!float.IsNaN(lastKnownHP) && currentHP < lastKnownHP - 0.001f && !isDead && !h.IsDead)
        {
            TriggerHitAnimation();
        }

        lastKnownHP = currentHP;
    }

    private void TriggerHitAnimation()
    {
        if (!useHitAnimation || animator == null)
        {
            return;
        }

        bool interruptingAttack = IsAttacking || swingActive;
        if (!interruptingAttack && Time.time - lastHitReactionTime < 0.35f)
        {
            return;
        }

        CancelAttackForHit();

        if (HasAnimatorParameter(HitHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(HitHash);
            animator.SetTrigger(HitHash);
        }

        animator.Play("Hit", 0, 0f);
        animator.Update(0f);
        lastHitReactionTime = Time.time;

        if (hitStunDuration > 0f)
        {
            hitStunTimer = Mathf.Max(hitStunTimer, hitStunDuration);
        }
    }

    private void CancelAttackForHit()
    {
        attackLockTimer = 0f;
        EndAttackSwing();

        if (animator == null)
        {
            return;
        }

        animator.ResetTrigger(AttackHash);

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Basic Attack") || state.IsName("Attack"))
        {
            animator.Play("Hit", 0, 0f);
        }
    }

    private bool HasAnimatorParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == hash && p.type == type) return true;
        }
        return false;
    }

    private void EnsureKnockback()
    {
        if (knockback == null)
        {
            knockback = GetComponent<CharacterKnockback>();
        }

        if (knockback == null)
        {
            knockback = gameObject.AddComponent<CharacterKnockback>();
        }
    }

    private void EnsureRagdoll()
    {
        if (ragdollOnDeath == null)
        {
            ragdollOnDeath = GetComponent<RagdollOnDeath>();
        }

        if (ragdollOnDeath != null && useDeathAnimation)
        {
            ragdollOnDeath.SetControlledExternally(true);
        }
    }

    private void HandleHealthDied(CharacterHealth deadHealth)
    {
        isDead = true;
        HandleDeath();
    }

    private void HandleDeath()
    {
        if (!isDead)
        {
            isDead = true;
        }
        attackLockTimer = 0f;
        hitStunTimer = 0f;

        FreezeAgentForDeath();
        UpdateAnimator(0f);

        if (animator != null)
        {
            if (HasAnimatorParameter(IsDeadHash, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(IsDeadHash, true);
            }
        }

        if (useDeathAnimation && animator != null && !deathSequenceFinished)
        {
            if (deathTimer <= 0f)
            {
                animator.ResetTrigger(AttackHash);
                animator.ResetTrigger(HitHash);
                if (HasAnimatorParameter(DieHash, AnimatorControllerParameterType.Trigger))
                {
                    animator.SetTrigger(DieHash);
                }
                if (ragdollOnDeath != null)
                {
                    ragdollOnDeath.BeginDeathPhysics();
                }
                deathTimer = Mathf.Max(0.05f, deathAnimationDuration);
            }

            deathTimer -= Time.deltaTime;

            if (deathTimer > 0f)
            {
                return;
            }

            deathSequenceFinished = true;
        }

        if (ragdollOnDeath != null)
        {
            ragdollOnDeath.EnableRagdoll();
            return;
        }

        if (agent != null)
        {
            agent.enabled = false;
        }
    }

    private void FreezeAgentForDeath()
    {
        if (agent == null) return;
        if (!agent.enabled) return;
        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }
        agent.updatePosition = false;
        agent.updateRotation = false;
    }

    private void RefreshDebugHealth()
    {
        if (enemyHealth == null || enemyHealth.RuntimeStats == null)
        {
            debugCurrentHP = 0f;
            debugMaxHP = 0f;
            return;
        }

        debugCurrentHP = enemyHealth.RuntimeStats.currentHP;
        debugMaxHP = enemyHealth.RuntimeStats.maxHP;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? GetEyePosition() : transform.position + Vector3.up * eyeHeight;

        // FOV cone
        Vector3 forward = transform.forward;
        if (flipForward180) forward = -forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f) forward.Normalize();

        Gizmos.color = hasLineOfSight ? new Color(0f, 1f, 0f, 0.9f) : new Color(1f, 0.9f, 0.2f, 0.9f);
        Quaternion leftRot = Quaternion.AngleAxis(-sightAngle * 0.5f, Vector3.up);
        Quaternion rightRot = Quaternion.AngleAxis(sightAngle * 0.5f, Vector3.up);
        Vector3 leftDir = leftRot * forward;
        Vector3 rightDir = rightRot * forward;
        Gizmos.DrawLine(origin, origin + leftDir * detectRange);
        Gizmos.DrawLine(origin, origin + rightDir * detectRange);

        const int arcSegments = 24;
        Vector3 prev = origin + leftDir * detectRange;
        for (int i = 1; i <= arcSegments; i++)
        {
            float t = (float)i / arcSegments;
            Quaternion rot = Quaternion.AngleAxis(Mathf.Lerp(-sightAngle * 0.5f, sightAngle * 0.5f, t), Vector3.up);
            Vector3 cur = origin + (rot * forward) * detectRange;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }

        // Proximity alert ring
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, detectRange * proximityAlertRatio);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
