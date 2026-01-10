using Unity.Netcode;
using UnityEngine;

public class CitadelHealth : NetworkBehaviour
{
    [Header("Citadel")]
    [SerializeField] private int maxHealth = 2000;
    [SerializeField] private int contactDamagePerSecond = 40;

    public NetworkVariable<int> health = new NetworkVariable<int>(
        2000,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> destroyed = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Owner")]
    public NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool _ownerAssigned;

    public int MaxHealth => maxHealth;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        health.Value = maxHealth;
        destroyed.Value = false;
        TryAutoAssignOwner();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!_ownerAssigned) TryAutoAssignOwner();
    }

    public void ApplyDamageServer(int damage)
    {
        if (!IsServer) return;
        if (destroyed.Value) return;
        if (damage <= 0) return;

        health.Value = Mathf.Max(0, health.Value - damage);
        if (health.Value <= 0)
            destroyed.Value = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;
        if (destroyed.Value) return;
        if (contactDamagePerSecond <= 0) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null) return;

        if (no.OwnerClientId == ownerClientId.Value) return;

        ApplyDamageServer(Mathf.CeilToInt(contactDamagePerSecond * Time.deltaTime));
    }

    private void TryAutoAssignOwner()
    {
        if (_ownerAssigned) return;
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.TryGetTeamClientIds(out var a, out var b)) return;

        ownerClientId.Value = transform.position.x <= 0f ? a : b;
        _ownerAssigned = true;
    }
}
