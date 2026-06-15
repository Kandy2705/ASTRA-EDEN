using UnityEngine;

/// <summary>
/// Gắn vào GameObject có Animator của player (thường là model con).
/// Animation Event trong clip Attack/Skill gọi function OnAttackHit hoặc OnAttackEnd ở đây
/// → relay ngược lên PlayerCombatController ở root.
/// </summary>
public class PlayerAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerCombatController owner;

    private void Reset()
    {
        owner = GetComponentInParent<PlayerCombatController>();
    }

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<PlayerCombatController>();
        }
    }

    /// <summary>Gọi từ Animation Event tại frame impact của clip.</summary>
    public void OnAttackHit()
    {
        if (owner != null) owner.OnAttackHit();
    }

    /// <summary>Optional: gọi cuối clip để reset swing state.</summary>
    public void OnAttackEnd()
    {
        if (owner != null) owner.OnAttackEnd();
    }
}
