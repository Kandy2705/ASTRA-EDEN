using UnityEngine;

public abstract class EnemyBossBehaviour : MonoBehaviour
{
    [Header("Visual Orientation")]
    [SerializeField] protected Transform visualRoot;
    [SerializeField, Range(-180f, 180f)]
    protected float visualYawOffset;

    private bool visualOffsetApplied;

    public virtual void Initialize(EnemyAIController owner)
    {
        ApplyVisualOrientation();
    }

    /// <summary>
    /// Khoảng cách khiến AI dừng chạy để đánh gần.
    /// Mặc định giữ nguyên cách tính cũ.
    /// </summary>
    public virtual float GetEffectiveAttackRange(EnemyData data)
    {
        float range = data != null ? data.attackRange : 2f;

        if (data == null || data.attackPatterns == null)
        {
            return range;
        }

        foreach (AttackPatternData pattern in data.attackPatterns)
        {
            if (pattern != null)
            {
                range = Mathf.Max(range, pattern.maxRange);
            }
        }

        return range;
    }

    /// <summary>
    /// Cho phép boss bắt đầu một đòn đặc biệt ngoài tầm đánh gần.
    /// </summary>
    public virtual bool CanStartSpecialAttack(
        EnemyData data,
        float distance,
        float cooldownRemaining)
    {
        return false;
    }

    protected static bool IsProjectile(AttackPatternData pattern)
    {
        return pattern != null &&
               (pattern.rangeType == EnemyAttackRangeType.Projectile ||
                pattern.rangeType == EnemyAttackRangeType.ProjectileAOE);
    }

    private void ApplyVisualOrientation()
    {
        if (visualOffsetApplied ||
            Mathf.Abs(visualYawOffset) <= 0.001f)
        {
            return;
        }

        Transform visual = ResolveVisualRoot();
        if (visual == null)
        {
            Debug.LogWarning(
                $"[{name}] Không tìm thấy Visual Root. Kéo object con chứa model (U3DMesh) vào Visual Root để xoay hình ảnh.",
                this);
            return;
        }

        visual.localRotation =
            Quaternion.Euler(0f, visualYawOffset, 0f) *
            visual.localRotation;

        Debug.Log(
            $"[BOSS-FIX] {name}: đã xoay visual {visualYawOffset}° áp vào '{FullPath(visual)}'",
            this);

        visualOffsetApplied = true;
    }

    private static string FullPath(Transform target)
    {
        string path = target.name;
        Transform parent = target.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    /// <summary>
    /// Visual Root đã gán trong Inspector được ưu tiên. Nếu để trống (hoặc đang trỏ
    /// vào AI root) thì tự tìm object chứa SkinnedMeshRenderer — node này không phải
    /// bone nên Animator không ghi đè rotation của nó.
    /// </summary>
    private Transform ResolveVisualRoot()
    {
        if (visualRoot != null && visualRoot != transform)
        {
            return visualRoot;
        }

        SkinnedMeshRenderer mesh = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (mesh != null && mesh.transform != transform)
        {
            return mesh.transform;
        }

        return null;
    }
}
