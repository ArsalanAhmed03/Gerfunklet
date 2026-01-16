using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BuildableHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 120;

    public NetworkVariable<int> health = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int MaxHealth => maxHealth;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            health.Value = maxHealth;
    }

    public void ApplyDamageServer(int amount)
    {
        if (!IsServer) return;
        if (amount <= 0) return;

        int next = Mathf.Max(0, health.Value - amount);
        health.Value = next;

        if (next <= 0)
            DespawnServer();
    }

    private void DespawnServer()
    {
        if (!IsServer) return;

        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            no.Despawn(true);
        else
            Destroy(gameObject);
    }
}
