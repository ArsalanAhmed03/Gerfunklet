using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BurningPatch : NetworkBehaviour
{
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private float durationSeconds = 3f;
    [SerializeField] private float tickInterval = 1f;
    [SerializeField] private int damagePerTick = 10;
    [SerializeField] private LayerMask enemyMask;

    private NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float _endTime;
    private float _nextTickTime;

    public void InitServer(ulong ownerId, float customRadius)
    {
        if (!IsServer) return;
        ownerClientId.Value = ownerId;
        radius = customRadius;
        _endTime = Time.time + durationSeconds;
        _nextTickTime = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Time.time >= _endTime)
        {
            var no = GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned)
                no.Despawn(true);
            else
                Destroy(gameObject);
            return;
        }

        if (Time.time < _nextTickTime)
            return;

        _nextTickTime = Time.time + tickInterval;
        ApplyTick();
    }

    private void ApplyTick()
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && IsEnemy(stats.OwnerClientId))
            {
                stats.TakeDamageServerRpc(damagePerTick);
                continue;
            }

            var minionOwner = col.GetComponentInParent<MinionOwner>();
            var minionHealth = col.GetComponentInParent<MinionHealth>();
            if (minionOwner != null && minionHealth != null && IsEnemy(minionOwner.OwnerClientId))
            {
                minionHealth.TakeDamage(damagePerTick);
            }
        }
    }

    private bool IsEnemy(ulong targetOwner)
    {
        if (ownerClientId.Value == ulong.MaxValue) return true;
        return targetOwner != ownerClientId.Value;
    }
}
