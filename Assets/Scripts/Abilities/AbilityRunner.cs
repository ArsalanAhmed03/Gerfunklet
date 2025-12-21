using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class AbilityRunner : NetworkBehaviour
{
    [Header("4 slots in order: 1..4")]
    public AbilityDefinition[] slots = new AbilityDefinition[4];

    private readonly Dictionary<AbilityId, float> serverReadyAt = new Dictionary<AbilityId, float>();
    [Header("Debug")]
    [SerializeField] private bool debugAbilities = true;

    private void DebugAbility(string msg)
    {
        if (!debugAbilities) return;
        Debug.Log(msg);
    }

    public void TryCastSlot(int slotIndex)
    {
        if (!IsOwner) return;

        // Gate locally to avoid spam (server will also validate)
        if (GameManager.Instance != null && !GameManager.Instance.GameplayEnabled) return;

        var stun = GetComponent<StunReceiver>();
        if (stun != null && stun.IsStunned) return;

        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        if (slots[slotIndex] == null) return;

        CastAbilityServerRpc(slotIndex);
    }

    [ServerRpc]
    private void CastAbilityServerRpc(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        // Server-side phase gate (authoritative)
        if (MatchManager.Instance == null) return;

        var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
        bool live = phase == MatchManager.MatchPhase.Playing || phase == MatchManager.MatchPhase.Overtime;
        if (!live) return;

        // Server-side stun gate (authoritative)
        var stun = GetComponent<StunReceiver>();
        if (stun != null && stun.IsStunned) return;

        var def = slots[slotIndex];
        if (def == null) return;

        if (serverReadyAt.TryGetValue(def.id, out float readyAt))
        {
            if (Time.time < readyAt)
                return;
        }

        serverReadyAt[def.id] = Time.time + def.cooldownSeconds;

        DebugAbility(
        $"[Ability][SERVER ACCEPT] client={OwnerClientId} " +
        $"ability={def.id} slot={slotIndex + 1} cd={def.cooldownSeconds:0.00}s " +
        $"round={(MatchManager.Instance != null ? MatchManager.Instance.CurrentRound.Value : -1)} " +
        $"phase={(MatchManager.Instance != null ? ((MatchManager.MatchPhase)MatchManager.Instance.Phase.Value).ToString() : "N/A")}"
        );
        def.ServerExecute(this);
    }

    [ClientRpc]
    public void PlayAbilityFxClientRpc(AbilityId id)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var def = slots[i];
            if (def != null && def.id == id)
            {
                if (debugAbilities)
                {
                    Debug.Log($"[Ability][CLIENT FX] localClient={NetworkManager.Singleton.LocalClientId} " +
                              $"owner={OwnerClientId} ability={id}");
                }
                def.ClientExecute(this);
                return;
            }
        }
    }

    // New: reset cooldowns between rounds
    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        serverReadyAt.Clear();
    }
}
