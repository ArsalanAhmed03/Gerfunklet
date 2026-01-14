using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Rally")]
public class RallyAbilityDefinition : AbilityDefinition
{
    public float radius = 3.5f;
    public float durationSeconds = 10f;
    public float moveSpeedMultiplier = 1.1f;
    public float attackSpeedMultiplier = 1.15f;

    public override void ServerExecute(AbilityRunner runner)
    {
        int mask = ~0;
        var hits = Physics.OverlapSphere(runner.transform.position, radius, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            if (!IsOwnedByRunner(runner, col.transform))
                continue;

            var buff = col.GetComponentInParent<BuffReceiver>();
            if (buff != null)
            {
                float moveMult = moveSpeedMultiplier;
                float atkMult = attackSpeedMultiplier;

                if (HasCarriedObject(col.transform))
                {
                    moveMult = 1f + (moveMult - 1f) * 2f;
                    atkMult = 1f + (atkMult - 1f) * 2f;
                }

                buff.ApplyBuffServerRpc(moveMult, atkMult, durationSeconds);
            }
        }

        runner.PlayAbilityFxClientRpc(id);
    }

    public override void ClientExecute(AbilityRunner runner)
    {
        // put VFX/SFX later
    }

    private bool IsOwnedByRunner(AbilityRunner runner, Transform target)
    {
        var no = target.GetComponentInParent<Unity.Netcode.NetworkObject>();
        if (no != null && no.OwnerClientId == runner.OwnerClientId)
            return true;

        var minionOwner = target.GetComponentInParent<MinionOwner>();
        return minionOwner != null && minionOwner.OwnerClientId == runner.OwnerClientId;
    }

    private bool HasCarriedObject(Transform target)
    {
        var minionOwner = target.GetComponentInParent<MinionOwner>();
        if (minionOwner == null)
            return false;

        var food = target.GetComponentInParent<FoodCarrier>();
        if (food != null && food.HasFood)
            return true;

        var carrier = target.GetComponentInParent<MillstoneCarrier>();
        if (carrier != null && carrier.IsCarrying.Value)
            return true;

        return false;
    }
}
