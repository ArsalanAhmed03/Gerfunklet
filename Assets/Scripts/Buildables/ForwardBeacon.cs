using Unity.Netcode;
using UnityEngine;

public class ForwardBeacon : NetworkBehaviour
{
    public NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public ulong OwnerClientId => ownerClientId.Value;

    public void SetOwnerServer(ulong clientId)
    {
        if (!IsServer) return;
        ownerClientId.Value = clientId;
    }
}
