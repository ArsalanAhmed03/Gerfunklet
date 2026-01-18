using Unity.Netcode;
using UnityEngine;

public class CombatDisableReceiver : NetworkBehaviour
{
    private NetworkVariable<bool> isDisabled = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float _endTime;

    public bool IsDisabled => isDisabled.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyDisableServerRpc(float duration)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;

        isDisabled.Value = true;
        _endTime = Time.time + duration;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        isDisabled.Value = false;
        _endTime = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!isDisabled.Value) return;
        if (Time.time >= _endTime)
            isDisabled.Value = false;
    }
}
