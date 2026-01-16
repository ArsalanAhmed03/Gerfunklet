using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ResourceDeposit : NetworkBehaviour
{
    [SerializeField] private bool autoAssignOwner = true;

    public NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (!autoAssignOwner) return;
        if (ownerClientId.Value != ulong.MaxValue) return;

        TryAutoAssignOwner();
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (ownerClientId.Value == ulong.MaxValue) return;

        var gatherer = other.GetComponentInParent<MinionGatherer>();
        if (gatherer == null || !gatherer.HasCargo) return;

        var owner = other.GetComponentInParent<MinionOwner>();
        if (owner == null) return;
        if (owner.OwnerClientId != ownerClientId.Value) return;

        float atp = gatherer.ConsumeCargo();
        if (atp <= 0f) return;

        var player = LocalSpawner.Instance != null ? LocalSpawner.Instance.GetPlayerForClient(owner.OwnerClientId) : null;
        if (player == null) return;

        var resource = player.GetComponent<AtpResource>();
        if (resource != null)
            resource.AddAtpServer(atp);
    }

    private void TryAutoAssignOwner()
    {
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.TryGetTeamClientIds(out var a, out var b)) return;

        ownerClientId.Value = transform.position.x <= 0f ? a : b;
    }
}
