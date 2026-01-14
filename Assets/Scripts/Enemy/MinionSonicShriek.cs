using Unity.Netcode;
using UnityEngine;

public class MinionSonicShriek : NetworkBehaviour
{
    [Header("Sonic Shriek (GDD)")]
    [SerializeField] private int damage = 20;
    [SerializeField] private float radius = 1.5f;
    [SerializeField] private float cooldownSeconds = 8f;
    [SerializeField] private int triggerEnemyCount = 3;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private float disorientDuration = 2f;
    [SerializeField] private float disorientAttackSpeedMultiplier = 0.75f;
    [SerializeField] private float disorientMissChance = 0.15f;
    [SerializeField] private LayerMask enemyMask;

    private MinionOwner _owner;
    private float _nextCheckTime;
    private float _readyTime;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        _owner = GetComponent<MinionOwner>();
        _readyTime = Time.time;
        TryShriek();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (Time.time < _nextCheckTime) return;
        _nextCheckTime = Time.time + checkInterval;
        if (Time.time < _readyTime) return;

        if (CountEnemiesInRadius(radius) >= triggerEnemyCount)
            TryShriek();
    }

    private void TryShriek()
    {
        if (Time.time < _readyTime) return;
        _readyTime = Time.time + cooldownSeconds;
        ApplyShriek();
    }

    private void ApplyShriek()
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            if (col == null) continue;

            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && IsEnemy(stats.OwnerClientId))
            {
                stats.TakeDamageServerRpc(damage);
                ApplyDisorient(col.transform);
                continue;
            }

            var owner = col.GetComponentInParent<MinionOwner>();
            var health = col.GetComponentInParent<MinionHealth>();
            if (health != null && owner != null && IsEnemy(owner.OwnerClientId))
            {
                health.TakeDamage(damage);
                ApplyDisorient(col.transform);
            }
        }
    }

    private void ApplyDisorient(Transform target)
    {
        var speed = target.GetComponentInParent<AttackSpeedModifierReceiver>();
        if (speed != null)
            speed.ApplyAttackSpeedDebuffServerRpc(disorientAttackSpeedMultiplier, disorientDuration);

        var miss = target.GetComponentInParent<MissChanceReceiver>();
        if (miss != null)
            miss.ApplyMissChanceServerRpc(disorientMissChance, disorientDuration);
    }

    private int CountEnemiesInRadius(float scanRadius)
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, scanRadius, mask, QueryTriggerInteraction.Ignore);
        int count = 0;

        foreach (var col in hits)
        {
            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && IsEnemy(stats.OwnerClientId))
            {
                count++;
                continue;
            }

            var owner = col.GetComponentInParent<MinionOwner>();
            if (owner != null && IsEnemy(owner.OwnerClientId))
                count++;
        }

        return count;
    }

    private bool IsEnemy(ulong targetOwner)
    {
        if (_owner == null) _owner = GetComponent<MinionOwner>();
        if (_owner == null) return true;
        return targetOwner != _owner.OwnerClientId;
    }
}
