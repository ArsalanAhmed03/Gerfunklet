using Unity.Netcode;
using UnityEngine;

public class BuildableInstance : NetworkBehaviour
{
    [SerializeField] private CardId cardId = CardId.None;

    public ulong OwnerClientId { get; private set; } = ulong.MaxValue;
    public CardId CardId => cardId;

    public void InitializeServer(ulong ownerClientId, CardId id)
    {
        if (!IsServer) return;

        OwnerClientId = ownerClientId;
        cardId = id;

        var beacon = GetComponentInChildren<ForwardBeacon>(true);
        if (beacon != null)
            beacon.SetOwnerServer(ownerClientId);
    }
}
