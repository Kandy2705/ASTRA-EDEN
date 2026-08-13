using UnityEngine;
[RequireComponent(typeof(CharacterController), typeof(PlayerInputReader), typeof(PlayerAnimatorBridge))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerAnimatorBridge animatorBridge;
    [SerializeField] private PlayerCombatController combatController;
    [SerializeField] private PlayerAudioController audioController;
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -2f;
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("Hit Reaction")]
    [SerializeField, Min(0f)] private float hitMovementLockDuration = 1f;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 3f;
    [SerializeField] private float dashAnimationSpeed = 1.8f;
    [SerializeField] private float dashEnergyCost = 25f;
    [SerializeField] private CharacterHealth playerHealth;
    [SerializeField] private PlayerKnockbackReceiver knockbackReceiver;

    [Header("Sprint Mana")]
    [SerializeField, Min(0f)] private float sprintEnergyDrainPerSecond = 10f;
    [SerializeField, Min(0f)] private float sprintRestartEnergyThreshold = 1f;
    [SerializeField, Min(0f)] private float energyRegenDelayAfterUse = 0.65f;

    [SerializeField] private float currentSpeedFactor = 0f;

    private Vector3 verticalVelocity;
    private Vector3 dashDirection;
    private Vector2 dashAnimationInput;
    private float dashTimer;
    private float nextDashTime;
    private bool isDashing;
    private bool isGrounded;
    private float movementLockedUntil;
    private float energyRegenBlockedUntil;
    private bool isSprinting;

    public bool IsDashing => isDashing;
    public bool IsMovementLockedByHit => Time.time < movementLockedUntil;

    private void Reset()
    {
        inputReader = GetComponent<PlayerInputReader>();
        animatorBridge = GetComponent<PlayerAnimatorBridge>();
        combatController = GetComponent<PlayerCombatController>();
        audioController = GetComponent<PlayerAudioController>();
        controller = GetComponent<CharacterController>();
        ResolveCameraTransform();
    }

    private void Awake()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        if (inputReader == null)
        {
            inputReader = gameObject.AddComponent<PlayerInputReader>();
        }

        if (animatorBridge == null)
        {
            animatorBridge = GetComponent<PlayerAnimatorBridge>();
        }

        if (animatorBridge == null)
        {
            animatorBridge = gameObject.AddComponent<PlayerAnimatorBridge>();
        }

        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        if (combatController == null)
        {
            combatController = GetComponent<PlayerCombatController>();
        }

        if (audioController == null)
        {
            audioController = GetComponent<PlayerAudioController>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<CharacterHealth>();
        }

        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<PlayerKnockbackReceiver>();
        }

        if (cameraTransform == null)
        {
            ResolveCameraTransform();
        }
    }

    private void Update()
    {
        if (PlayerDeathController.IsPlayerDead || (playerHealth != null && playerHealth.IsDead))
        {
            // Chết: chỉ giữ gravity nhẹ nếu CC còn bật; không move/dash/jump.
            if (controller != null && controller.enabled && !controller.isGrounded)
            {
                verticalVelocity.y += gravity * Time.deltaTime;
                controller.Move(verticalVelocity * Time.deltaTime);
            }

            animatorBridge?.UpdateLocomotion(0f, Vector2.zero, controller != null && controller.isGrounded);
            return;
        }

        inputReader.ReadInput();
        isGrounded = controller.isGrounded;

        if (knockbackReceiver != null && knockbackReceiver.IsKnockedBack)
        {
            ApplyGravityAndJump(false);
            animatorBridge.UpdateLocomotion(0f, Vector2.zero, isGrounded);
            return;
        }

        if (IsMovementLockedByHit)
        {
            currentSpeedFactor = 0f;
            animatorBridge.UpdateLocomotion(0f, Vector2.zero, isGrounded);
            ApplyGravityAndJump(true);

            TickEnergyRegenIfAllowed();

            return;
        }

        Vector2 movementInput = inputReader.MoveInput;
        Vector3 moveDir = GetMoveDirection(movementInput);
        bool isAttacking = combatController != null && combatController.IsAttacking;
        bool attackMoveActive = combatController != null && combatController.IsAttackMoveActive;
        bool canMove = !isDashing && !isAttacking;
        bool isMoving = canMove && moveDir.sqrMagnitude > 0.001f;
        bool isRunning = ResolveSprintState(isMoving);

        float targetSpeed = 0f;
        if (isDashing)
        {
            targetSpeed = 2f;
        }
        else if (isMoving)
        {
            targetSpeed = isRunning ? 2f : 1f;
        }

        currentSpeedFactor = Mathf.MoveTowards(
            currentSpeedFactor,
            targetSpeed,
            acceleration * Time.deltaTime
        );

        animatorBridge.UpdateLocomotion(currentSpeedFactor, isDashing ? dashAnimationInput : isAttacking ? Vector2.zero : movementInput, isGrounded);

        if (!isAttacking)
        {
            HandleDashInput(moveDir, movementInput, moveDir.sqrMagnitude > 0.001f);
        }

        if (isDashing)
        {
            MoveDash();
        }
        else if (attackMoveActive)
        {
            MoveAttackForward();
        }
        else if (!isAttacking)
        {
            MoveCharacter(moveDir, isMoving);
        }

        ApplyGravityAndJump(isAttacking);

        TickEnergyRegenIfAllowed();
    }

    public void LockMovementForHit()
    {
        LockMovementForHit(hitMovementLockDuration);
    }

    public void LockMovementForHit(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        movementLockedUntil = Mathf.Max(movementLockedUntil, Time.time + duration);
        currentSpeedFactor = 0f;
        combatController?.InterruptForHit(duration);

        if (isDashing)
        {
            StopDash();
        }
    }

    private void HandleDashInput(Vector3 moveDir, Vector2 movementInput, bool hasMoveInput)
    {
        if (isDashing || !hasMoveInput || Time.time < nextDashTime || !isGrounded)
        {
            return;
        }

        if (!inputReader.DashPressed)
        {
            return;
        }

        if (dashEnergyCost > 0f && playerHealth != null && !playerHealth.TryConsumeEnergy(dashEnergyCost))
        {
            return;
        }

        BlockEnergyRegen();

        StartDash(moveDir, movementInput);
    }

    private void StartDash(Vector3 moveDir, Vector2 movementInput)
    {
        isDashing = true;
        dashTimer = dashDuration;
        nextDashTime = Time.time + dashCooldown;

        dashDirection = moveDir.sqrMagnitude > 0.001f ? moveDir.normalized : transform.forward;
        dashAnimationInput = movementInput.sqrMagnitude > 0.001f ? movementInput : Vector2.up;

        animatorBridge.PushPlaybackSpeed(dashAnimationSpeed);
    }

    private void MoveDash()
    {
        dashTimer -= Time.deltaTime;

        float dashSpeed = dashDistance / Mathf.Max(0.001f, dashDuration);
        controller.Move(dashDirection * dashSpeed * Time.deltaTime);

        transform.forward = Vector3.Slerp(
            transform.forward,
            dashDirection,
            turnSpeed * Time.deltaTime
        );

        if (dashTimer <= 0f)
        {
            StopDash();
        }
    }

    private void StopDash()
    {
        isDashing = false;

        animatorBridge.RestorePlaybackSpeed();
    }

    private bool ResolveSprintState(bool isMoving)
    {
        if (!isMoving || inputReader == null || !inputReader.RunHeld || playerHealth == null)
        {
            isSprinting = false;
            return false;
        }

        if (!isSprinting && !playerHealth.HasEnoughEnergy(sprintRestartEnergyThreshold))
        {
            return false;
        }

        if (sprintEnergyDrainPerSecond <= 0f)
        {
            isSprinting = true;
            return true;
        }

        isSprinting = playerHealth.DrainEnergy(sprintEnergyDrainPerSecond * Time.deltaTime);
        BlockEnergyRegen();
        return isSprinting;
    }

    private void BlockEnergyRegen()
    {
        energyRegenBlockedUntil = Mathf.Max(energyRegenBlockedUntil, Time.time + energyRegenDelayAfterUse);
    }

    private void TickEnergyRegenIfAllowed()
    {
        if (playerHealth != null && !isDashing && !isSprinting && Time.time >= energyRegenBlockedUntil)
        {
            playerHealth.TickEnergyRegen(Time.deltaTime);
        }
    }

    private void MoveCharacter(Vector3 moveDir, bool isMoving)
    {
        if (isMoving)
        {
            float actualMoveSpeed = GetCurrentMoveSpeed();
            controller.Move(moveDir * actualMoveSpeed * Time.deltaTime);

            transform.forward = Vector3.Slerp(
                transform.forward,
                moveDir,
                turnSpeed * Time.deltaTime
            );
        }
    }

    private void MoveAttackForward()
    {
        if (combatController == null)
        {
            return;
        }

        float attackMoveSpeed = combatController.AttackMoveSpeed;
        Vector3 forward = transform.forward;
        forward.y = 0f;

        controller.Move(forward.normalized * attackMoveSpeed * Time.deltaTime);
    }

    private void ApplyGravityAndJump(bool isAttacking)
    {
        if (isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = groundedStickForce;
        }

        if (!isDashing && !isAttacking && inputReader.JumpPressed && isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animatorBridge.TriggerJump();
            audioController?.PlayJumpSound();
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    private float GetCurrentMoveSpeed()
    {
        float finalRunSpeed = playerHealth != null && playerHealth.RuntimeStats != null
            ? Mathf.Max(0f, playerHealth.RuntimeStats.moveSpeed)
            : runSpeed;
        float walkRatio = runSpeed > 0.001f
            ? Mathf.Clamp01(walkSpeed / runSpeed)
            : 0.5f;
        float finalWalkSpeed = finalRunSpeed * walkRatio;

        if (currentSpeedFactor <= 1f)
        {
            return Mathf.Lerp(0f, finalWalkSpeed, currentSpeedFactor);
        }

        return Mathf.Lerp(finalWalkSpeed, finalRunSpeed, currentSpeedFactor - 1f);
    }

    private Vector3 GetMoveDirection(Vector2 movementInput)
    {
        if (movementInput.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        if (cameraTransform == null)
        {
            ResolveCameraTransform();
        }

        if (cameraTransform == null)
        {
            return new Vector3(movementInput.x, 0f, movementInput.y).normalized;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        return (cameraForward * movementInput.y + cameraRight * movementInput.x).normalized;
    }

    /// <summary>
    /// Resolve cameraTransform ưu tiên Camera có CameraController (ổn định sau khi load scene qua portal).
    /// Nếu không có thì fallback Camera.main.
    /// </summary>
    private void ResolveCameraTransform()
    {
        // Ưu tiên camera đang có CameraController (camera follow player)
        CameraController camCtrl = FindFirstObjectByType<CameraController>();
        if (camCtrl != null)
        {
            cameraTransform = camCtrl.transform;
            return;
        }

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

}
