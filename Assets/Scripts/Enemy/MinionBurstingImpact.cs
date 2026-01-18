using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MinionBurstingImpact : NetworkBehaviour
{
    [Header("Impact")]
    [SerializeField] private float chargeDistance = 2f;
    [SerializeField] private float chargeDuration = 0.35f;
    [SerializeField] private int impactDamage = 60;
    [SerializeField] private float impactRadius = 1.5f;
    [SerializeField] private float slowMultiplier = 0.75f;
    [SerializeField] private float slowDuration = 1f;
    [SerializeField] private LayerMask enemyMask;

    [Header("Electrified Spikes")]
    [SerializeField] private float electrifiedSeconds = 8f;
    [SerializeField] private int dotDamage = 5;
    [SerializeField] private float dotDuration = 2f;
    [SerializeField] private float dotTickInterval = 2f;

    private MinionOwner _owner;
    private bool _impactDone;
    private float _electrifiedUntil;
    private Vector3 _chargeDir;
    private float _chargeSpeed;
    private float _chargeEndTime;
    private MinionAI _ai;
    private bool _savedAutoTargeting = true;
    private bool _savedAttacksEnabled = true;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        _owner = GetComponent<MinionOwner>();
        _ai = GetComponent<MinionAI>();
        BeginCharge();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (_impactDone) return;

        UpdateCharge();
    }

    public void NotifyHit(Transform target)
    {
        if (!IsServer) return;
        if (Time.time > _electrifiedUntil) return;
        if (target == null) return;

        var dot = target.GetComponentInParent<DamageOverTimeReceiver>();
        if (dot != null)
            dot.ApplyDotServerRpc(dotDamage, dotDuration, dotTickInterval);
    }

    private void BeginCharge()
    {
        _impactDone = false;
        _chargeEndTime = Time.time + Mathf.Max(0.05f, chargeDuration);
        _chargeSpeed = chargeDistance / Mathf.Max(0.05f, chargeDuration);
        _chargeDir = transform.forward;
        _chargeDir.y = 0f;
        if (_chargeDir.sqrMagnitude < 0.01f)
            _chargeDir = Vector3.forward;

        if (_ai != null)
        {
            _savedAutoTargeting = _ai.AutoTargeting;
            _savedAttacksEnabled = _ai.AttacksEnabled;
            _ai.AutoTargeting = false;
            _ai.AttacksEnabled = false;
        }
    }

    private void UpdateCharge()
    {
        transform.position += _chargeDir.normalized * _chargeSpeed * Time.deltaTime;

        if (HitEnemyNow() || Time.time >= _chargeEndTime)
        {
            Explode();
            _impactDone = true;
            if (_ai != null)
            {
                _ai.AutoTargeting = _savedAutoTargeting;
                _ai.AttacksEnabled = _savedAttacksEnabled;
            }
        }
    }

    private bool HitEnemyNow()
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, 0.4f, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            if (col == null) continue;
            if (IsEnemyCollider(col))
                return true;
        }

        return false;
    }

    private void Explode()
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, impactRadius, mask, QueryTriggerInteraction.Ignore);
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
                stats.TakeDamageServerRpc(impactDamage);
                var slow = stats.GetComponent<MoveSpeedModifierReceiver>();
                if (slow != null)
                    slow.ApplyMoveSpeedDebuffServerRpc(slowMultiplier, slowDuration);
                continue;
            }

            var minionOwner = col.GetComponentInParent<MinionOwner>();
            var minionHealth = col.GetComponentInParent<MinionHealth>();
            if (minionOwner != null && minionHealth != null && IsEnemy(minionOwner.OwnerClientId))
            {
                minionHealth.TakeDamage(impactDamage);
                var slow = minionHealth.GetComponent<MoveSpeedModifierReceiver>();
                if (slow != null)
                    slow.ApplyMoveSpeedDebuffServerRpc(slowMultiplier, slowDuration);
                continue;
            }

            var buildable = col.GetComponentInParent<BuildableHealth>();
            var buildableOwner = col.GetComponentInParent<BuildableInstance>();
            if (buildable != null && buildableOwner != null && IsEnemy(buildableOwner.OwnerClientId))
            {
                buildable.ApplyDamageServer(impactDamage);
                continue;
            }

            var citadel = col.GetComponentInParent<CitadelHealth>();
            if (citadel != null && IsEnemy(citadel.ownerClientId.Value))
            {
                citadel.ApplyDamageServer(impactDamage);
            }
        }

        _electrifiedUntil = Time.time + electrifiedSeconds;
    }

    private bool IsEnemyCollider(Collider col)
    {
        var stats = col.GetComponentInParent<PlayerStatsManager>();
        if (stats != null && IsEnemy(stats.OwnerClientId)) return true;

        var owner = col.GetComponentInParent<MinionOwner>();
        if (owner != null && IsEnemy(owner.OwnerClientId)) return true;

        var buildable = col.GetComponentInParent<BuildableHealth>();
        var buildableOwner = col.GetComponentInParent<BuildableInstance>();
        if (buildable != null && buildableOwner != null && IsEnemy(buildableOwner.OwnerClientId)) return true;

        var citadel = col.GetComponentInParent<CitadelHealth>();
        if (citadel != null && IsEnemy(citadel.ownerClientId.Value)) return true;

        return false;
    }

    private bool IsEnemy(ulong targetOwner)
    {
        if (_owner == null) _owner = GetComponent<MinionOwner>();
        if (_owner == null) return true;
        return targetOwner != _owner.OwnerClientId;
    }
}
