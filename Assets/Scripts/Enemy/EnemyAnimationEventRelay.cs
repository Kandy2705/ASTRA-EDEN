using UnityEngine;

/// <summary>
/// Gắn vào GameObject có Animator của enemy.
/// Animation Event gọi OnAttackStart / OnAttackHit → relay lên EnemyAIController hoặc EnemyPatrol.
/// </summary>
public class EnemyAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private EnemyAIController aiOwner;
    [SerializeField] private EnemyPatrol patrolOwner;

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
    }

    public void OnAttackStart()
    {
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
}