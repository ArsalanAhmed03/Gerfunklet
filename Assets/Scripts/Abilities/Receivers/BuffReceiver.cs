using Unity.Netcode;
using UnityEngine;

public class BuffReceiver : NetworkBehaviour
{
    private NetworkVariable<float> moveSpeedMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float buffEndTime;

    public float MoveSpeedMultiplier => moveSpeedMultiplier.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyMoveSpeedBuffServerRpc(float multiplier, float duration)
    {
        moveSpeedMultiplier.Value = multiplier;
        buffEndTime = Time.time + duration;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (moveSpeedMultiplier.Value != 1f && Time.time >= buffEndTime)
        {
            moveSpeedMultiplier.Value = 1f;
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        moveSpeedMultiplier.Value = 1f;
        buffEndTime = 0f;
    }

}
