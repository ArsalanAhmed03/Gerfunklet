using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DamageOverTimeReceiver : NetworkBehaviour
{
    private struct DotEntry
    {
        public int damagePerTick;
        public float tickInterval;
        public double nextTickTime;
        public double endTime;
    }

    private readonly List<DotEntry> _activeDots = new List<DotEntry>();

    [ServerRpc(RequireOwnership = false)]
    public void ApplyDotServerRpc(int damagePerTick, float duration, float tickInterval)
    {
        if (!IsServer) return;
        if (damagePerTick <= 0) return;
        if (duration <= 0f) return;
        if (tickInterval <= 0f) return;

        double now = GetServerTime();
        _activeDots.Add(new DotEntry
        {
            damagePerTick = damagePerTick,
            tickInterval = tickInterval,
            nextTickTime = now + tickInterval,
            endTime = now + duration
        });
    }

    private void Update()
    {
        if (!IsServer) return;
        if (_activeDots.Count == 0) return;

        double now = GetServerTime();
        for (int i = _activeDots.Count - 1; i >= 0; i--)
        {
            var dot = _activeDots[i];
            if (now >= dot.endTime)
            {
                _activeDots.RemoveAt(i);
                continue;
            }

            if (now >= dot.nextTickTime)
            {
                ApplyTick(dot.damagePerTick);
                dot.nextTickTime = now + dot.tickInterval;
                _activeDots[i] = dot;
            }
        }
    }

    private void ApplyTick(int damage)
    {
        var stats = GetComponent<PlayerStatsManager>();
        if (stats != null)
        {
            stats.TakeDamageServerRpc(damage);
            return;
        }

        var minion = GetComponent<MinionHealth>();
        if (minion != null)
        {
            minion.TakeDamage(damage);
        }
    }

    private double GetServerTime()
    {
        if (NetworkManager.Singleton != null)
            return NetworkManager.Singleton.ServerTime.Time;
        return Time.timeAsDouble;
    }
}
