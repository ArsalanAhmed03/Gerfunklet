using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FeastRing : NetworkBehaviour
{
    [SerializeField] private bool autoAssignOwner = true;
    [SerializeField] private int maxStoredPiles = 20;

    public NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkList<int> storedFood = new NetworkList<int>(
        new List<int>(),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (!autoAssignOwner) return;
        if (ownerClientId.Value != ulong.MaxValue) return;

        ownerClientId.Value = NetworkObject.OwnerClientId;
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (ownerClientId.Value == ulong.MaxValue) return;

        var carrier = other.GetComponentInParent<FoodCarrier>();
        if (carrier == null || !carrier.HasFood) return;

        var minionOwner = other.GetComponentInParent<MinionOwner>();
        if (minionOwner == null) return;
        if (minionOwner.OwnerClientId != ownerClientId.Value) return;

        if (storedFood.Count >= maxStoredPiles) return;

        int value = carrier.DropAll();
        if (value <= 0) return;

        storedFood.Add(value);
    }

    public int ConsumeForWakeServer(int maxPiles, out int totalValue)
    {
        totalValue = 0;
        if (!IsServer) return 0;
        if (maxPiles <= 0) return 0;

        int count = Mathf.Min(maxPiles, storedFood.Count);
        for (int i = 0; i < count; i++)
        {
            totalValue += storedFood[0];
            storedFood.RemoveAt(0);
        }

        return count;
    }
}
