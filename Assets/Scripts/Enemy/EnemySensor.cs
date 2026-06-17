using UnityEngine;

/// <summary>
/// FOV + LOS + hearing perception cho enemy. Đọc range từ EnemyData (nếu có), fallback giá trị inspector.
/// Không tự quản lý state — chỉ trả về có thấy/cảm nhận target hay không.
/// </summary>
[DisallowMultipleComponent]
public class EnemySensor : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Nếu gán EnemyData, range sẽ đọc từ đó. Bỏ trống thì dùng giá trị overrides bên dưới.")]
    [SerializeField] private EnemyData enemyData;

    [Header("Overrides (fallback khi không có EnemyData)")]
    [SerializeField, Min(0f)] private float sightRangeOverride = 14f;
    [SerializeField, Range(10f, 360f)] private float sightAngleOverride = 110f;
    [SerializeField, Min(0f)] private float hearingRangeOverride = 7f;

    [Header("Eye / Target Sensors")]
    [Tooltip("Transform mắt để raycast LOS. Bỏ trống sẽ dùng transform.position + eyeHeight.")]
    [SerializeField] private Transform eyeSensor;
    [SerializeField, Min(0f)] private float eyeHeight = 1.6f;
    [Tooltip("Cao độ ngực target để raycast tới (tránh mặt đất).")]
    [SerializeField, Min(0f)] private float targetChestHeight = 1.0f;

    [Header("LOS")]
    [Tooltip("Layer của vật cản che tầm nhìn. KHÔNG bao gồm layer Player.")]
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color sightColor = new Color(1f, 0.8f, 0f, 0.25f);
    [SerializeField] private Color hearingColor = new Color(0f, 0.6f, 1f, 0.25f);

    public float SightRange => enemyData != null ? enemyData.sightRange : sightRangeOverride;
    public float SightAngle => enemyData != null ? enemyData.sightAngle : sightAngleOverride;
    public float HearingRange => enemyData != null ? enemyData.hearingRange : hearingRangeOverride;

    public void Configure(EnemyData data)
    {
        enemyData = data;
    }

    /// <summary>Player có nằm trong FOV + LOS hoặc trong tầm nghe không?</summary>
    public bool CanSense(Transform target, out float distance)
    {
        distance = float.PositiveInfinity;
        if (target == null) return false;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        distance = toTarget.magnitude;

        // Hearing — bỏ qua FOV/LOS nếu vào sát.
        if (distance <= HearingRange) return true;

        if (distance > SightRange) return false;

        // FOV
        Vector3 forward = transform.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return false;
        forward.Normalize();
        Vector3 dirFlat = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : forward;

        float halfAngle = SightAngle * 0.5f;
        float angleToTarget = Vector3.Angle(forward, dirFlat);
        if (angleToTarget > halfAngle) return false;

        // LOS
        Vector3 eyePos = eyeSensor != null ? eyeSensor.position : transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = target.position + Vector3.up * targetChestHeight;
        Vector3 ray = targetPos - eyePos;
        float rayDist = ray.magnitude;
        if (rayDist <= 0.01f) return true;

        if (Physics.Raycast(eyePos, ray.normalized, out RaycastHit hit, rayDist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            // Có vật cản giữa mắt và target → mất LOS.
            if (!hit.transform.IsChildOf(target) && hit.transform != target) return false;
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        // Sight cone
        Gizmos.color = sightColor;
        Gizmos.DrawWireSphere(transform.position, SightRange);

        Vector3 forward = transform.forward;
        float half = SightAngle * 0.5f;
        Quaternion left = Quaternion.Euler(0f, -half, 0f);
        Quaternion right = Quaternion.Euler(0f, half, 0f);
        Vector3 leftDir = left * forward * SightRange;
        Vector3 rightDir = right * forward * SightRange;
        Gizmos.DrawLine(transform.position, transform.position + leftDir);
        Gizmos.DrawLine(transform.position, transform.position + rightDir);

        // Hearing
        Gizmos.color = hearingColor;
        Gizmos.DrawWireSphere(transform.position, HearingRange);
    }
}
