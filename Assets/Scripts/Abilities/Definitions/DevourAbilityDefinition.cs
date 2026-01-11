using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Devour")]
public class DevourAbilityDefinition : AbilityDefinition
{
    [Header("Targeting")]
    public float radius = 2.5f;
    public float coneAngleDegrees = 70f;
    public int maxTargets = 3;
    public LayerMask targetMask;

    [Header("Healing")]
    public int healPerTarget = 0;
    public bool consumeFood = true;
    public int maxFoodConsumed = 1;
    public float foodValueToHealMultiplier = 1f;

    [Header("Bone Scrap (optional)")]
    public NetworkObject boneScrapPrefab;

    public override void ServerExecute(AbilityRunner runner)
    {
        var origin = runner.transform.position;
        var forward = runner.transform.forward;
        float cosHalf = Mathf.Cos(coneAngleDegrees * 0.5f * Mathf.Deg2Rad);

        int mask = targetMask.value != 0 ? targetMask.value : ~0;
        var hits = Physics.OverlapSphere(origin, radius, mask, QueryTriggerInteraction.Ignore);

        var minions = new List<MinionAI>();
        var food = new List<FoodPile>();

        foreach (var hit in hits)
        {
            var to = (hit.transform.position - origin);
            if (to.sqrMagnitude < 0.0001f) continue;
            if (Vector3.Dot(forward, to.normalized) < cosHalf) continue;

            var minion = hit.GetComponentInParent<MinionAI>();
            if (minion != null)
            {
                if (!minions.Contains(minion))
                    minions.Add(minion);
                continue;
            }

            if (consumeFood)
            {
                var pile = hit.GetComponentInParent<FoodPile>();
                if (pile != null && !food.Contains(pile))
                    food.Add(pile);
            }
        }

        int eaten = 0;
        for (int i = 0; i < minions.Count && eaten < maxTargets; i++)
        {
            var minion = minions[i];
            if (minion == null) continue;

            var stats = minion.GetComponent<MinionStats>();
            if (stats == null) continue;
            if (!stats.Devourable) continue;
            if (stats.SizeCategory == MinionStats.Size.Large) continue;

            DespawnTarget(minion.gameObject);
            eaten++;

            if (healPerTarget > 0)
                HealRunner(runner, healPerTarget);

            SpawnBoneScrap(minion.transform.position);
        }

        if (consumeFood && food.Count > 0 && maxFoodConsumed > 0)
        {
            int foodCount = Mathf.Min(maxFoodConsumed, food.Count);
            for (int i = 0; i < foodCount; i++)
            {
                var pile = food[i];
                if (pile == null) continue;

                int value = pile.GetValue();
                pile.ConsumeServer();

                int heal = Mathf.RoundToInt(value * foodValueToHealMultiplier);
                if (heal > 0)
                    HealRunner(runner, heal);
            }
        }

        runner.PlayAbilityFxClientRpc(id);
    }

    private void HealRunner(AbilityRunner runner, int amount)
    {
        var stats = runner.GetComponent<PlayerStatsManager>();
        if (stats != null)
            stats.Heal(amount);
    }

    private void DespawnTarget(GameObject target)
    {
        var no = target.GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            no.Despawn(true);
        else
            Object.Destroy(target);
    }

    private void SpawnBoneScrap(Vector3 position)
    {
        if (boneScrapPrefab == null) return;

        var scrap = Object.Instantiate(boneScrapPrefab, position, Quaternion.identity);
        scrap.Spawn(true);
    }
}
