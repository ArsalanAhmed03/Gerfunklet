using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Fortify")]
public class FortifyAbilityDefinition : AbilityDefinition
{
    public float durationSeconds = 2.5f;
    public float damageMultiplier = 0.6f; // 60% damage taken

    public override void ServerExecute(AbilityRunner runner)
    {
        var dr = runner.GetComponent<DamageReceiver>();
        if (dr != null)
            dr.ApplyDamageReductionServerRpc(damageMultiplier, durationSeconds);

        runner.PlayAbilityFxClientRpc(id);
    }

    public override void ClientExecute(AbilityRunner runner)
    {
        // later: shader flash / SFX
    }
}
