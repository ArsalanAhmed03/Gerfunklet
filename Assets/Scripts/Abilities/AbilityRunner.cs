using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class AbilityRunner : NetworkBehaviour
{
    [Header("Definition DB (client + server)")]
    [SerializeField] private AbilityCatalog defDb;

    // Replicated slot ids (server writes, everyone reads)
    public NetworkVariable<AbilityId> Slot0 = new NetworkVariable<AbilityId>(
        AbilityId.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<AbilityId> Slot1 = new NetworkVariable<AbilityId>(
        AbilityId.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<AbilityId> Slot2 = new NetworkVariable<AbilityId>(
        AbilityId.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<AbilityId> Slot3 = new NetworkVariable<AbilityId>(
        AbilityId.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<AbilityId> Slot4 = new NetworkVariable<AbilityId>(
        AbilityId.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Local derived cache (not networked)
    [Header("Local derived defs (debug)")]
    public AbilityDefinition[] slots = new AbilityDefinition[5];

    private readonly Dictionary<AbilityId, float> serverReadyAt = new Dictionary<AbilityId, float>();

    [Header("Debug")]
    [SerializeField] private bool debugAbilities = true;

    private void Awake()
    {
        if (defDb != null) defDb.Build();
    }

    public override void OnNetworkSpawn()
    {
        Slot0.OnValueChanged += (_, __) => RebuildLocalSlots();
        Slot1.OnValueChanged += (_, __) => RebuildLocalSlots();
        Slot2.OnValueChanged += (_, __) => RebuildLocalSlots();
        Slot3.OnValueChanged += (_, __) => RebuildLocalSlots();
        Slot4.OnValueChanged += (_, __) => RebuildLocalSlots();
        Debug.Log($"[AbilityRunner][SPAWN] local={NetworkManager.Singleton.LocalClientId} owner={OwnerClientId} isServer={IsServer} defDbNull={(defDb==null)} slots={Slot0.Value},{Slot1.Value},{Slot2.Value},{Slot3.Value},{Slot4.Value}");
        RebuildLocalSlots();
    }

    private void RebuildLocalSlots()
    {
        slots[0] = Resolve(Slot0.Value);
        slots[1] = Resolve(Slot1.Value);
        slots[2] = Resolve(Slot2.Value);
        slots[3] = Resolve(Slot3.Value);
        slots[4] = Resolve(Slot4.Value);
    }

    private AbilityDefinition Resolve(AbilityId id)
    {
        if (id == AbilityId.None) return null;
        if (defDb == null) return null;
        return defDb.GetDefinition(id);
    }

    public AbilityId GetSlotId(int slotIndex)
    {
        return slotIndex switch
        {
            0 => Slot0.Value,
            1 => Slot1.Value,
            2 => Slot2.Value,
            3 => Slot3.Value,
            4 => Slot4.Value,
            _ => AbilityId.None
        };
    }

    // Called by MatchManager on server when loadout submitted
    public void ApplyLoadoutServer(AbilityId[] chosen)
    {
        if (!IsServer) return;
        if (chosen == null || chosen.Length != 5) return;

        Slot0.Value = chosen[0];
        Slot1.Value = chosen[1];
        Slot2.Value = chosen[2];
        Slot3.Value = chosen[3];
        Slot4.Value = chosen[4];

        serverReadyAt.Clear();
    }

    public void TryCastSlot(int slotIndex)
    {
        if (!IsOwner) return;

        if (GameManager.Instance != null && !GameManager.Instance.GameplayEnabled) return;

        var stun = GetComponent<StunReceiver>();
        if (stun != null && stun.IsStunned) return;

        var stats = GetComponent<PlayerStatsManager>();
        if (stats != null && stats.IsSleeping) return;

        if (slotIndex < 0 || slotIndex > 4) return;

        // IMPORTANT: gate on replicated ids, not on ScriptableObject refs
        var id = GetSlotId(slotIndex);
        if (id == AbilityId.None) return;

        CastAbilityServerRpc(slotIndex);
    }

    [ServerRpc]
    private void CastAbilityServerRpc(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 4) return;

        if (MatchManager.Instance == null) return;
        var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
        bool live = phase == MatchManager.MatchPhase.Playing || phase == MatchManager.MatchPhase.Overtime;
        if (!live) return;

        var stun = GetComponent<StunReceiver>();
        if (stun != null && stun.IsStunned) return;

        var stats = GetComponent<PlayerStatsManager>();
        if (stats != null && stats.IsSleeping) return;

        var id = GetSlotId(slotIndex);
        if (id == AbilityId.None) return;

        var def = Resolve(id);
        if (def == null) return;

        if (serverReadyAt.TryGetValue(id, out float readyAt) && Time.time < readyAt) return;
        serverReadyAt[id] = Time.time + def.cooldownSeconds;

        if (debugAbilities)
        {
            Debug.Log($"[Ability][SERVER ACCEPT] owner={OwnerClientId} ability={id} slot={slotIndex + 1}");
        }

        def.ServerExecute(this);
    }

    [ClientRpc]
    public void PlayAbilityFxClientRpc(AbilityId id)
    {
        var def = Resolve(id);
        if (def == null) return;

        if (debugAbilities)
        {
            Debug.Log($"[Ability][CLIENT FX] local={NetworkManager.Singleton.LocalClientId} owner={OwnerClientId} ability={id}");
        }

        def.ClientExecute(this);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        serverReadyAt.Clear();
    }
}
