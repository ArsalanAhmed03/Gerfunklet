using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MinionSystemOverload : NetworkBehaviour
{
    [SerializeField] private float range = 1.2f;
    [SerializeField] private float channelSeconds = 1.5f;
    [SerializeField] private float disableSeconds = 3f;
    [SerializeField] private int structureDamageTotal = 50;
    [SerializeField] private float structureDamageSeconds = 3f;
    [SerializeField] private float cooldownSeconds = 10f;
    [SerializeField] private LayerMask targetMask;

    private MinionOwner _owner;
    private MinionAI _ai;
    private float _readyTime;
    private bool _channeling;
    private float _channelEndTime;
    private Transform _channelTarget;
    private bool _savedAutoTargeting = true;
    private bool _savedAttacksEnabled = true;

    private void Awake()
    {
        _owner = GetComponent<MinionOwner>();
        _ai = GetComponent<MinionAI>();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (Time.time < _readyTime) return;

        if (_channeling)
        {
            if (_channelTarget == null)
            {
                _channeling = false;
                return;
            }

            float dist = Vector3.Distance(transform.position, _channelTarget.position);
            if (dist > range)
            {
                _channeling = false;
                _channelTarget = null;
                return;
            }

            if (Time.time >= _channelEndTime)
                CompleteChannel();
            return;
        }

        var target = FindTarget();
        if (target == null) return;

        if (Vector3.Distance(transform.position, target.position) <= range)
            BeginChannel(target);
    }

    private Transform FindTarget()
    {
        if (_ai != null && _ai.target != null)
        {
            if (IsValidTarget(_ai.target))
                return _ai.target;
        }

        int mask = targetMask.value != 0 ? targetMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, range, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            if (col == null) continue;
            var t = col.transform;
            if (IsValidTarget(t))
                return t;
        }

        return null;
    }

    private bool IsValidTarget(Transform target)
    {
        if (target == null) return false;

        var buildable = target.GetComponentInParent<BuildableHealth>();
        var buildableOwner = target.GetComponentInParent<BuildableInstance>();
        if (buildable != null && buildableOwner != null && IsEnemy(buildableOwner.OwnerClientId))
            return true;

        var citadel = target.GetComponentInParent<CitadelHealth>();
        if (citadel != null && IsEnemy(citadel.ownerClientId.Value))
            return true;

        var stats = target.GetComponentInParent<PlayerStatsManager>();
        if (stats != null && IsEnemy(stats.OwnerClientId))
            return true;

        var owner = target.GetComponentInParent<MinionOwner>();
        var minionStats = target.GetComponentInParent<MinionStats>();
        if (owner != null && IsEnemy(owner.OwnerClientId))
        {
            if (minionStats == null) return true;
            if (minionStats.TargetingMode == MinionStats.Targeting.StructuresFirst)
                return true;
            if (minionStats.RoleType == MinionStats.Role.Acolyte)
                return true;
        }

        return false;
    }

    private void BeginChannel(Transform target)
    {
        _channeling = true;
        _channelTarget = target;
        _channelEndTime = Time.time + channelSeconds;
        _readyTime = Time.time + cooldownSeconds;

        if (_ai != null)
        {
            _savedAutoTargeting = _ai.AutoTargeting;
            _savedAttacksEnabled = _ai.AttacksEnabled;
            _ai.AutoTargeting = false;
            _ai.AttacksEnabled = false;
        }
    }

    private void CompleteChannel()
    {
        if (_channelTarget == null)
        {
            _channeling = false;
            return;
        }

        var stats = _channelTarget.GetComponentInParent<PlayerStatsManager>();
        if (stats != null && IsEnemy(stats.OwnerClientId))
        {
            var disable = stats.GetComponent<CombatDisableReceiver>();
            if (disable != null)
                disable.ApplyDisableServerRpc(disableSeconds);
        }

        var minionOwner = _channelTarget.GetComponentInParent<MinionOwner>();
        var minionHealth = _channelTarget.GetComponentInParent<MinionHealth>();
        if (minionOwner != null && minionHealth != null && IsEnemy(minionOwner.OwnerClientId))
        {
            var disable = minionHealth.GetComponent<CombatDisableReceiver>();
            if (disable != null)
                disable.ApplyDisableServerRpc(disableSeconds);
        }

        var buildable = _channelTarget.GetComponentInParent<BuildableHealth>();
        var buildableOwner = _channelTarget.GetComponentInParent<BuildableInstance>();
        if (buildable != null && buildableOwner != null && IsEnemy(buildableOwner.OwnerClientId))
        {
            StartCoroutine(ApplyStructureDot(buildable, structureDamageTotal, structureDamageSeconds));
        }

        var citadel = _channelTarget.GetComponentInParent<CitadelHealth>();
        if (citadel != null && IsEnemy(citadel.ownerClientId.Value))
        {
            StartCoroutine(ApplyCitadelDot(citadel, structureDamageTotal, structureDamageSeconds));
        }

        _channeling = false;
        _channelTarget = null;

        if (_ai != null)
        {
            _ai.AutoTargeting = _savedAutoTargeting;
            _ai.AttacksEnabled = _savedAttacksEnabled;
        }
    }

    private IEnumerator ApplyStructureDot(BuildableHealth buildable, int totalDamage, float duration)
    {
        if (buildable == null) yield break;
        if (totalDamage <= 0 || duration <= 0f) yield break;

        int ticks = 5;
        float interval = duration / ticks;
        int perTick = Mathf.Max(1, Mathf.RoundToInt(totalDamage / (float)ticks));

        for (int i = 0; i < ticks; i++)
        {
            if (buildable == null) yield break;
            buildable.ApplyDamageServer(perTick);
            yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator ApplyCitadelDot(CitadelHealth citadel, int totalDamage, float duration)
    {
        if (citadel == null) yield break;
        if (totalDamage <= 0 || duration <= 0f) yield break;

        int ticks = 5;
        float interval = duration / ticks;
        int perTick = Mathf.Max(1, Mathf.RoundToInt(totalDamage / (float)ticks));

        for (int i = 0; i < ticks; i++)
        {
            if (citadel == null) yield break;
            citadel.ApplyDamageServer(perTick);
            yield return new WaitForSeconds(interval);
        }
    }

    private bool IsEnemy(ulong targetOwner)
    {
        if (_owner == null) _owner = GetComponent<MinionOwner>();
        if (_owner == null) return true;
        return targetOwner != _owner.OwnerClientId;
    }
}
