using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BuildableAuraDamage : NetworkBehaviour
{
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private int damagePerTick = 10;
    [SerializeField] private float tickSeconds = 0.5f;
    [SerializeField] private LayerMask targetMask;
    [SerializeField] private bool affectPlayers = true;
    [SerializeField] private bool affectMinions = true;

    private float _nextTickTime;
    private BuildableInstance _owner;

    private void Awake()
    {
        _owner = GetComponent<BuildableInstance>();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (tickSeconds <= 0f || damagePerTick <= 0) return;

        if (Time.time < _nextTickTime)
            return;

        _nextTickTime = Time.time + tickSeconds;
        ApplyAuraDamage();
    }

    private void ApplyAuraDamage()
    {
        int mask = targetMask.value != 0 ? targetMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            if (affectPlayers)
            {
                var stats = col.GetComponentInParent<PlayerStatsManager>();
                if (stats != null && IsEnemy(stats.OwnerClientId))
                {
                    stats.TakeDamageServerRpc(damagePerTick);
                    continue;
                }
            }

            if (affectMinions)
            {
                var minionOwner = col.GetComponentInParent<MinionOwner>();
                var minionHealth = col.GetComponentInParent<MinionHealth>();
                if (minionHealth != null && minionOwner != null && IsEnemy(minionOwner.OwnerClientId))
                {
                    minionHealth.TakeDamage(damagePerTick);
                }
            }
        }
    }

    private bool IsEnemy(ulong targetOwner)
    {
        if (_owner == null) return true;
        return targetOwner != _owner.OwnerClientId;
    }
}
