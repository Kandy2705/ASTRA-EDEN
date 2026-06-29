using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public enum CameraMode
    {
        Exploration,
        Combat,
        LockOn,
        Boss
    }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0.45f, 1.55f, 0f);

    [Header("Mode")]
    [SerializeField] private CameraMode currentMode = CameraMode.Exploration;

    [Header("Distance")]
    [SerializeField] private float distance = 4.5f;
    [SerializeField] private float minDistance = 1.2f;
    [SerializeField] private float maxDistance = 6f;
    [SerializeField] private float zoomSpeed = 2f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivityX = 180f;
    [SerializeField] private float mouseSensitivityY = 120f;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private bool invertY;

    [Header("Smoothing")]
    [SerializeField] private float smoothSpeed = 12f;

    [Header("Combat Hook")]
    [SerializeField] private Vector3 combatTargetOffset = new Vector3(0.55f, 1.45f, 0f);
    [SerializeField] private float combatDistance = 4.2f;
    [SerializeField] private float combatSmoothSpeed = 14f;

    [Header("Lock-On Hook")]
    [SerializeField] private Transform lockOnTarget;
    [SerializeField] private Vector3 lockOnTargetOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private Vector3 lockOnCameraOffset = new Vector3(0.2f, 1.45f, 0f);
    [SerializeField] private float lockOnDistance = 5f;
    [SerializeField] private float lockOnRotationSpeed = 10f;

    [Header("Boss Hook")]
    [SerializeField] private Transform bossTarget;
    [SerializeField] private Vector3 bossTargetOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private Vector3 bossCameraOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField] private float bossDistance = 7f;
    [SerializeField] private float bossRotationSpeed = 7f;
    [SerializeField, Range(0f, 1f)] private float bossFramingWeight = 0.45f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionBuffer = 0.15f;

    [Header("Cursor")]
    [SerializeField] private bool showCursorOnStart = true;
    [SerializeField] private bool rotateOnlyWhileHoldingRightMouse = true;
    [SerializeField] private bool hideCursorWhileRotating;

    [Header("Fast Movement Follow")]
    [SerializeField] private float fastFollowDistance = 1.5f;
    [SerializeField] private float snapDistance = 4f;
    [SerializeField] private float fastFollowSmoothSpeed = 35f;

    private float yaw;
    private float pitch = 15f;
    private Vector3 currentVelocity;

    public CameraMode CurrentMode => currentMode;
    public Transform LockOnTarget => lockOnTarget;
    public Transform BossTarget => bossTarget;

    private void Start()
    {
        Vector3 startAngles = transform.eulerAngles;
        yaw = startAngles.y;
        pitch = NormalizeAngle(startAngles.x);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        ApplyCursorState(false);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        SanitizeCameraMode();

        // 1. Read camera input after gameplay movement has already updated.
        ApplyCursorState(IsRotatingCamera());
        ReadMouseInput();
        ReadZoomInput();

        // 2. Convert yaw and pitch into a camera orbit rotation.
        Quaternion cameraRotation = CalculateCameraRotation();

        // 3. Pick the point the camera should orbit around, usually near head/shoulder height.
        Vector3 focusPoint = CalculateFocusPoint();

        // 4. Place the camera behind that point at the requested distance.
        Vector3 desiredPosition = CalculateDesiredPosition(focusPoint, cameraRotation);

        // 5. Pull the camera forward if a wall is between the player and the camera.
        Vector3 finalPosition = ResolveCameraCollision(focusPoint, desiredPosition);

        // 6. Smoothly apply the calculated position and rotation.
        ApplyCameraTransform(finalPosition, cameraRotation);
    }

    private void ReadMouseInput()
    {
        if (!IsRotatingCamera())
        {
            return;
        }

        Vector2 mouseDelta = GetMouseDelta();

        // Mouse X rotates the camera around the character.
        yaw += mouseDelta.x * mouseSensitivityX * Time.deltaTime;

        // Mouse Y tilts the camera up/down, then clamps so it never flips over.
        float pitchDirection = invertY ? 1f : -1f;
        pitch += mouseDelta.y * mouseSensitivityY * pitchDirection * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void ReadZoomInput()
    {
        float scroll = GetMouseScroll();

        if (Mathf.Abs(scroll) <= 0.001f)
        {
            return;
        }

        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    private Quaternion CalculateCameraRotation()
    {
        // Chỉ auto-framing khi player không đang giữ chuột phải để xoay tay.
        if (!IsRotatingCamera())
        {
            if (currentMode == CameraMode.LockOn && lockOnTarget != null)
            {
                UpdateLookAtAngles(lockOnTarget.position + lockOnTargetOffset, lockOnRotationSpeed);
            }
            else if (currentMode == CameraMode.Boss && bossTarget != null)
            {
                UpdateBossAngles();
            }
        }

        return Quaternion.Euler(pitch, yaw, 0f);
    }

    private Vector3 CalculateFocusPoint()
    {
        // Offset follows the camera yaw, which keeps over-the-shoulder framing consistent.
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        return target.position + yawRotation * GetActiveTargetOffset();
    }

    private Vector3 CalculateDesiredPosition(Vector3 focusPoint, Quaternion cameraRotation)
    {
        float clampedDistance = GetActiveClampedDistance();
        return focusPoint + cameraRotation * new Vector3(0f, 0f, -clampedDistance);
    }

    private Vector3 ResolveCameraCollision(Vector3 focusPoint, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - focusPoint;
        float desiredDistance = direction.magnitude;

        if (desiredDistance <= 0.001f)
        {
            return desiredPosition;
        }

        direction /= desiredDistance;

        if (Physics.SphereCast(
                focusPoint,
                collisionRadius,
                direction,
                out RaycastHit hit,
                desiredDistance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            // Keep a small buffer from the hit point to reduce wall clipping.
            float safeDistance = Mathf.Clamp(hit.distance - collisionBuffer, minDistance, desiredDistance);
            return focusPoint + direction * safeDistance;
        }

        return desiredPosition;
    }

    private void ApplyCameraTransform(Vector3 targetPosition, Quaternion targetRotation)
    {
        float activeSmoothSpeed = GetActiveSmoothSpeed();

        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        // Nếu camera bị bỏ quá xa, snap thẳng tới vị trí đúng
        if (distanceToTarget >= snapDistance)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            currentVelocity = Vector3.zero;
            return;
        }

        // Nếu player đang di chuyển nhanh/dodge làm camera tụt lại,
        // tăng tốc độ follow tạm thời.
        if (distanceToTarget >= fastFollowDistance)
        {
            activeSmoothSpeed = fastFollowSmoothSpeed;
        }

        float damping = 1f - Mathf.Exp(-activeSmoothSpeed * Time.deltaTime);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            Mathf.Max(0.001f, 1f / activeSmoothSpeed)
        );

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, damping);
    }

    public void SetExplorationMode()
    {
        currentMode = CameraMode.Exploration;
        ClearTrackedTargets();
    }

    public void SetCombatMode(bool enabled)
    {
        currentMode = enabled ? CameraMode.Combat : CameraMode.Exploration;
        if (!enabled)
        {
            ClearTrackedTargets();
        }
    }

    public void SetLockOnTarget(Transform newLockOnTarget)
    {
        lockOnTarget = newLockOnTarget;
        currentMode = lockOnTarget != null ? CameraMode.LockOn : CameraMode.Combat;
    }

    public void ClearLockOnTarget()
    {
        lockOnTarget = null;
        currentMode = currentMode == CameraMode.LockOn ? CameraMode.Combat : currentMode;
    }

    public void SetBossTarget(Transform newBossTarget)
    {
        bossTarget = newBossTarget;
        currentMode = bossTarget != null ? CameraMode.Boss : CameraMode.Exploration;
    }

    public void ClearBossTarget()
    {
        bossTarget = null;
        currentMode = currentMode == CameraMode.Boss ? CameraMode.Exploration : currentMode;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void ClearTrackedTargets()
    {
        lockOnTarget = null;
        bossTarget = null;
    }

    private void SanitizeCameraMode()
    {
        if (bossTarget == null && currentMode == CameraMode.Boss)
        {
            currentMode = CameraMode.Exploration;
        }

        if (lockOnTarget == null && currentMode == CameraMode.LockOn)
        {
            currentMode = CameraMode.Combat;
        }
    }

    private Vector3 GetActiveTargetOffset()
    {
        switch (currentMode)
        {
            case CameraMode.Combat:
                return combatTargetOffset;
            case CameraMode.LockOn:
                return lockOnCameraOffset;
            case CameraMode.Boss:
                return bossCameraOffset;
            default:
                return targetOffset;
        }
    }

    private float GetActiveDistance()
    {
        switch (currentMode)
        {
            case CameraMode.Combat:
                return combatDistance;
            case CameraMode.LockOn:
                return lockOnDistance;
            case CameraMode.Boss:
                return bossDistance;
            default:
                return distance;
        }
    }

    private float GetActiveClampedDistance()
    {
        // Luôn dùng distance (đã chỉnh bằng scroll) — zoom hoạt động ở mọi camera mode.
        return Mathf.Clamp(distance, minDistance, maxDistance);
    }

    private float GetActiveSmoothSpeed()
    {
        switch (currentMode)
        {
            case CameraMode.Combat:
                return combatSmoothSpeed;
            case CameraMode.LockOn:
                return Mathf.Max(smoothSpeed, lockOnRotationSpeed);
            case CameraMode.Boss:
                return Mathf.Max(smoothSpeed, bossRotationSpeed);
            default:
                return smoothSpeed;
        }
    }

    private void UpdateLookAtAngles(Vector3 lookTargetPosition, float rotationSpeed)
    {
        Vector3 focusPoint = target.position + Vector3.up * targetOffset.y;
        Vector3 lookDirection = lookTargetPosition - focusPoint;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float targetYaw = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;
        yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateBossAngles()
    {
        Vector3 playerFocus = target.position + Vector3.up * targetOffset.y;
        Vector3 bossFocus = bossTarget.position + bossTargetOffset;
        Vector3 framedPoint = Vector3.Lerp(playerFocus, bossFocus, bossFramingWeight);
        UpdateLookAtAngles(framedPoint, bossRotationSpeed);
    }

    private Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#else
        return Vector2.zero;
#endif
    }

    private bool IsRotatingCamera()
    {
        if (!rotateOnlyWhileHoldingRightMouse)
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.rightButton.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(1);
#else
        return false;
#endif
    }

    private void ApplyCursorState(bool isRotating)
    {
        if (isRotating && hideCursorWhileRotating)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = showCursorOnStart;
    }

    private float GetMouseScroll()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.scroll.ReadValue().y / 120f;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mouseScrollDelta.y;
#else
        return 0f;
#endif
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }
}
