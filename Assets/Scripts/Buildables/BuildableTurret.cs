using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BuildableTurret : NetworkBehaviour
{
    [SerializeField] private float range = 6f;
    [SerializeField] private int damage = 25;
    [SerializeField] private float attackIntervalSeconds = 1f;
    [SerializeField] private LayerMask targetMask;

    private float _nextAttackTime;
    private BuildableInstance _owner;

    private void Awake()
    {
        _owner = GetComponent<BuildableInstance>();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (Time.time < _nextAttackTime) return;

        var target = FindTarget();
        if (target == null) return;

        ApplyDamage(target);
        float attackSpeedMul = 1f;
        var mod = GetComponent<AttackSpeedModifierReceiver>();
        if (mod != null)
            attackSpeedMul = Mathf.Max(0.1f, mod.Multiplier);

        _nextAttackTime = Time.time + (attackIntervalSeconds / attackSpeedMul);
    }

    private Transform FindTarget()
    {
        int mask = targetMask.value != 0 ? targetMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, range, mask, QueryTriggerInteraction.Ignore);
        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (var col in hits)
        {
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

            var owner = col.GetComponentInParent<MinionOwner>();
            var health = col.GetComponentInParent<MinionHealth>();
            if (health != null && owner != null && IsEnemy(owner.OwnerClientId))
            {
                float sqr = (health.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = health.transform;
                }
            }
        }

        return best;
    }

    private void ApplyDamage(Transform target)
    {
        var stats = target.GetComponentInParent<PlayerStatsManager>();
        if (stats != null && IsEnemy(stats.OwnerClientId))
        {
            stats.TakeDamageServerRpc(damage);
            return;
        }

        var minionOwner = target.GetComponentInParent<MinionOwner>();
        var minionHealth = target.GetComponentInParent<MinionHealth>();
        if (minionHealth != null && minionOwner != null && IsEnemy(minionOwner.OwnerClientId))
        {
            minionHealth.TakeDamage(damage);
        }
    }

    private bool IsEnemy(ulong targetOwner)
    {
        if (_owner == null) return true;
        return targetOwner != _owner.OwnerClientId;
    }
}
