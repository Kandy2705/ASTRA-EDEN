using System.Collections.Generic;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAttackHitbox : MonoBehaviour
{
    public enum HitShape { Sphere, Box }

    [Header("Shape")]
    [SerializeField] private HitShape shape = HitShape.Sphere;
    [SerializeField] private float radius = 0.6f;
    [SerializeField] private Vector3 boxHalfExtents = new Vector3(0.5f, 0.5f, 0.6f);

    [Header("Offset (local to this transform)")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1f, 0.8f);

    [Header("Filter")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField, Min(1f)] private float minimumHitInterval = 1f;
    [Tooltip("Bỏ qua sensor/interaction trigger của Player để melee chỉ trúng collider thân thật.")]
    [SerializeField] private bool ignoreTriggerColliders;

    [Header("Debug Gizmo")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.3f, 0.3f, 0.4f);
    [SerializeField] private bool drawGizmoAlways = false;

    private static readonly Collider[] HitBuffer = new Collider[16];
    private readonly HashSet<CharacterHealth> hitThisSwing = new HashSet<CharacterHealth>();
    private readonly Dictionary<CharacterHealth, float> nextAllowedHitTime =
        new Dictionary<CharacterHealth, float>();

    HitShape defaultShape;
    float defaultRadius;
    Vector3 defaultBoxHalfExtents;
    Vector3 defaultLocalOffset;
    float activeKnockbackDistance;
    float activeKnockbackDuration = 0.2f;
    float activeKnockbackVerticalLift;
    Transform dynamicAnchor;

    public LayerMask TargetLayer => targetLayer;

    /// <summary>
    /// Raised only after this swing actually damages at least one target. This
    /// lets a boss play impact SFX without playing a false "hit" sound when it
    /// swings into empty space.
    /// </summary>
    public event Action<int> DamageApplied;

    public void SetTargetLayer(LayerMask layer)
    {
        targetLayer = layer;
    }

    void Awake()
    {
        CaptureDefaultConfiguration();
    }

    public void CaptureDefaultConfiguration()
    {
        defaultShape = shape;
        defaultRadius = radius;
        defaultBoxHalfExtents = boxHalfExtents;
        defaultLocalOffset = localOffset;
    }

    public void ApplyPatternConfiguration(AttackPatternData pattern)
    {
        // Attack thường dùng hitbox ở root. Boss-specific behaviour có thể gắn
        // riêng một đòn vào bone (vd. TailWhip) ngay sau bước này.
        dynamicAnchor = null;

        if (pattern != null && pattern.overrideHitbox)
        {
            shape = pattern.hitboxShape;
            radius = Mathf.Max(0.05f, pattern.hitboxRadius);
            boxHalfExtents = new Vector3(
                Mathf.Max(0.05f, pattern.hitboxHalfExtents.x),
                Mathf.Max(0.05f, pattern.hitboxHalfExtents.y),
                Mathf.Max(0.05f, pattern.hitboxHalfExtents.z));
            localOffset = pattern.hitboxLocalOffset;
        }
        else
        {
            shape = defaultShape;
            radius = defaultRadius;
            boxHalfExtents = defaultBoxHalfExtents;
            localOffset = defaultLocalOffset;
        }

        activeKnockbackDistance = pattern != null
            ? Mathf.Max(0f, pattern.knockbackDistance)
            : 0f;
        activeKnockbackDuration = pattern != null
            ? Mathf.Max(0.01f, pattern.knockbackDuration)
            : 0.2f;
        activeKnockbackVerticalLift = pattern != null
            ? Mathf.Max(0f, pattern.knockbackVerticalLift)
            : 0f;
    }

    /// <summary>
    /// Cho phép một đòn đặc biệt đặt tâm hitbox trên bone đang animate, thay vì
    /// quét một vùng tĩnh quanh root của enemy.
    /// </summary>
    public void SetDynamicAnchor(Transform anchor)
    {
        dynamicAnchor = anchor;
    }

    /// <summary>Gọi khi bắt đầu 1 đòn đánh — reset danh sách target đã trúng.</summary>
    public void BeginSwing()
    {
        hitThisSwing.Clear();
    }

    /// <summary>Quét hitbox tại 1 frame impact và gây dame cho tất cả target hợp lệ chưa trúng trong swing này.</summary>
    public int PerformHit(float damage)
    {
        Vector3 worldCenter = GetWorldCenter();
        QueryTriggerInteraction triggerInteraction = ignoreTriggerColliders
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;
        int count = shape == HitShape.Sphere
            ? Physics.OverlapSphereNonAlloc(worldCenter, radius, HitBuffer, targetLayer, triggerInteraction)
            : Physics.OverlapBoxNonAlloc(worldCenter, boxHalfExtents, HitBuffer, transform.rotation, targetLayer, triggerInteraction);

        int dealt = 0;
        for (int i = 0; i < count; i++)
        {
            Collider col = HitBuffer[i];
            if (col == null) continue;
            if (ignoreTriggerColliders && col.isTrigger) continue;

            CharacterHealth health = col.GetComponentInParent<CharacterHealth>();
            if (health == null || health.IsDead) continue;
            // Player đã chết (anim death) — không đánh nữa.
            if (health.CompareTag("Player") || health.transform.root.CompareTag("Player"))
            {
                if (PlayerDeathController.IsPlayerDead) continue;
            }

            if (!hitThisSwing.Add(health)) continue;

            if (nextAllowedHitTime.TryGetValue(health, out float nextHitTime) &&
                Time.time < nextHitTime)
            {
                continue;
            }

            nextAllowedHitTime[health] =
                Time.time + Mathf.Max(1f, minimumHitInterval);
            health.TakeDamage(damage);

            if (activeKnockbackDistance > 0f)
            {
                PlayerKnockbackReceiver receiver =
                    health.GetComponentInParent<PlayerKnockbackReceiver>();
                if (receiver != null)
                {
                    Vector3 pushDirection = health.transform.position - transform.root.position;
                    pushDirection.y = 0f;
                    if (pushDirection.sqrMagnitude <= 0.001f)
                    {
                        pushDirection = transform.forward;
                    }

                    receiver.ApplyKnockback(
                        pushDirection.normalized,
                        activeKnockbackDistance,
                        activeKnockbackDuration,
                        activeKnockbackVerticalLift);
                }
            }
            dealt++;
        }

        if (dealt > 0)
        {
            DamageApplied?.Invoke(dealt);
        }

        return dealt;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmoAlways) return;
        DrawGizmoShape();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawGizmoAlways) return;
        DrawGizmoShape();
    }

    private void DrawGizmoShape()
    {
        Gizmos.color = gizmoColor;
        Vector3 worldCenter = GetWorldCenter();
        if (shape == HitShape.Sphere)
        {
            Gizmos.DrawWireSphere(worldCenter, radius);
        }
        else
        {
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(worldCenter, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2f);
            Gizmos.matrix = prev;
        }
    }

    Vector3 GetWorldCenter()
    {
        Transform anchor = dynamicAnchor != null ? dynamicAnchor : transform;
        return anchor.TransformPoint(localOffset);
    }
}
