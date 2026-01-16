using Unity.Netcode;
using UnityEngine;

public class FoodPile : NetworkBehaviour
{
    public enum FoodSize
    {
        Small,
        Medium,
        Big
    }

    [SerializeField] private FoodSize size = FoodSize.Small;
    [SerializeField] private int smallValue = 10;
    [SerializeField] private int mediumValue = 20;
    [SerializeField] private int bigValue = 35;

    public int GetValue()
    {
        return size switch
        {
            FoodSize.Medium => mediumValue,
            FoodSize.Big => bigValue,
            _ => smallValue
        };
    }

    public void ConsumeServer()
    {
        if (!IsServer) return;

        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            no.Despawn(true);
        else
            Destroy(gameObject);
    }
}
