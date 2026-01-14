using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ResourceNode : NetworkBehaviour
{
    [SerializeField] private int maxEnergy = 3;
    [SerializeField] private float atpPerHarvest = 1f;
    [SerializeField] private float respawnSeconds = 8f;
    [SerializeField] private bool allowTheft = true;
    [SerializeField] private bool autoAssignOwner = false;

    public NetworkVariable<int> energy = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private double _respawnAtTime;

    public float AtpPerHarvest => atpPerHarvest;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (energy.Value <= 0)
            energy.Value = maxEnergy;

        if (autoAssignOwner && ownerClientId.Value == ulong.MaxValue)
            TryAutoAssignOwner();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (energy.Value > 0) return;
        if (_respawnAtTime <= 0d) return;

        double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
        if (now >= _respawnAtTime)
        {
            energy.Value = maxEnergy;
            _respawnAtTime = 0d;
        }
    }

    public bool TryHarvestServer(ulong harvesterOwner, bool isScout, out float atpValue)
    {
        atpValue = 0f;
        if (!IsServer) return false;
        if (energy.Value <= 0) return false;

        bool owned = ownerClientId.Value == ulong.MaxValue || ownerClientId.Value == harvesterOwner;
        if (!owned && (!allowTheft || !isScout))
            return false;

        energy.Value = Mathf.Max(0, energy.Value - 1);
        atpValue = atpPerHarvest;

        if (energy.Value <= 0 && respawnSeconds > 0f)
        {
            double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
            _respawnAtTime = now + respawnSeconds;
        }

        return true;
    }

    private void TryAutoAssignOwner()
    {
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.TryGetTeamClientIds(out var a, out var b)) return;

        ownerClientId.Value = transform.position.x <= 0f ? a : b;
    }
}
