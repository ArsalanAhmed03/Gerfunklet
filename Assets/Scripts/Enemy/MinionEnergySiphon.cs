using Unity.Netcode;
using UnityEngine;

public class MinionEnergySiphon : NetworkBehaviour
{
    [Header("Passive Drain")]
    [SerializeField] private float passiveRange = 4f;
    [SerializeField] private float passiveDrainPerSec = 0.2f;
    [SerializeField] private float passiveConvertPerSec = 0.1f;

    [Header("Active Drain")]
    [SerializeField] private float activeRange = 4f;
    [SerializeField] private float activeDrainPerSec = 1f;
    [SerializeField] private float activeDuration = 3f;
    [SerializeField] private float activeConvertTotal = 1.5f;
    [SerializeField] private float activeCooldownSeconds = 15f;
    [SerializeField] private LayerMask enemyMask;

    private MinionOwner _owner;
    private float _nextActiveTime;
    private float _activeEndTime;
    private float _nextActiveTick;

    private void Awake()
    {
        _owner = GetComponent<MinionOwner>();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (_owner == null) return;

        if (_activeEndTime > Time.time)
        {
            RunActiveDrain();
        }
        else if (Time.time >= _nextActiveTime)
        {
            TryStartActiveDrain();
        }

        RunPassiveDrain();
    }

    private void RunPassiveDrain()
    {
        if (!HasEnemyMinionInRange(passiveRange)) return;
        var resource = GetOwnerAtp();
        if (resource == null) return;

        resource.AddAtpServer(passiveConvertPerSec * Time.deltaTime);
    }

    private void TryStartActiveDrain()
    {
        var target = FindEnemyPlayer(activeRange);
        if (target == null) return;

        _activeEndTime = Time.time + activeDuration;
        _nextActiveTick = 0f;
        _nextActiveTime = Time.time + activeCooldownSeconds;
    }

    private void RunActiveDrain()
    {
        if (Time.time < _nextActiveTick) return;
        _nextActiveTick = Time.time + 1f;

        var target = FindEnemyPlayer(activeRange);
        if (target == null) return;

        var enemyAtp = target.GetComponent<AtpResource>();
        if (enemyAtp != null)
            enemyAtp.TryDrainServer(activeDrainPerSec);

        var ownerAtp = GetOwnerAtp();
        if (ownerAtp != null)
        {
            float convertPerTick = activeConvertTotal / Mathf.Max(1f, activeDuration);
            ownerAtp.AddAtpServer(convertPerTick);
        }
    }

    private bool HasEnemyMinionInRange(float radius)
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            var owner = col.GetComponentInParent<MinionOwner>();
            if (owner != null && owner.OwnerClientId != _owner.OwnerClientId)
                return true;
        }

        return false;
    }

    private PlayerStatsManager FindEnemyPlayer(float radius)
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);
        PlayerStatsManager best = null;
        float bestSqr = float.MaxValue;

        foreach (var col in hits)
        {
            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats == null) continue;
            if (stats.OwnerClientId == _owner.OwnerClientId) continue;

            float sqr = (stats.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = stats;
            }
        }

        return best;
    }

    private AtpResource GetOwnerAtp()
    {
        if (LocalSpawner.Instance == null) return null;
        var player = LocalSpawner.Instance.GetPlayerForClient(_owner.OwnerClientId);
        if (player == null) return null;
        return player.GetComponent<AtpResource>();
    }
}
