using Unity.Netcode;
using UnityEngine;

public class SuperController : NetworkBehaviour
{
    [Header("Catalog")]
    [SerializeField] private SuperAbilityCatalog catalog;
    [SerializeField] private SuperChoice defaultChoice = SuperChoice.SeismicQuake;

    [Header("Debug")]
    [SerializeField] private bool debugSuper = true;

    public NetworkVariable<SuperChoice> Choice = new NetworkVariable<SuperChoice>(
        SuperChoice.SeismicQuake,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private AbilityRunner _runner;

    private void Awake()
    {
        if (catalog != null) catalog.Build();
        _runner = GetComponent<AbilityRunner>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            Choice.Value = defaultChoice;
    }

    public void TryCastSuper()
    {
        if (!IsOwner) return;
        if (GameManager.Instance != null && !GameManager.Instance.GameplayEnabled) return;

        var stun = GetComponent<StunReceiver>();
        if (stun != null && stun.IsStunned) return;

        var disable = GetComponent<CombatDisableReceiver>();
        if (disable != null && disable.IsDisabled) return;

        var stats = GetComponent<PlayerStatsManager>();
        if (stats != null)
        {
            if (!stats.IsAlive) return;
            if (stats.IsSleeping) return;
        }

        CastSuperServerRpc();
    }

    [ServerRpc]
    private void CastSuperServerRpc()
    {
        if (!IsServer) return;

        if (MatchManager.Instance == null) return;
        var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
        bool live = phase == MatchManager.MatchPhase.Playing || phase == MatchManager.MatchPhase.Overtime;
        if (!live) return;

        var stun = GetComponent<StunReceiver>();
        if (stun != null && stun.IsStunned) return;

        var disable = GetComponent<CombatDisableReceiver>();
        if (disable != null && disable.IsDisabled) return;

        var stats = GetComponent<PlayerStatsManager>();
        if (stats != null)
        {
            if (!stats.IsAlive) return;
            if (stats.IsSleeping) return;
        }

        var charge = GetComponent<SuperCharge>();
        if (charge == null) return;
        if (!charge.TryConsumeFullServer()) return;

        var def = GetDefinition(Choice.Value);
        if (def == null) return;

        if (debugSuper)
            Debug.Log($"[Super][SERVER] owner={OwnerClientId} choice={Choice.Value}");

        var runner = _runner != null ? _runner : GetComponent<AbilityRunner>();
        def.ServerExecute(runner);
        PlaySuperFxClientRpc(Choice.Value);
    }

    private SuperAbilityDefinition GetDefinition(SuperChoice choice)
    {
        if (catalog == null) return null;
        return catalog.GetDefinition(choice);
    }

    [ClientRpc]
    private void PlaySuperFxClientRpc(SuperChoice choice)
    {
        var def = GetDefinition(choice);
        if (def == null) return;

        if (debugSuper && NetworkManager.Singleton != null)
            Debug.Log($"[Super][CLIENT FX] local={NetworkManager.Singleton.LocalClientId} owner={OwnerClientId} choice={choice}");

        var runner = _runner != null ? _runner : GetComponent<AbilityRunner>();
        def.ClientExecute(runner);
    }

    [ServerRpc]
    public void SetChoiceServerRpc(SuperChoice choice)
    {
        if (!IsServer) return;
        Choice.Value = choice;
    }
}
