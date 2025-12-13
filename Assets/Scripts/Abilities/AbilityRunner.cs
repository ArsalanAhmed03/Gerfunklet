using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AbilityRunner : NetworkBehaviour
{
    [Header("4 slots in order: 1..4")]
    public AbilityDefinition[] slots = new AbilityDefinition[4];

    private readonly Dictionary<AbilityId, float> serverReadyAt = new Dictionary<AbilityId, float>();

    public void TryCastSlot(int slotIndex)
    {
        if (!IsOwner) return;
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        if (slots[slotIndex] == null) return;

        CastAbilityServerRpc(slotIndex);
    }

    [ServerRpc]
    private void CastAbilityServerRpc(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        var def = slots[slotIndex];
        if (def == null) return;

        if (serverReadyAt.TryGetValue(def.id, out float readyAt))
        {
            if (Time.time < readyAt)
                return;
        }

        serverReadyAt[def.id] = Time.time + def.cooldownSeconds;

        def.ServerExecute(this);
    }

    [ClientRpc]
    public void PlayAbilityFxClientRpc(AbilityId id)
    {
        // route to the definition for cosmetic behavior
        for (int i = 0; i < slots.Length; i++)
        {
            var def = slots[i];
            if (def != null && def.id == id)
            {
                def.ClientExecute(this);
                return;
            }
        }
    }
}
