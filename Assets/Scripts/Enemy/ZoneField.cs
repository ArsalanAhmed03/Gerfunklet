using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ZoneField : NetworkBehaviour
{
    public enum ZoneType
    {
        Defensive,
        Disruption
    }

    [SerializeField] private float radius = 3f;
    [SerializeField] private float durationSeconds = 5f;
    [SerializeField] private float tickInterval = 1f;
    [SerializeField] private int disruptionDamagePerTick = 5;
    [SerializeField] private float disruptionSlowMultiplier = 0.8f;
    [SerializeField] private float defensiveDamageMultiplier = 0.85f;
    [SerializeField] private LayerMask unitMask;

    public NetworkVariable<ulong> OwnerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> ZoneKind = new NetworkVariable<int>(
        (int)ZoneType.Defensive,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float _endTime;
    private float _nextTickTime;

    public void InitServer(ulong ownerId, ZoneType zoneType, float customRadius, float customDuration)
    {
        if (!IsServer) return;
        OwnerClientId.Value = ownerId;
        ZoneKind.Value = (int)zoneType;
        radius = customRadius;
        durationSeconds = customDuration;
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
        ApplyZoneTick();
    }

    private void ApplyZoneTick()
    {
        int mask = unitMask.value != 0 ? unitMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);
        var type = (ZoneType)ZoneKind.Value;

        foreach (var col in hits)
        {
            if (col == null) continue;

            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null)
            {
                if (type == ZoneType.Defensive)
                {
                    if (!IsEnemy(stats.OwnerClientId))
                        ApplyDefense(stats);
                }
                else
                {
                    if (IsEnemy(stats.OwnerClientId))
                        ApplyDisruption(stats, null);
                }
                continue;
            }

            var minionOwner = col.GetComponentInParent<MinionOwner>();
            var minionHealth = col.GetComponentInParent<MinionHealth>();
            if (minionOwner != null && minionHealth != null)
            {
                if (type == ZoneType.Defensive)
                {
                    if (!IsEnemy(minionOwner.OwnerClientId))
                        ApplyDefense(minionHealth);
                }
                else
                {
                    if (IsEnemy(minionOwner.OwnerClientId))
                        ApplyDisruption(null, minionHealth);
                }
            }
        }
    }

    private void ApplyDefense(PlayerStatsManager stats)
    {
        var dr = stats.GetComponent<DamageReceiver>();
        if (dr != null)
            dr.ApplyDamageReductionServerRpc(defensiveDamageMultiplier, tickInterval + 0.1f);
    }

    private void ApplyDefense(MinionHealth minion)
    {
        var dr = minion.GetComponent<DamageReceiver>();
        if (dr != null)
            dr.ApplyDamageReductionServerRpc(defensiveDamageMultiplier, tickInterval + 0.1f);
    }

    private void ApplyDisruption(PlayerStatsManager stats, MinionHealth minion)
    {
        if (stats != null)
        {
            stats.TakeDamageServerRpc(disruptionDamagePerTick);
            var slow = stats.GetComponent<MoveSpeedModifierReceiver>();
            if (slow != null)
                slow.ApplyMoveSpeedDebuffServerRpc(disruptionSlowMultiplier, tickInterval + 0.1f);
            return;
        }

        if (minion != null)
        {
            minion.TakeDamage(disruptionDamagePerTick);
            var slow = minion.GetComponent<MoveSpeedModifierReceiver>();
            if (slow != null)
                slow.ApplyMoveSpeedDebuffServerRpc(disruptionSlowMultiplier, tickInterval + 0.1f);
        }
    }

    private bool IsEnemy(ulong targetOwner)
    {
        return OwnerClientId.Value != ulong.MaxValue && targetOwner != OwnerClientId.Value;
    }
}
