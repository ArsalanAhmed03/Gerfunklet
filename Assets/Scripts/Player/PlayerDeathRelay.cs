using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerStatsManager))]
public class PlayerDeathRelay : NetworkBehaviour
{
    private PlayerStatsManager _stats;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        _stats = GetComponent<PlayerStatsManager>();
        if (_stats != null)
            _stats.OnPlayerDied += HandleDied;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        if (_stats != null)
            _stats.OnPlayerDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (!IsServer) return;
        if (MatchManager.Instance != null)
            MatchManager.Instance.NotifyPlayerDied(OwnerClientId);
    }
}
