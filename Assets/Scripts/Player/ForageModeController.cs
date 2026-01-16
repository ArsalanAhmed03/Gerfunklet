using Unity.Netcode;
using UnityEngine;

public class ForageModeController : NetworkBehaviour
{
    public enum ForageMode
    {
        ProtectOnly = 0,
        Balanced = 1,
        MaxForage = 2
    }

    [SerializeField] private ForageMode defaultMode = ForageMode.Balanced;
    [SerializeField] private int balancedForagers = 3;
    [SerializeField] private int maxForagers = 5;
    [SerializeField] private float refreshSeconds = 0.5f;

    public NetworkVariable<int> Mode = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private PlayerStatsManager _stats;
    private FeastRing _ring;
    private float _nextRefreshTime;

    public ForageMode CurrentMode => (ForageMode)Mode.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            Mode.Value = (int)defaultMode;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (_stats == null)
            _stats = GetComponent<PlayerStatsManager>();
        if (_ring == null)
            _ring = GetComponent<FeastRing>();

        if (_stats == null)
            return;

        if (Time.time < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.time + refreshSeconds;

        if (!_stats.IsSleeping)
        {
            SetForageOnAll(false);
            UpdateGuards(false);
            return;
        }

        int targetCount = GetForagerTargetCount();
        AssignForagers(targetCount);
        UpdateGuards(true);
    }

    [ServerRpc]
    public void SetModeServerRpc(ForageMode mode, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        Mode.Value = (int)mode;
    }

    private int GetForagerTargetCount()
    {
        switch (CurrentMode)
        {
            case ForageMode.ProtectOnly:
                return 0;
            case ForageMode.MaxForage:
                return Mathf.Max(0, maxForagers);
            default:
                return Mathf.Max(0, balancedForagers);
        }
    }

    private void AssignForagers(int targetCount)
    {
        var agents = FindObjectsOfType<MinionForageAgent>(true);
        int enabled = 0;

        foreach (var agent in agents)
        {
            if (agent == null) continue;
            if (!IsOwnedMinion(agent)) continue;
            if (IsSiege(agent)) continue;

            bool enable = enabled < targetCount;
            agent.SetForageEnabled(enable, _ring);

            if (enable)
                enabled++;
        }
    }

    private void SetForageOnAll(bool enabled)
    {
        var agents = FindObjectsOfType<MinionForageAgent>(true);
        foreach (var agent in agents)
        {
            if (agent == null) continue;
            if (!IsOwnedMinion(agent)) continue;
            if (IsSiege(agent)) continue;
            agent.SetForageEnabled(enabled, _ring);
        }
    }

    private void UpdateGuards(bool sleeping)
    {
        var minions = FindObjectsOfType<MinionAI>(true);
        foreach (var minion in minions)
        {
            if (minion == null) continue;
            if (!IsOwnedMinion(minion)) continue;

            var forage = minion.GetComponent<MinionForageAgent>();
            bool isForaging = forage != null && forage.IsForaging;

            if (!sleeping || isForaging)
                minion.ClearGuard();
            else
                minion.SetGuardAnchor(transform);
        }
    }

    private bool IsOwnedMinion(MinionForageAgent agent)
    {
        var owner = agent.GetComponent<MinionOwner>();
        return owner != null && owner.OwnerClientId == OwnerClientId;
    }

    private bool IsOwnedMinion(MinionAI minion)
    {
        var owner = minion.GetComponent<MinionOwner>();
        return owner != null && owner.OwnerClientId == OwnerClientId;
    }

    private bool IsSiege(MinionForageAgent agent)
    {
        var stats = agent.GetComponent<MinionStats>();
        return stats != null && stats.TargetingMode == MinionStats.Targeting.StructuresFirst;
    }
}
