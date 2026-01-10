using Unity.Netcode;
using UnityEngine;

public class MillstonePedestal : NetworkBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private MillstoneHead millstonePrefab;

    [Header("Auto assign owner")]
    [SerializeField] private bool autoAssignOwner = true;

    public NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private MillstoneHead _spawnedHead;
    private bool _ownerAssigned;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        TryAutoAssignOwner();
        SpawnHeadIfNeeded();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!_ownerAssigned) TryAutoAssignOwner();
    }

    private void TryAutoAssignOwner()
    {
        if (!autoAssignOwner) return;
        if (_ownerAssigned) return;
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.TryGetTeamClientIds(out var a, out var b)) return;

        ownerClientId.Value = transform.position.x <= 0f ? a : b;
        _ownerAssigned = true;

        if (_spawnedHead != null)
            _spawnedHead.OwnerClientId.Value = ownerClientId.Value;
    }

    private void SpawnHeadIfNeeded()
    {
        if (!IsServer) return;
        if (millstonePrefab == null) return;
        if (_spawnedHead != null) return;

        var head = Instantiate(millstonePrefab, transform.position, transform.rotation);
        var no = head.GetComponent<NetworkObject>();
        if (no != null)
            no.Spawn(true);

        head.OwnerClientId.Value = ownerClientId.Value;
        _spawnedHead = head;
    }
}
