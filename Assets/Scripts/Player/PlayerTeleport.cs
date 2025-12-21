using Unity.Netcode;
using UnityEngine;

public class PlayerTeleport : NetworkBehaviour
{
    // server calls this, it tells ONLY the owner to move itself
    [ClientRpc]
    public void TeleportOwnerClientRpc(Vector3 pos, Quaternion rot, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        transform.SetPositionAndRotation(pos, rot);
    }
}
