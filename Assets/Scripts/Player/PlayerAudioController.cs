using UnityEngine;

/// <summary>
/// Quản lý tất cả âm thanh của Player.
/// Gắn vào Player root (cùng level với PlayerCombatController).
/// Sử dụng AudioManager của game để phát âm thanh.
/// </summary>
public class PlayerAudioController : MonoBehaviour
{
    [Header("Audio Clips")]
    [SerializeField] private AudioClip swordSlashClip;
    [SerializeField] private AudioClip[] attackClips; // Các âm thanh attack khác nhau
    [SerializeField] private AudioClip scoreClip; // Âm thanh khi chấm điểm
    [SerializeField] private AudioClip[] skillClips; // Âm thanh cho các skill

    [Header("Settings")]
    [SerializeField, Range(0f, 2f)] private float attackPitch = 1f;
    [SerializeField, Range(0f, 0.5f)] private float attackPitchRandomRange = 0.1f;

    private PlayerCombatController combatController;
    private int lastAttackClipIndex = -1;

    private void Reset()
    {
        combatController = GetComponent<PlayerCombatController>();
    }

    private void Awake()
    {
        if (combatController == null)
        {
            combatController = GetComponent<PlayerCombatController>();
        }
    }

    /// <summary>
    /// Phát âm thanh chém kiếm (sword slash).
    /// Gọi từ PlayerCombatController hoặc Animation Event.
    /// </summary>
    public void PlaySlashSound()
    {
        if (swordSlashClip == null)
        {
            Debug.LogWarning("[PlayerAudioController] swordSlashClip chưa được assign!");
            return;
        }

        PlayAttackSound(swordSlashClip);
    }

    /// <summary>
    /// Phát âm thanh attack ngẫu nhiên từ mảng attackClips.
    /// </summary>
    public void PlayRandomAttackSound()
    {
        if (attackClips == null || attackClips.Length == 0)
        {
            PlaySlashSound();
            return;
        }

        int randomIndex;
        do
        {
            randomIndex = Random.Range(0, attackClips.Length);
        } while (attackClips.Length > 1 && randomIndex == lastAttackClipIndex);

        lastAttackClipIndex = randomIndex;
        PlayAttackSound(attackClips[randomIndex]);
    }

    /// <summary>
    /// Phát âm thanh chấm điểm (score).
    /// </summary>
    public void PlayScoreSound()
    {
        if (scoreClip == null)
        {
            Debug.LogWarning("[PlayerAudioController] scoreClip chưa được assign!");
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(scoreClip, 1f);
        }
        else
        {
            Debug.LogWarning("[PlayerAudioController] AudioManager chưa có instance!");
        }
    }

    /// <summary>
    /// Phát âm thanh skill.
    /// </summary>
    public void PlaySkillSound(int skillIndex = 0)
    {
        if (skillClips != null && skillIndex >= 0 && skillIndex < skillClips.Length && skillClips[skillIndex] != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(skillClips[skillIndex], 1f);
            }
            return;
        }

        // Fallback: dùng swordSlashClip
        PlaySlashSound();
    }

    /// <summary>
    /// Phát âm thanh attack với clip cụ thể.
    /// </summary>
    private void PlayAttackSound(AudioClip clip)
    {
        if (clip == null) return;

        if (AudioManager.Instance != null)
        {
            float randomPitch = attackPitch + Random.Range(-attackPitchRandomRange, attackPitchRandomRange);
            AudioManager.Instance.PlaySfx(clip, 1f);
            // Note: AudioManager.PlaySfx hiện tại chưa hỗ trợ pitch, có thể thêm sau
        }
        else
        {
            Debug.LogWarning("[PlayerAudioController] AudioManager chưa có instance!");
        }
    }

    // ============================================
    // CÁC PHƯƠNG THỨC GỌI TỪ ANIMATION EVENT
    // ============================================

    /// <summary>
    /// Gọi từ Animation Event: PlaySlashSound
    /// </summary>
    public void OnPlaySlashSound()
    {
        PlaySlashSound();
    }

    /// <summary>
    /// Gọi từ Animation Event: PlayScoreSound
    /// </summary>
    public void OnPlayScoreSound()
    {
        PlayScoreSound();
    }

    /// <summary>
    /// Gọi từ Animation Event: PlaySkillSound
    /// </summary>
    public void OnPlaySkillSound()
    {
        PlaySkillSound();
    }

    /// <summary>
    /// Gọi từ Animation Event: PlayAttackSound
    /// </summary>
    public void OnPlayAttackSound()
    {
        PlayRandomAttackSound();
    }

    // ============================================
    // TÍCH HỢP VỚI PLAYER COMBAT CONTROLLER
    // ============================================

    /// <summary>
    /// Được gọi từ PlayerCombatController khi bắt đầu attack.
    /// </summary>
    public void OnAttackStarted(int skillIndex)
    {
        // Có thể phát âm thanh ngay lúc bắt đầu attack
        // Hoặc đợi Animation Event gọi sau
        // PlaySlashSound();
    }

    /// <summary>
    /// Được gọi từ PlayerCombatController khi OnAttackHit.
    /// Đây là thời điểm lý tưởng để phát âm thanh chém!
    /// </summary>
    public void OnAttackHitSound()
    {
        // Phát âm thanh khi trúng địch (lúc này animation đang ở frame impact)
        PlaySlashSound();
    }

    /// <summary>
    /// Được gọi từ PlayerCombatController khi kết thúc attack.
    /// </summary>
    public void OnAttackEndSound()
    {
        // Có thể phát âm thanh kết thúc
    }
}
