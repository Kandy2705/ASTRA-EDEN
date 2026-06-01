using UnityEngine;

public class PlayerAnimatorBridge : MonoBehaviour
{
    private static readonly int BlendHash = Animator.StringToHash("Blend");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [SerializeField] private Animator animator;
    [SerializeField] private float animatorDampTime = 0.1f;

    private float normalAnimatorSpeed = 1f;

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
        if (animator == null)
        {
            return;
        }

        animator.SetFloat(BlendHash, speedFactor, animatorDampTime, Time.deltaTime);
        animator.SetFloat(HorizontalHash, movementInput.x, animatorDampTime, Time.deltaTime);
        animator.SetFloat(VerticalHash, movementInput.y, animatorDampTime, Time.deltaTime);
        animator.SetBool(IsGroundedHash, isGrounded);
    }

    public void TriggerJump()
    {
        if (animator != null)
        {
            animator.SetTrigger(JumpHash);
        }
    }

    public void TriggerAttack()
    {
        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }
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
