using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MinionUnstoppableCharge : NetworkBehaviour
{
    [SerializeField] private float chargeDistance = 3f;
    [SerializeField] private float chargeDuration = 0.4f;
    [SerializeField] private int damage = 75;
    [SerializeField] private float knockbackDistance = 2f;
    [SerializeField] private float knockbackSeconds = 0.15f;
    [SerializeField] private float cooldownSeconds = 15f;
    [SerializeField] private float damageReductionMultiplier = 0.5f;
    [SerializeField] private float triggerRange = 4f;
    [SerializeField] private LayerMask enemyMask;

    private MinionOwner _owner;
    private MinionAI _ai;
    private bool _charging;
    private float _chargeEndTime;
    private float _readyTime;
    private Vector3 _chargeDir;
    private float _chargeSpeed;
    private readonly HashSet<Transform> _hitTargets = new HashSet<Transform>();
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

        if (_charging)
        {
            UpdateCharge();
            return;
        }

        if (Time.time < _readyTime) return;
        if (_ai == null || _ai.target == null) return;

        float dist = Vector3.Distance(transform.position, _ai.target.position);
        if (dist > triggerRange) return;

        BeginCharge(_ai.target.position);
    }

    private void BeginCharge(Vector3 targetPosition)
    {
        _charging = true;
        _readyTime = Time.time + cooldownSeconds;
        _chargeEndTime = Time.time + Mathf.Max(0.05f, chargeDuration);

        Vector3 dir = (targetPosition - transform.position);
        dir.y = 0f;
        _chargeDir = dir.sqrMagnitude < 0.01f ? transform.forward : dir.normalized;
        _chargeSpeed = chargeDistance / Mathf.Max(0.05f, chargeDuration);
        _hitTargets.Clear();

        if (_ai != null)
        {
            _savedAutoTargeting = _ai.AutoTargeting;
            _savedAttacksEnabled = _ai.AttacksEnabled;
            _ai.AutoTargeting = false;
            _ai.AttacksEnabled = false;
        }

        var dr = GetComponent<DamageReceiver>();
        if (dr != null)
            dr.ApplyDamageReductionServerRpc(damageReductionMultiplier, chargeDuration);
    }

    private void UpdateCharge()
    {
        if (Time.time >= _chargeEndTime)
        {
            _charging = false;
            if (_ai != null)
            {
                _ai.AutoTargeting = _savedAutoTargeting;
                _ai.AttacksEnabled = _savedAttacksEnabled;
            }
            return;
        }

        transform.position += _chargeDir * _chargeSpeed * Time.deltaTime;
        transform.forward = _chargeDir;

        ApplyChargeHits();
    }

    private void ApplyChargeHits()
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, 0.6f, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            if (col == null) continue;
            var root = col.GetComponentInParent<Transform>();
            if (root == null) continue;
            if (_hitTargets.Contains(root)) continue;

            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && IsEnemy(stats.OwnerClientId))
            {
                stats.TakeDamageServerRpc(damage);
                var knock = stats.GetComponent<KnockbackReceiver>();
                if (knock != null)
                    knock.ApplyKnockbackServer(_chargeDir, knockbackDistance, knockbackSeconds);
                _hitTargets.Add(root);
                continue;
            }

            var minionOwner = col.GetComponentInParent<MinionOwner>();
            var minionHealth = col.GetComponentInParent<MinionHealth>();
            if (minionOwner != null && minionHealth != null && IsEnemy(minionOwner.OwnerClientId))
            {
                minionHealth.TakeDamage(damage);
                minionHealth.transform.position += _chargeDir * knockbackDistance;
                _hitTargets.Add(root);
                continue;
            }

            var buildable = col.GetComponentInParent<BuildableHealth>();
            var buildableOwner = col.GetComponentInParent<BuildableInstance>();
            if (buildable != null && buildableOwner != null && IsEnemy(buildableOwner.OwnerClientId))
            {
                buildable.ApplyDamageServer(damage * 2);
                _hitTargets.Add(root);
                continue;
            }

            var citadel = col.GetComponentInParent<CitadelHealth>();
            if (citadel != null && IsEnemy(citadel.ownerClientId.Value))
            {
                citadel.ApplyDamageServer(damage * 2);
                _hitTargets.Add(root);
            }
        }
    }

    private bool IsEnemy(ulong targetOwner)
    {
        if (_owner == null) _owner = GetComponent<MinionOwner>();
        if (_owner == null) return true;
        return targetOwner != _owner.OwnerClientId;
    }
}
