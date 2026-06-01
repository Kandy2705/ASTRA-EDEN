using UnityEngine;

/// <summary>
/// Gắn vào GameObject có Animator (thường là model con của enemy) để Animation Event
/// có thể gọi method ở đây và relay ngược lên EnemyPatrol ở parent.
/// Trong Animation Event chọn function: OnAttackStart / OnAttackHit.
/// </summary>
public class EnemyAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private EnemyPatrol owner;

    private void Reset()
    {
        owner = GetComponentInParent<EnemyPatrol>();
    }

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<EnemyPatrol>();
        }
    }

    /// <summary>Gọi từ Animation Event lúc bắt đầu swing (frame trước impact).</summary>
    public void OnAttackStart()
    {
        if (owner != null) owner.BeginAttackSwing();
    }

    /// <summary>Gọi từ Animation Event tại frame impact để quét hitbox.</summary>
    public void OnAttackHit()
    {
        if (owner != null) owner.PerformAttackHit();
    }
}
