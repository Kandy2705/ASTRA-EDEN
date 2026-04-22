using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private static readonly int BlendHash = Animator.StringToHash("Blend");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");

    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float animatorDampTime = 0.1f;

    [SerializeField] private float currentSpeedFactor = 0f;

    void Update()
    {
        Vector2 movementInput = ReadMovementInput();
        Vector3 moveDir = GetMoveDirection(movementInput);
        bool isMoving = moveDir.sqrMagnitude > 0.001f;
        bool isRunning = isMoving && Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        float targetSpeed = 0f;
        if (isMoving)
        {
            targetSpeed = isRunning ? 2f : 1f;
        }

        currentSpeedFactor = Mathf.MoveTowards(
            currentSpeedFactor,
            targetSpeed,
            acceleration * Time.deltaTime
        );

        UpdateAnimator(movementInput);

        if (isMoving)
        {
            float actualMoveSpeed = GetCurrentMoveSpeed();
            transform.position += moveDir * actualMoveSpeed * Time.deltaTime;

            transform.forward = Vector3.Slerp(
                transform.forward,
                moveDir,
                turnSpeed * Time.deltaTime
            );
        }
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
}
