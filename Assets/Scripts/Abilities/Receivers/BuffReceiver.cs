using Unity.Netcode;
using UnityEngine;

public class BuffReceiver : NetworkBehaviour
{
    private NetworkVariable<float> moveSpeedMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private NetworkVariable<float> attackSpeedMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float buffEndTime;

    public float MoveSpeedMultiplier => moveSpeedMultiplier.Value;
    public float AttackSpeedMultiplier => attackSpeedMultiplier.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyMoveSpeedBuffServerRpc(float multiplier, float duration)
    {
        ApplyBuffServer(multiplier, attackSpeedMultiplier.Value, duration);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyAttackSpeedBuffServerRpc(float multiplier, float duration)
    {
        ApplyBuffServer(moveSpeedMultiplier.Value, multiplier, duration);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyBuffServerRpc(float moveMultiplier, float attackMultiplier, float duration)
    {
        ApplyBuffServer(moveMultiplier, attackMultiplier, duration);
    }

    private void ApplyBuffServer(float moveMultiplier, float attackMultiplier, float duration)
    {
        if (!IsServer) return;
        moveSpeedMultiplier.Value = moveMultiplier;
        attackSpeedMultiplier.Value = attackMultiplier;
        buffEndTime = Time.time + duration;
    }

    private void Update()
    {
        if (!IsServer) return;

        if ((moveSpeedMultiplier.Value != 1f || attackSpeedMultiplier.Value != 1f) &&
            Time.time >= buffEndTime)
        {
            moveSpeedMultiplier.Value = 1f;
            attackSpeedMultiplier.Value = 1f;
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        moveSpeedMultiplier.Value = 1f;
        attackSpeedMultiplier.Value = 1f;
        buffEndTime = 0f;
    }

}
