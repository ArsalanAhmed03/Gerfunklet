using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BuildableFoodCache : NetworkBehaviour
{
    [SerializeField] private FoodPile smallPrefab;
    [SerializeField] private FoodPile mediumPrefab;
    [SerializeField] private FoodPile bigPrefab;
    [SerializeField] private float spawnIntervalSeconds = 6f;
    [SerializeField] private int maxPilesNear = 4;
    [SerializeField] private float spawnRadius = 0.6f;
    [SerializeField] private float dropOffsetY = 0.05f;
    [SerializeField] private float mediumChance = 0.25f;
    [SerializeField] private float bigChance = 0.1f;

    private float _nextSpawnTime;

    private void Update()
    {
        if (!IsServer) return;
        if (spawnIntervalSeconds <= 0f) return;
        if (Time.time < _nextSpawnTime) return;

        _nextSpawnTime = Time.time + spawnIntervalSeconds;
        TrySpawnFood();
    }

    private void TrySpawnFood()
    {
        if (CountNearbyPiles() >= maxPilesNear) return;

        var prefab = ChoosePrefab();
        if (prefab == null) return;

        Vector3 offset = new Vector3(Random.Range(-spawnRadius, spawnRadius), dropOffsetY, Random.Range(-spawnRadius, spawnRadius));
        var instance = Instantiate(prefab, transform.position + offset, Quaternion.identity);
        var no = instance.GetComponent<NetworkObject>();
        if (no != null)
            no.Spawn();
    }

    private int CountNearbyPiles()
    {
        var hits = Physics.OverlapSphere(transform.position, spawnRadius * 2f, ~0, QueryTriggerInteraction.Ignore);
        int count = 0;
        foreach (var hit in hits)
        {
            if (hit.GetComponentInParent<FoodPile>() != null)
                count++;
        }
        return count;
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
