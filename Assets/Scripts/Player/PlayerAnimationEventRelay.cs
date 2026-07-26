using UnityEngine;

/// <summary>
/// Gắn vào GameObject có Animator của player (thường là model con).
/// Animation Event trong clip Attack/Skill gọi function OnAttackHit hoặc OnAttackEnd ở đây
/// → relay ngược lên PlayerCombatController ở root.
///
/// Cũng relay các sự kiện âm thanh đến PlayerAudioController.
/// </summary>
public class PlayerAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerCombatController owner;
    [SerializeField] private PlayerAudioController audioController;

    private void Reset()
    {
        owner = GetComponentInParent<PlayerCombatController>();
        audioController = GetComponentInParent<PlayerAudioController>();
    }

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<PlayerCombatController>();
        }
        if (audioController == null)
        {
            audioController = GetComponentInParent<PlayerAudioController>();
        }
    }

    /// <summary>Gọi từ Animation Event tại frame impact của clip.</summary>
    public void OnAttackHit()
    {
        if (owner != null) owner.OnAttackHit();
    }

    /// <summary>Bắt đầu vùng sát thương chiêu R (có thể gọi thay cho OnAttackHit).</summary>
    public void OnAreaDamageStart()
    {
        if (owner != null) owner.OnAreaDamageStart();
    }

    /// <summary>Dừng vùng sát thương chiêu R.</summary>
    public void OnAreaDamageEnd()
    {
        if (owner != null) owner.OnAreaDamageEnd();
    }

    /// <summary>Optional: gọi cuối clip để reset swing state.</summary>
    public void OnAttackEnd()
    {
        if (owner != null) owner.OnAttackEnd();
    }

    // ============================================
    // ANIMATION EVENTS CHO ÂM THANH
    // ============================================

    /// <summary>Gọi từ Animation Event: PlaySlashSound</summary>
    public void PlaySlashSound()
    {
        if (audioController != null) audioController.OnPlaySlashSound();
    }

    /// <summary>Gọi từ Animation Event: PlayScoreSound</summary>
    public void PlayScoreSound()
    {
        if (audioController != null) audioController.OnPlayScoreSound();
    }

    /// <summary>Gọi từ Animation Event: PlaySkillSound</summary>
    public void PlaySkillSound()
    {
        if (audioController != null) audioController.OnPlaySkillSound();
    }

    /// <summary>Gọi từ Animation Event: PlayAttackSound</summary>
    public void PlayAttackSound()
    {
        if (audioController != null) audioController.OnPlayAttackSound();
    }
}
