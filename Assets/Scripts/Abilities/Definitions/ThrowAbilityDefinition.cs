using UnityEngine;
using Unity.Netcode;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Throw")]
public class ThrowAbilityDefinition : AbilityDefinition
{
    [Header("Pickup")]
    public float pickupRadius = 2f;
    public LayerMask pickupMask;
    public bool allowMinionThrow = true;
    public MinionStats.Size maxMinionSize = MinionStats.Size.Medium;
    public bool allowBarricadeThrow = true;

    [Header("Throw Arc")]
    public float throwSpeed = 12f;
    public float throwUpVelocity = 4f;
    public float throwGravity = 20f;
    public float throwLifeSeconds = 1.5f;
    public float hitRadius = 0.35f;
    public LayerMask hitMask;

    [Header("Impact")]
    public int damage = 25;
    public float knockbackDistance = 2.5f;
    public float knockbackSeconds = 0.2f;

    public override void ServerExecute(AbilityRunner runner)
    {
        var target = FindThrowable(runner);
        if (target == null)
            return;

        var no = target.GetComponent<NetworkObject>();
        if (no == null || !no.IsSpawned)
            return;

        var thrown = target.GetComponent<ThrownObject>();
        if (thrown == null)
            thrown = target.gameObject.AddComponent<ThrownObject>();

        int mask = hitMask.value != 0 ? hitMask.value : ~0;
        thrown.BeginThrowServer(
            runner.OwnerClientId,
            runner.transform.forward,
            throwSpeed,
            throwUpVelocity,
            throwGravity,
            damage,
            knockbackDistance,
            knockbackSeconds,
            throwLifeSeconds,
            hitRadius,
            mask);

        runner.PlayAbilityFxClientRpc(id);
    }

    public override void ClientExecute(AbilityRunner runner)
    {
        // put throw anim later
    }

    private Transform FindThrowable(AbilityRunner runner)
    {
        if (runner == null) return null;

        int mask = pickupMask.value != 0 ? pickupMask.value : ~0;
        var hits = Physics.OverlapSphere(runner.transform.position, pickupRadius, mask, QueryTriggerInteraction.Ignore);

        float bestDist = float.MaxValue;
        Transform best = null;

        foreach (var col in hits)
        {
            if (col.GetComponentInParent<AbilityRunner>() != null)
                continue;

            var head = col.GetComponentInParent<MillstoneHead>();
            if (head != null)
                continue;

            var minion = col.GetComponentInParent<MinionAI>();
            if (allowMinionThrow && minion != null)
            {
                var stats = minion.GetComponent<MinionStats>();
                if (stats != null && stats.SizeCategory <= maxMinionSize)
                {
                    float d = Vector3.SqrMagnitude(minion.transform.position - runner.transform.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = minion.transform;
                    }
                }
                continue;
            }

            if (allowBarricadeThrow)
            {
                var barricade = col.GetComponentInParent<Barricade>();
                if (barricade != null)
                {
                    float d = Vector3.SqrMagnitude(barricade.transform.position - runner.transform.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = barricade.transform;
                    }
                }
            }
        }

        return best;
    }
}
