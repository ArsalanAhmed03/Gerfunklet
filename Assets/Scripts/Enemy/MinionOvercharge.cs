using Unity.Netcode;
using UnityEngine;

public class MinionOvercharge : NetworkBehaviour
{
    [SerializeField] private float overchargeSeconds = 5f;
    [SerializeField] private float cooldownSeconds = 20f;
    [SerializeField] private float cooldownDebuffSeconds = 3f;
    [SerializeField] private float attackSpeedMultiplier = 1.5f;
    [SerializeField] private float cooldownAttackSpeedMultiplier = 0.5f;
    [SerializeField] private float triggerRange = 6f;
    [SerializeField] private int triggerEnemyCount = 3;
    [SerializeField] private int highHpThreshold = 500;
    [SerializeField] private LayerMask enemyMask;

    private float _readyTime;
    private float _overchargeEndTime;
    private bool _cooldownDebuffPending;
    private AttackSpeedModifierReceiver _attackSpeed;
    private MinionOwner _owner;
    private MinionAI _ai;

    private void Awake()
    {
        _attackSpeed = GetComponent<AttackSpeedModifierReceiver>();
        _owner = GetComponent<MinionOwner>();
        _ai = GetComponent<MinionAI>();
    }

    private void Update()
    {
        if (!IsServer) return;

        if (_cooldownDebuffPending && Time.time >= _overchargeEndTime)
        {
            if (_attackSpeed != null && cooldownDebuffSeconds > 0f)
                _attackSpeed.ApplyAttackSpeedDebuffServerRpc(cooldownAttackSpeedMultiplier, cooldownDebuffSeconds);
            _cooldownDebuffPending = false;
        }

        if (IsOvercharged)
            return;

        if (Time.time < _readyTime)
            return;

        if (ShouldTriggerOvercharge())
            ActivateOvercharge();
    }

    public bool IsOvercharged => Time.time < _overchargeEndTime;

    public void NotifyHit(Transform primaryTarget)
    {
        if (!IsServer) return;
        if (!IsOvercharged) return;
        if (primaryTarget == null) return;
        if (_ai == null) _ai = GetComponent<MinionAI>();
        if (_ai == null) return;

        var secondary = FindSecondaryTarget(primaryTarget, 1.5f);
        if (secondary == null) return;

        ApplyDamageToTarget(secondary, _ai.damage);
    }

    private bool ShouldTriggerOvercharge()
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, triggerRange, mask, QueryTriggerInteraction.Ignore);
        int enemyCount = 0;

        foreach (var col in hits)
        {
            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && IsEnemy(stats.OwnerClientId))
                return true;

            var owner = col.GetComponentInParent<MinionOwner>();
            var health = col.GetComponentInParent<MinionHealth>();
            if (owner != null && health != null && IsEnemy(owner.OwnerClientId))
            {
                enemyCount++;
                if (health.maxHealth >= highHpThreshold)
                    return true;
            }
        }

        return enemyCount >= triggerEnemyCount;
    }

    private void ActivateOvercharge()
    {
        if (_attackSpeed != null)
            _attackSpeed.ApplyAttackSpeedBuffServerRpc(attackSpeedMultiplier, overchargeSeconds);

        _overchargeEndTime = Time.time + overchargeSeconds;
        _readyTime = Time.time + cooldownSeconds;
        _cooldownDebuffPending = cooldownDebuffSeconds > 0f;
    }

    private Transform FindSecondaryTarget(Transform primary, float radius)
    {
        if (primary == null) return null;
        Vector3 center = primary.position;
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(center, radius, mask, QueryTriggerInteraction.Ignore);
        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (var col in hits)
        {
            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && IsEnemy(stats.OwnerClientId))
            {
                if (stats.transform == primary) continue;
                float sqr = (stats.transform.position - center).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = stats.transform;
                }
                continue;
            }

            var owner = col.GetComponentInParent<MinionOwner>();
            var health = col.GetComponentInParent<MinionHealth>();
            if (owner != null && health != null && IsEnemy(owner.OwnerClientId))
            {
                if (health.transform == primary) continue;
                float sqr = (health.transform.position - center).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = health.transform;
                }
            }
        }

        return best;
    }

    private void ApplyDamageToTarget(Transform target, int dmg)
    {
        var stats = target.GetComponentInParent<PlayerStatsManager>();
        if (stats != null && IsEnemy(stats.OwnerClientId))
        {
            stats.TakeDamageServerRpc(dmg);
            return;
        }

        var owner = target.GetComponentInParent<MinionOwner>();
        var health = target.GetComponentInParent<MinionHealth>();
        if (owner != null && health != null && IsEnemy(owner.OwnerClientId))
        {
            health.TakeDamage(dmg);
        }
    }

    private bool IsEnemy(ulong targetOwner)
    {
        if (_owner == null) _owner = GetComponent<MinionOwner>();
        if (_owner == null) return true;
        return targetOwner != _owner.OwnerClientId;
    }
}
