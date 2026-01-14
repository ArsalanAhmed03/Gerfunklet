using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Stomp")]
public class StompAbilityDefinition : AbilityDefinition
{
    [Header("Gameplay")]
    public float radius = 2.6f;
    public int damage = 160;
    public float stunSeconds = 0.25f;
    public float superChargePerHit = 0.08f;
    public LayerMask targetMask;

    [Header("Parry interaction")]
    public float attackerStunOnParry = 0.4f;

    public override void ServerExecute(AbilityRunner runner)
    {
        var center = runner.transform.position;
        int mask = targetMask.value != 0 ? targetMask.value : ~0;
        var hits = Physics.OverlapSphere(center, radius, mask, QueryTriggerInteraction.Ignore);
        var super = runner.GetComponent<SuperCharge>();

        foreach (var col in hits)
        {
            // Ignore self root
            if (col.GetComponentInParent<AbilityRunner>() == runner)
                continue;

            var barricade = col.GetComponentInParent<Barricade>();
            if (barricade != null)
            {
                DespawnTarget(barricade.gameObject);
                continue;
            }

            var targetStats = col.GetComponentInParent<PlayerStatsManager>();
            if (targetStats != null)
            {
                var targetParry = col.GetComponentInParent<ParryReceiver>();
                if (targetParry != null && targetParry.IsParryActive)
                {
                    var attackerStun = runner.GetComponent<StunReceiver>();
                    if (attackerStun != null)
                        attackerStun.ApplyStunServerRpc(attackerStunOnParry);

                    continue;
                }

                targetStats.TakeDamageServerRpc(damage);

                if (super != null)
                    super.AddChargeFlatServer(superChargePerHit);

                var targetStun = col.GetComponentInParent<StunReceiver>();
                if (targetStun != null)
                    targetStun.ApplyStunServerRpc(stunSeconds);

                continue;
            }

            var minionHealth = col.GetComponentInParent<MinionHealth>();
            if (minionHealth != null)
            {
                minionHealth.TakeDamage(damage);

                if (super != null)
                    super.AddChargeFlatServer(superChargePerHit);
            }
        }

        runner.PlayAbilityFxClientRpc(id);
    }

    public override void ClientExecute(AbilityRunner runner)
    {
        runner.GetComponent<PlayerMovement>()?.playerAnimator?.Stomp();
    }

    private void DespawnTarget(GameObject target)
    {
        var no = target.GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            no.Despawn(true);
        else
            Object.Destroy(target);
    }
}
