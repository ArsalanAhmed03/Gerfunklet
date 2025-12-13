using UnityEngine;
using Unity.Netcode;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Throw")]
public class ThrowAbilityDefinition : AbilityDefinition
{
    public NetworkProjectile projectilePrefab;
    public float spawnForward = 1.2f;
    public float spawnUp = 0.8f;

    public override void ServerExecute(AbilityRunner runner)
    {
        if (projectilePrefab == null) return;

        Vector3 spawnPos = runner.transform.position + runner.transform.forward * spawnForward + Vector3.up * spawnUp;
        Quaternion rot = Quaternion.LookRotation(runner.transform.forward, Vector3.up);

        var proj = Object.Instantiate(projectilePrefab, spawnPos, rot);
        proj.GetComponent<NetworkObject>().Spawn(true);
        proj.InitServer(runner.transform.forward, runner.OwnerClientId);

        runner.PlayAbilityFxClientRpc(id);
    }

    public override void ClientExecute(AbilityRunner runner)
    {
        // put throw anim later
    }
}
