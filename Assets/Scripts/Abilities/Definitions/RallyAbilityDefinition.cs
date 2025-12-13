using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Rally")]
public class RallyAbilityDefinition : AbilityDefinition
{
    public float radius = 3.5f;
    public float durationSeconds = 10f;
    public float moveSpeedMultiplier = 1.15f;

    public override void ServerExecute(AbilityRunner runner)
    {
        int mask = LayerMask.GetMask("Player");
        var hits = Physics.OverlapSphere(runner.transform.position, radius, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            var buff = col.GetComponentInParent<BuffReceiver>();
            if (buff != null)
                buff.ApplyMoveSpeedBuffServerRpc(moveSpeedMultiplier, durationSeconds);
        }

        runner.PlayAbilityFxClientRpc(id);
    }

    public override void ClientExecute(AbilityRunner runner)
    {
        // put VFX/SFX later
    }
}
