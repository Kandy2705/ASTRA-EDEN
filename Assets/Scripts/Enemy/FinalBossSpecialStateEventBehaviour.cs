using UnityEngine;

/// <summary>
/// Delivers normalized-time events without modifying the Player animation
/// clips reused by the Final Boss. The captured token rejects stale state
/// callbacks after Hurt/Stagger has cancelled a Summon.
/// </summary>
public sealed class FinalBossSpecialStateEventBehaviour : StateMachineBehaviour
{
    [SerializeField] private bool fireActionEvent;
    [SerializeField, Range(0f, 1f)] private float actionEventNormalizedTime = 0.55f;
    [SerializeField, Range(0f, 1f)] private float actionEndNormalizedTime = 0.94f;

    FinalBossBehaviour behaviour;
    int expectedToken;
    bool eventSent;
    bool endSent;

    public void Configure(bool shouldFireActionEvent, float eventTime, float endTime)
    {
        fireActionEvent = shouldFireActionEvent;
        actionEventNormalizedTime = Mathf.Clamp01(eventTime);
        actionEndNormalizedTime = Mathf.Clamp01(endTime);
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        behaviour = animator.GetComponentInParent<FinalBossBehaviour>();
        expectedToken = behaviour != null ? behaviour.CurrentActionToken : -1;
        eventSent = false;
        endSent = false;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (behaviour == null || behaviour.CurrentActionToken != expectedToken)
        {
            return;
        }

        float normalized = stateInfo.normalizedTime;
        if (fireActionEvent && !eventSent && normalized >= actionEventNormalizedTime)
        {
            eventSent = true;
            behaviour.ResolveExclusiveActionEvent(expectedToken);
        }

        if (!endSent && normalized >= actionEndNormalizedTime)
        {
            endSent = true;
            behaviour.RequestExclusiveActionEnd(expectedToken);
        }
    }
}
