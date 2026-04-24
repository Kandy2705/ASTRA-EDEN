using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0.45f, 1.55f, 0f);

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

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionBuffer = 0.15f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;

    private float yaw;
    private float pitch = 15f;
    private Vector3 currentVelocity;

    private void Start()
    {
        Vector3 startAngles = transform.eulerAngles;
        yaw = startAngles.y;
        pitch = NormalizeAngle(startAngles.x);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // 1. Read camera input after gameplay movement has already updated.
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
        return Quaternion.Euler(pitch, yaw, 0f);
    }

    private Vector3 CalculateFocusPoint()
    {
        // Offset follows the camera yaw, which keeps over-the-shoulder framing consistent.
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        return target.position + yawRotation * targetOffset;
    }

    private Vector3 CalculateDesiredPosition(Vector3 focusPoint, Quaternion cameraRotation)
    {
        float clampedDistance = Mathf.Clamp(distance, minDistance, maxDistance);
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
        float damping = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            Mathf.Max(0.001f, 1f / smoothSpeed)
        );

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, damping);
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
