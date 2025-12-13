using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Parry")]
public class ParryAbilityDefinition : AbilityDefinition
{
    public float windowSeconds = 0.22f;

    public override void ServerExecute(AbilityRunner runner)
    {
        var parry = runner.GetComponent<ParryReceiver>();
        if (parry != null)
            parry.ActivateParryServerRpc(windowSeconds);

        runner.PlayAbilityFxClientRpc(id);
    }

    public override void ClientExecute(AbilityRunner runner)
    {
        // put VFX/SFX later
    }
}
