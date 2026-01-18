using Unity.Netcode;
using UnityEngine;

public class RootReceiver : NetworkBehaviour
{
    private NetworkVariable<bool> isRooted = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float _endTime;

    public bool IsRooted => isRooted.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyRootServerRpc(float duration)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;

        isRooted.Value = true;
        _endTime = Time.time + duration;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        isRooted.Value = false;
        _endTime = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!isRooted.Value) return;
        if (Time.time >= _endTime)
            isRooted.Value = false;
    }
}
