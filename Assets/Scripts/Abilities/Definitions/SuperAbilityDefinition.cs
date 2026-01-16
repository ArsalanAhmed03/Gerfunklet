using System.Collections;
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Super Ability Definition")]
public class SuperAbilityDefinition : ScriptableObject
{
    public SuperChoice choice;

    [Header("Seismic Quake")]
    [SerializeField] private float seismicRadius = 6f;
    [SerializeField] private float seismicKnockbackDistance = 4f;
    [SerializeField] private float seismicKnockbackSeconds = 0.25f;
    [SerializeField] private LayerMask seismicTargetMask;

    [Header("Boulder Pitch")]
    [SerializeField] private BoulderPitchProjectile boulderProjectilePrefab;
    [SerializeField] private float boulderSpawnForward = 1.2f;
    [SerializeField] private float boulderSpawnUp = 1.0f;
    [SerializeField] private float boulderLaunchSpeed = 14f;
    [SerializeField] private float boulderLaunchUpVelocity = 6f;
    [SerializeField] private int boulderDamageToStructures = 600;
    [SerializeField] private int boulderDamageToPlayers = 0;
    [SerializeField] private LayerMask boulderHitMask;

    [Header("Gorge")]
    [SerializeField] private DevourAbilityDefinition gorgeDevour;
    [SerializeField] private int gorgePulseCount = 3;
    [SerializeField] private float gorgePulseInterval = 0.2f;
    [SerializeField] private float gorgeCcImmunitySeconds = 2f;

    // Server does real gameplay (damage, CC, spawns)
    public virtual void ServerExecute(AbilityRunner runner)
    {
        if (runner == null) return;

        switch (choice)
        {
            case SuperChoice.SeismicQuake:
                ServerExecuteSeismic(runner);
                break;
            case SuperChoice.BoulderPitch:
                ServerExecuteBoulder(runner);
                break;
            case SuperChoice.Gorge:
                ServerExecuteGorge(runner);
                break;
        }
    }

    // Clients do visuals only (anim, VFX, SFX)
    public virtual void ClientExecute(AbilityRunner runner)
    {
        if (runner == null) return;

        switch (choice)
        {
            case SuperChoice.SeismicQuake:
                runner.GetComponent<PlayerAnimator>()?.Stomp();
                break;
            case SuperChoice.BoulderPitch:
                break;
            case SuperChoice.Gorge:
                break;
        }
    }

    private void ServerExecuteSeismic(AbilityRunner runner)
    {
        int mask = seismicTargetMask.value != 0 ? seismicTargetMask.value : ~0;
        var center = runner.transform.position;
        var hits = Physics.OverlapSphere(center, seismicRadius, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            if (col.GetComponentInParent<AbilityRunner>() == runner)
                continue;

            var dir = (col.transform.position - center);
            if (dir.sqrMagnitude < 0.0001f) continue;
            dir.Normalize();

            var knock = col.GetComponentInParent<KnockbackReceiver>();
            if (knock != null)
            {
                knock.ApplyKnockbackServer(dir, seismicKnockbackDistance, seismicKnockbackSeconds);
                continue;
            }

            var minion = col.GetComponentInParent<MinionAI>();
            if (minion != null)
            {
                minion.transform.position += dir * seismicKnockbackDistance;
            }
        }
    }

    private void ServerExecuteBoulder(AbilityRunner runner)
    {
        if (boulderProjectilePrefab == null) return;

        Vector3 spawnPos = runner.transform.position + runner.transform.forward * boulderSpawnForward + Vector3.up * boulderSpawnUp;
        Quaternion rot = Quaternion.LookRotation(runner.transform.forward, Vector3.up);

        var proj = Object.Instantiate(boulderProjectilePrefab, spawnPos, rot);
        var no = proj.GetComponent<NetworkObject>();
        if (no != null)
            no.Spawn(true);

        int hitMask = boulderHitMask.value != 0 ? boulderHitMask.value : ~0;
        proj.InitServer(runner.transform.forward, runner.OwnerClientId, boulderLaunchSpeed, boulderLaunchUpVelocity, boulderDamageToStructures, boulderDamageToPlayers, hitMask);
    }

    private void ServerExecuteGorge(AbilityRunner runner)
    {
        var stun = runner.GetComponent<StunReceiver>();
        if (stun != null)
            stun.ApplyCcImmunityServer(gorgeCcImmunitySeconds);

        if (gorgeDevour == null) return;

        runner.StartCoroutine(GorgeRoutine(runner));
    }

    private IEnumerator GorgeRoutine(AbilityRunner runner)
    {
        int pulses = Mathf.Max(1, gorgePulseCount);
        for (int i = 0; i < pulses; i++)
        {
            gorgeDevour.ServerExecute(runner);
            if (i < pulses - 1 && gorgePulseInterval > 0f)
                yield return new WaitForSeconds(gorgePulseInterval);
        }
    }
}
