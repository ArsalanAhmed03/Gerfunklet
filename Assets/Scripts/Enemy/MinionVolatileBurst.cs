using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MinionVolatileBurst : NetworkBehaviour
{
    [SerializeField] private int explosionDamage = 100;
    [SerializeField] private float explosionRadius = 2.5f;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private BurningPatch burningPatchPrefab;

    private MinionOwner _owner;

    private void Awake()
    {
        _owner = GetComponent<MinionOwner>();
    }

    public void HandleDeath()
    {
        if (!IsServer) return;
        Explode();
        SpawnBurningPatch();
    }

    private void Explode()
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, explosionRadius, mask, QueryTriggerInteraction.Ignore);
        var hitSet = new HashSet<Transform>();

        foreach (var col in hits)
        {
            if (col == null) continue;
            var root = col.GetComponentInParent<Transform>();
            if (root == null || hitSet.Contains(root)) continue;
            hitSet.Add(root);

            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && IsEnemy(stats.OwnerClientId))
            {
                stats.TakeDamageServerRpc(explosionDamage);
                continue;
            }

            var minionOwner = col.GetComponentInParent<MinionOwner>();
            var minionHealth = col.GetComponentInParent<MinionHealth>();
            if (minionOwner != null && minionHealth != null && IsEnemy(minionOwner.OwnerClientId))
            {
                minionHealth.TakeDamage(explosionDamage);
            }
        }
    }

    private void SpawnBurningPatch()
    {
        if (burningPatchPrefab == null) return;
        var patch = Instantiate(burningPatchPrefab, transform.position, Quaternion.identity);
        var no = patch.GetComponent<NetworkObject>();
        if (no != null)
            no.Spawn();

        patch.InitServer(_owner != null ? _owner.OwnerClientId : ulong.MaxValue, explosionRadius);
    }

    private bool IsEnemy(ulong targetOwner)
    {
        if (_owner == null) _owner = GetComponent<MinionOwner>();
        if (_owner == null) return true;
        return targetOwner != _owner.OwnerClientId;
    }
}
