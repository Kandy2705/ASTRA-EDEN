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

    /// <summary>
    /// Hook dành cho boss cần hitbox bám một bone đang animation. Mặc định giữ
    /// nguyên hitbox root để không đổi behaviour các enemy/boss khác.
    /// </summary>
    public virtual void ConfigureAttackHitbox(
        EnemyAttackHitbox hitbox,
        AttackPatternData pattern)
    {
    }

    /// <summary>
    /// Cho boss ưu tiên một pattern cụ thể (vd. đòn R khi sắp hết máu).
    /// Trả null để dùng random/weighted selection mặc định.
    /// </summary>
    public virtual AttackPatternData SelectPriorityAttackPattern(
        EnemyData data,
        float distanceToPlayer)
    {
        return null;
    }

    /// <summary>
    /// Boss-specific action that temporarily owns the FSM (for example a
    /// summon or phase transition). Regular enemies keep the default false.
    /// </summary>
    public virtual bool CanStartExclusiveAction(
        EnemyData data,
        float distanceToPlayer,
        float attackCooldownRemaining)
    {
        return false;
    }

    public virtual void BeginExclusiveAction(Animator animator, Transform player)
    {
    }

    /// <returns>True when the action is complete and AI may return to Chase.</returns>
    public virtual bool TickExclusiveAction(float deltaTime, Animator animator, Transform player)
    {
        return true;
    }

    /// <summary>
    /// Called whenever Hurt/Stagger/Dead invalidates an exclusive action.
    /// Implementations should invalidate delayed animation events here.
    /// </summary>
    public virtual void CancelExclusiveAction()
    {
    }

    public virtual void Anim_OnExclusiveActionEvent()
    {
    }

    public virtual void Anim_OnExclusiveActionEnd()
    {
    }

    public virtual bool ExclusiveActionCanBeInterrupted => true;

    public virtual float GetMovementSpeedMultiplier() => 1f;

    public virtual float GetAttackDamageMultiplier() => 1f;

    public virtual float GetAttackCooldownMultiplier() => 1f;

    public virtual void OnOwnerDeath()
    {
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
