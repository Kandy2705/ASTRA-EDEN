using UnityEngine;

public class PlayerAnimatorBridge : MonoBehaviour
{
    private static readonly int BlendHash = Animator.StringToHash("Blend");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int CastSkillHash = Animator.StringToHash("CastSkill");
    private static readonly int SkillIndexHash = Animator.StringToHash("SkillIndex");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    [SerializeField] private Animator animator;
    [SerializeField] private float animatorDampTime = 0.1f;

    private float normalAnimatorSpeed = 1f;
    private bool isDead;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    public void UpdateLocomotion(float speedFactor, Vector2 movementInput, bool isGrounded)
    {
        if (animator == null || isDead)
        {
            return;
        }

        animator.SetFloat(BlendHash, speedFactor, animatorDampTime, Time.deltaTime);
        animator.SetFloat(HorizontalHash, movementInput.x, animatorDampTime, Time.deltaTime);
        animator.SetFloat(VerticalHash, movementInput.y, animatorDampTime, Time.deltaTime);
        animator.SetBool(IsGroundedHash, isGrounded);
    }

    /// <summary>Gọi khi player chết — set bool IsDead, chặn locomotion/attack triggers.</summary>
    public void SetDead(bool dead)
    {
        isDead = dead;
        if (animator == null)
        {
            return;
        }

        if (HasParameter(IsDeadHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsDeadHash, dead);
        }

        if (dead)
        {
            animator.SetFloat(BlendHash, 0f);
            animator.SetFloat(HorizontalHash, 0f);
            animator.SetFloat(VerticalHash, 0f);
        }
    }

    public void TriggerJump()
    {
        if (animator != null && !isDead)
        {
            animator.SetTrigger(JumpHash);
        }
    }

    public void TriggerAttack()
    {
        if (animator != null && !isDead)
        {
            animator.SetTrigger(AttackHash);
        }
    }

    public void TriggerHit()
    {
        if (animator == null || isDead ||
            !HasParameter(HitHash, AnimatorControllerParameterType.Trigger))
        {
            return;
        }

        animator.ResetTrigger(HitHash);
        animator.SetTrigger(HitHash);
    }

    public void TriggerCastSkill(int skillIndex)
    {
        if (animator == null || isDead)
        {
            return;
        }

        if (HasParameter(SkillIndexHash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(SkillIndexHash, skillIndex);
        }
        else if (HasParameter(SkillIndexHash, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(SkillIndexHash, skillIndex);
        }

        if (HasParameter(CastSkillHash, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(CastSkillHash);
        }
        else
        {
            animator.SetTrigger(AttackHash);
        }
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == hash && parameter.type == type)
            {
                return true;
            }
        }

        return false;
    }

    public void PushPlaybackSpeed(float speed)
    {
        if (animator == null)
        {
            return;
        }

        normalAnimatorSpeed = animator.speed;
        animator.speed = speed;
    }

    public void RestorePlaybackSpeed()
    {
        if (animator != null)
        {
            animator.speed = normalAnimatorSpeed;
        }
    }
}
