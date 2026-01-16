using Unity.Netcode;
using UnityEngine;

public class FoodDropper : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private FoodPile smallPrefab;
    [SerializeField] private FoodPile mediumPrefab;
    [SerializeField] private FoodPile bigPrefab;

    [Header("Drop Rules")]
    [SerializeField] private float dropChance = 0.5f;
    [SerializeField] private int minPiles = 1;
    [SerializeField] private int maxPiles = 1;
    [SerializeField] private float mediumChance = 0.25f;
    [SerializeField] private float bigChance = 0.1f;
    [SerializeField] private float scatterRadius = 0.4f;
    [SerializeField] private float dropOffsetY = 0.05f;

    public void DropServer()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            return;

        if (Random.value > dropChance)
            return;

        int count = Mathf.Clamp(Random.Range(minPiles, maxPiles + 1), 0, 10);
        for (int i = 0; i < count; i++)
        {
            var prefab = ChoosePrefab();
            if (prefab == null) continue;

            Vector3 offset = new Vector3(Random.Range(-scatterRadius, scatterRadius), dropOffsetY, Random.Range(-scatterRadius, scatterRadius));
            var instance = Instantiate(prefab, transform.position + offset, Quaternion.identity);
            var no = instance.GetComponent<NetworkObject>();
            if (no != null)
                no.Spawn();
        }
    }

    private FoodPile ChoosePrefab()
    {
        float roll = Random.value;
        if (bigPrefab != null && roll < bigChance)
            return bigPrefab;

        if (mediumPrefab != null && roll < bigChance + mediumChance)
            return mediumPrefab;

        return smallPrefab != null ? smallPrefab : mediumPrefab != null ? mediumPrefab : bigPrefab;
    }
}
