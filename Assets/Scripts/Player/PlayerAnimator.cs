using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header("Animator Reference")]
    public Animator animator;

    private int isWalkingHash;
    private int attackTriggerHash;
    private int jumpTriggerHash;
    public int stompTriggerHash;

    private void Awake()
    {
        isWalkingHash = Animator.StringToHash("isWalking");
        attackTriggerHash = Animator.StringToHash("Attack");
        jumpTriggerHash = Animator.StringToHash("Jump");
        stompTriggerHash = Animator.StringToHash("Stomp");
    }

    /// <summary>
    /// Toggle walk animation (looping)
    /// </summary>
    public void SetMoving(bool isMoving)
    {
        animator.SetBool(isWalkingHash, isMoving);
    }

    /// <summary>
    /// Fire attack trigger
    /// </summary>
    public void Attack()
    {
        animator.SetTrigger(attackTriggerHash);
    }

    /// <summary>
    /// Fire jump trigger
    /// </summary>
    public void Jump()
    {
        animator.SetTrigger(jumpTriggerHash);
    }

    /// <summary>
    /// Fire stomp trigger
    /// </summary>
    public void Stomp()
    {
        animator.SetTrigger(stompTriggerHash);
    }
}
