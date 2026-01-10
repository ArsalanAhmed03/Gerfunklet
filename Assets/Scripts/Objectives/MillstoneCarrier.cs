using Unity.Netcode;
using UnityEngine;

public class MillstoneCarrier : NetworkBehaviour
{
    [SerializeField] private Transform carryAnchor;

    public NetworkVariable<bool> IsCarrying = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private MillstoneHead _carriedHead;

    public Transform CarryAnchor => carryAnchor != null ? carryAnchor : transform;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            IsCarrying.Value = false;
    }

    public void AttachHeadServer(MillstoneHead head)
    {
        if (!IsServer) return;
        _carriedHead = head;
        IsCarrying.Value = head != null;
    }

    public void DetachHeadServer()
    {
        if (!IsServer) return;
        _carriedHead = null;
        IsCarrying.Value = false;
    }

    [ServerRpc]
    public void ThrowCarriedHeadServerRpc(Vector3 direction)
    {
        if (!IsServer) return;
        if (_carriedHead == null) return;
        _carriedHead.ThrowServer(direction);
    }

    [ServerRpc]
    public void DropCarriedHeadServerRpc()
    {
        if (!IsServer) return;
        DropCarriedHeadServer();
    }

    public void DropCarriedHeadServer()
    {
        if (!IsServer) return;
        if (_carriedHead == null) return;
        _carriedHead.DropServer();
    }

    public bool IsCarryingHead(MillstoneHead head)
    {
        return _carriedHead == head;
    }
}
