using Unity.Netcode;
using UnityEngine;

public class MinionShadowBind : NetworkBehaviour
{
    [SerializeField] private float range = 4.5f;
    [SerializeField] private int damage = 30;
    [SerializeField] private int structureDamage = 60;
    [SerializeField] private float rootSeconds = 2f;
    [SerializeField] private float cooldownSeconds = 10f;
    [SerializeField] private float structureAttackSpeedDebuff = 0.5f;
    [SerializeField] private float structureDebuffSeconds = 2f;
    [SerializeField] private LayerMask targetMask;

    private MinionOwner _owner;
    private float _readyTime;

    private void Awake()
    {
        _owner = GetComponent<MinionOwner>();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (Time.time < _readyTime) return;

        var target = FindTarget();
        if (target == null) return;

        ApplyShadowBind(target);
        _readyTime = Time.time + cooldownSeconds;
    }

    private Transform FindTarget()
    {
        int mask = targetMask.value != 0 ? targetMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, range, mask, QueryTriggerInteraction.Ignore);
        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (var col in hits)
        {
            if (col == null) continue;

            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && IsEnemy(stats.OwnerClientId))
            {
                float sqr = (stats.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = stats.transform;
                }
                continue;
            }

            var minionOwner = col.GetComponentInParent<MinionOwner>();
            var minionHealth = col.GetComponentInParent<MinionHealth>();
            if (minionOwner != null && minionHealth != null && IsEnemy(minionOwner.OwnerClientId))
            {
                float sqr = (minionHealth.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = minionHealth.transform;
                }
                continue;
            }

            var buildable = col.GetComponentInParent<BuildableHealth>();
            var buildableOwner = col.GetComponentInParent<BuildableInstance>();
            if (buildable != null && buildableOwner != null && IsEnemy(buildableOwner.OwnerClientId))
            {
                float sqr = (buildable.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = buildable.transform;
                }
            }
        }

        return best;
    }

    private void ApplyShadowBind(Transform target)
    {
        var stats = target.GetComponentInParent<PlayerStatsManager>();
        if (stats != null && IsEnemy(stats.OwnerClientId))
        {
            stats.TakeDamageServerRpc(damage);
            var root = stats.GetComponent<RootReceiver>();
            if (root != null)
                root.ApplyRootServerRpc(rootSeconds);
            return;
        }

        var minionOwner = target.GetComponentInParent<MinionOwner>();
        var minionHealth = target.GetComponentInParent<MinionHealth>();
        if (minionOwner != null && minionHealth != null && IsEnemy(minionOwner.OwnerClientId))
        {
            minionHealth.TakeDamage(damage);
            var root = minionHealth.GetComponent<RootReceiver>();
            if (root != null)
                root.ApplyRootServerRpc(rootSeconds);
            return;
        }

        var buildable = target.GetComponentInParent<BuildableHealth>();
        var buildableOwner = target.GetComponentInParent<BuildableInstance>();
        if (buildable != null && buildableOwner != null && IsEnemy(buildableOwner.OwnerClientId))
        {
            buildable.ApplyDamageServer(structureDamage);
            var mod = buildable.GetComponent<AttackSpeedModifierReceiver>();
            if (mod != null)
                mod.ApplyAttackSpeedDebuffServerRpc(structureAttackSpeedDebuff, structureDebuffSeconds);
        }
    }

    private bool IsEnemy(ulong targetOwner)
    {
        if (_owner == null) _owner = GetComponent<MinionOwner>();
        if (_owner == null) return true;
        return targetOwner != _owner.OwnerClientId;
    }
}
