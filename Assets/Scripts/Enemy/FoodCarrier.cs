using Unity.Netcode;
using UnityEngine;

public class FoodCarrier : NetworkBehaviour
{
    [SerializeField] private int carriedValue;

    public bool HasFood => carriedValue > 0;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (carriedValue > 0) return;

        var pile = other.GetComponentInParent<FoodPile>();
        if (pile == null) return;

        carriedValue = pile.GetValue();
        pile.ConsumeServer();
    }

    public int DropAll()
    {
        int value = carriedValue;
        carriedValue = 0;
        return value;
    }
}
