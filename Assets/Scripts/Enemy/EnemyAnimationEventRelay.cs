using UnityEngine;

/// <summary>
/// Gắn vào GameObject có Animator của enemy.
/// Animation Event gọi OnAttackStart / OnAttackHit → relay lên EnemyAIController hoặc EnemyPatrol.
/// </summary>
public class EnemyAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private EnemyAIController aiOwner;
    [SerializeField] private EnemyPatrol patrolOwner;
    [SerializeField] private EnemyPushHitbox tacklePushHitbox;
    [SerializeField] private DinosaurVocalAudio vocalAudio;

    private void Reset()
    {
        aiOwner = GetComponentInParent<EnemyAIController>();
        patrolOwner = GetComponentInParent<EnemyPatrol>();
    }

    private void Awake()
    {
        if (aiOwner == null)
        {
            aiOwner = GetComponentInParent<EnemyAIController>();
        }

        if (patrolOwner == null)
        {
            patrolOwner = GetComponentInParent<EnemyPatrol>();
        }

        if (tacklePushHitbox == null)
        {
            EnemyAIController controller = GetComponentInParent<EnemyAIController>();
            if (controller != null)
            {
                tacklePushHitbox = controller.GetComponentInChildren<EnemyPushHitbox>(true);
            }
        }

        if (vocalAudio == null)
        {
            vocalAudio = GetComponentInParent<DinosaurVocalAudio>();
        }
    }

    public void OnAttackStart()
    {
        if (vocalAudio != null)
        {
            vocalAudio.PlayAttack();
        }

        if (aiOwner != null)
        {
            aiOwner.Anim_OnAttackStart();
            return;
        }

        if (patrolOwner != null)
        {
            patrolOwner.BeginAttackSwing();
        }
    }

    public void OnAttackHit()
    {
        if (aiOwner != null)
        {
            aiOwner.Anim_OnAttackHit();
            return;
        }

        if (patrolOwner != null)
        {
            patrolOwner.PerformAttackHit();
        }
    }

    /// <summary>Kết thúc một attack pattern thường (không phải tackle).</summary>
    public void OnAttackEnd()
    {
        if (aiOwner != null)
        {
            aiOwner.Anim_OnAttackEnd();
        }
    }

    public void OpenTackleHitbox()
    {
        if (tacklePushHitbox != null)
        {
            tacklePushHitbox.OpenHitbox();
        }
    }

    public void CloseTackleHitbox()
    {
        if (tacklePushHitbox != null)
        {
            tacklePushHitbox.CloseHitbox();
        }
    }

    public void OnAttackFinished()
    {
        CloseTackleHitbox();

        if (aiOwner != null)
        {
            aiOwner.Anim_OnTackleFinished();
        }
    }
}
