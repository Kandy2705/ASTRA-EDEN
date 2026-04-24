using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private static readonly int BlendHash = Animator.StringToHash("Blend");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float animatorDampTime = 0.1f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -2f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private Key jumpKey = Key.Space;

    [Header("Dash")]
    [SerializeField] private Key dashKey = Key.LeftCtrl;
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 3f;
    [SerializeField] private float dashAnimationSpeed = 1.8f;

    [SerializeField] private float currentSpeedFactor = 0f;

    private Vector3 verticalVelocity;
    private Vector3 dashDirection;
    private Vector2 dashAnimationInput;
    private float dashTimer;
    private float nextDashTime;
    private float normalAnimatorSpeed = 1f;
    private bool isDashing;
    private bool isGrounded;

    public bool IsDashing => isDashing;

    private void Reset()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        cameraTransform = Camera.main != null ? Camera.main.transform : null;
    }

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }
    }

    private void Update()
    {
        isGrounded = controller.isGrounded;

        Vector2 movementInput = ReadMovementInput();
        Vector3 moveDir = GetMoveDirection(movementInput);
        bool isMoving = !isDashing && moveDir.sqrMagnitude > 0.001f;
        bool isRunning = isMoving && Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

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

        UpdateAnimator(isDashing ? dashAnimationInput : movementInput);

        HandleDashInput(moveDir, movementInput, moveDir.sqrMagnitude > 0.001f);

        if (isDashing)
        {
            MoveDash();
        }
        else
        {
            MoveCharacter(moveDir, isMoving);
        }

        ApplyGravityAndJump();
    }

    private void HandleDashInput(Vector3 moveDir, Vector2 movementInput, bool hasMoveInput)
    {
        if (isDashing || !hasMoveInput || Time.time < nextDashTime || !isGrounded)
        {
            return;
        }

        if (!IsDashPressed())
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

        if (animator != null)
        {
            normalAnimatorSpeed = animator.speed;
            animator.speed = dashAnimationSpeed;
        }
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

        if (animator != null)
        {
            animator.speed = normalAnimatorSpeed;
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

    private void ApplyGravityAndJump()
    {
        if (isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = groundedStickForce;
        }

        if (!isDashing && IsJumpPressed() && isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }
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

    private void UpdateAnimator(Vector2 movementInput)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat(BlendHash, currentSpeedFactor, animatorDampTime, Time.deltaTime);
        animator.SetFloat(HorizontalHash, movementInput.x, animatorDampTime, Time.deltaTime);
        animator.SetFloat(VerticalHash, movementInput.y, animatorDampTime, Time.deltaTime);
        animator.SetBool(IsGroundedHash, isGrounded);
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

    private Vector2 ReadMovementInput()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        float h = 0f;
        float v = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            h -= 1f;
        }

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            h += 1f;
        }

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        {
            v -= 1f;
        }

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
        {
            v += 1f;
        }

        return new Vector2(h, v).normalized;
    }

    private bool IsJumpPressed()
    {
        return Keyboard.current != null && Keyboard.current[jumpKey].wasPressedThisFrame;
    }

    private bool IsDashPressed()
    {
        return Keyboard.current != null && Keyboard.current[dashKey].wasPressedThisFrame;
    }
}
