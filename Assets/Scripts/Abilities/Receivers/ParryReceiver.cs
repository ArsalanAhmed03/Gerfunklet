using Unity.Netcode;
using UnityEngine;

public class ParryReceiver : NetworkBehaviour
{
    private NetworkVariable<bool> isParryActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float parryEndTime;

    public bool IsParryActive => isParryActive.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ActivateParryServerRpc(float windowSeconds)
    {
        if (windowSeconds <= 0f) return;

        isParryActive.Value = true;
        parryEndTime = Time.time + windowSeconds;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!isParryActive.Value) return;

        if (Time.time >= parryEndTime)
            isParryActive.Value = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        isParryActive.Value = false;
        parryEndTime = 0f;
    }

}
