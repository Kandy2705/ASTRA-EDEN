using System.Collections.Generic;
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

    [Header("Debug Gizmo")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.3f, 0.3f, 0.4f);
    [SerializeField] private bool drawGizmoAlways = false;

    private static readonly Collider[] HitBuffer = new Collider[16];
    private readonly HashSet<CharacterHealth> hitThisSwing = new HashSet<CharacterHealth>();
    private readonly Dictionary<CharacterHealth, float> nextAllowedHitTime =
        new Dictionary<CharacterHealth, float>();

    public LayerMask TargetLayer => targetLayer;

    public void SetTargetLayer(LayerMask layer)
    {
        targetLayer = layer;
    }

    /// <summary>Gọi khi bắt đầu 1 đòn đánh — reset danh sách target đã trúng.</summary>
    public void BeginSwing()
    {
        hitThisSwing.Clear();
    }

    /// <summary>Quét hitbox tại 1 frame impact và gây dame cho tất cả target hợp lệ chưa trúng trong swing này.</summary>
    public int PerformHit(float damage)
    {
        Vector3 worldCenter = transform.TransformPoint(localOffset);
        int count = shape == HitShape.Sphere
            ? Physics.OverlapSphereNonAlloc(worldCenter, radius, HitBuffer, targetLayer, QueryTriggerInteraction.Collide)
            : Physics.OverlapBoxNonAlloc(worldCenter, boxHalfExtents, HitBuffer, transform.rotation, targetLayer, QueryTriggerInteraction.Collide);

        int dealt = 0;
        for (int i = 0; i < count; i++)
        {
            Collider col = HitBuffer[i];
            if (col == null) continue;

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
            dealt++;
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
        Vector3 worldCenter = transform.TransformPoint(localOffset);
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
}
