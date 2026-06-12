using UnityEngine;
[RequireComponent(typeof(CharacterController), typeof(PlayerInputReader), typeof(PlayerAnimatorBridge))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerAnimatorBridge animatorBridge;
    [SerializeField] private PlayerCombatController combatController;
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -2f;
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 3f;
    [SerializeField] private float dashAnimationSpeed = 1.8f;
    [SerializeField] private float dashEnergyCost = 25f;
    [SerializeField] private CharacterHealth playerHealth;

    [SerializeField] private float currentSpeedFactor = 0f;

    private Vector3 verticalVelocity;
    private Vector3 dashDirection;
    private Vector2 dashAnimationInput;
    private float dashTimer;
    private float nextDashTime;
    private bool isDashing;
    private bool isGrounded;

    public bool IsDashing => isDashing;

    private void Reset()
    {
        inputReader = GetComponent<PlayerInputReader>();
        animatorBridge = GetComponent<PlayerAnimatorBridge>();
        combatController = GetComponent<PlayerCombatController>();
        controller = GetComponent<CharacterController>();
        cameraTransform = Camera.main != null ? Camera.main.transform : null;
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

        if (playerHealth == null)
        {
            playerHealth = GetComponent<CharacterHealth>();
        }
    }

    private void Update()
    {
        inputReader.ReadInput();
        isGrounded = controller.isGrounded;

        Vector2 movementInput = inputReader.MoveInput;
        Vector3 moveDir = GetMoveDirection(movementInput);
        bool isAttacking = combatController != null && combatController.IsAttacking;
        bool attackMoveActive = combatController != null && combatController.IsAttackMoveActive;
        bool canMove = !isDashing && !isAttacking;
        bool isMoving = canMove && moveDir.sqrMagnitude > 0.001f;
        bool isRunning = isMoving && inputReader.RunHeld;

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

        if (playerHealth != null && !isDashing)
        {
            playerHealth.TickEnergyRegen(Time.deltaTime);
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
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    private float GetCurrentMoveSpeed()
    {
        if (currentSpeedFactor <= 1f)
        {
            return Mathf.Lerp(0f, walkSpeed, currentSpeedFactor);
        }

        return Mathf.Lerp(walkSpeed, runSpeed, currentSpeedFactor - 1f);
    }

    private Vector3 GetMoveDirection(Vector2 movementInput)
    {
        if (movementInput.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        Transform activeCamera = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
        if (activeCamera == null)
        {
            return new Vector3(movementInput.x, 0f, movementInput.y).normalized;
        }

        Vector3 cameraForward = activeCamera.forward;
        Vector3 cameraRight = activeCamera.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        return (cameraForward * movementInput.y + cameraRight * movementInput.x).normalized;
    }

}
