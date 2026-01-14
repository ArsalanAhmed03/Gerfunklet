using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerMeleeAttack : NetworkBehaviour
{
    [Header("GDD Core Stats")]
    [SerializeField] private int attackDamage = 150;
    [SerializeField] private float attackIntervalSeconds = 3f;

    [Header("Targeting")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackRadius = 0.6f;
    [SerializeField] private LayerMask hitMask;

    private double _nextAttackServerTime;

    public void TryAttack()
    {
        if (!IsOwner) return;

        var stats = GetComponent<PlayerStatsManager>();
        if (stats != null)
        {
            if (!stats.IsAlive) return;
            if (stats.IsSleeping) return;
        }

        RequestAttackServerRpc();
    }

    [ServerRpc]
    private void RequestAttackServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        var stats = GetComponent<PlayerStatsManager>();
        if (stats != null)
        {
            if (!stats.IsAlive) return;
            if (stats.IsSleeping) return;
        }

        var stun = GetComponent<StunReceiver>();
        if (stun != null && stun.IsStunned)
            return;

        var disable = GetComponent<CombatDisableReceiver>();
        if (disable != null && disable.IsDisabled)
            return;

        if (MatchManager.Instance != null)
        {
            var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
            if (phase != MatchManager.MatchPhase.Playing && phase != MatchManager.MatchPhase.Overtime)
                return;
        }

        double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
        if (now < _nextAttackServerTime)
            return;

        float attackSpeedMul = 1f;
        var buff = GetComponent<BuffReceiver>();
        if (buff != null)
            attackSpeedMul *= buff.AttackSpeedMultiplier;

        var mod = GetComponent<AttackSpeedModifierReceiver>();
        if (mod != null)
            attackSpeedMul *= mod.Multiplier;

        attackSpeedMul = Mathf.Max(0.1f, attackSpeedMul);
        _nextAttackServerTime = now + (attackIntervalSeconds / attackSpeedMul);
        ApplyMeleeDamageServer();
        PlayAttackClientRpc();
    }

    [ClientRpc]
    private void PlayAttackClientRpc()
    {
        var animator = GetComponent<PlayerAnimator>();
        if (animator != null)
            animator.Attack();
    }

    private void ApplyMeleeDamageServer()
    {
        if (!IsServer) return;

        var miss = GetComponent<MissChanceReceiver>();
        if (miss != null && miss.MissChance > 0f && Random.value < miss.MissChance)
            return;

        int mask = hitMask.value == 0 ? ~0 : hitMask.value;
        Vector3 center = transform.position + transform.forward * attackRange;
        var hits = Physics.OverlapSphere(center, attackRadius, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        var unique = new HashSet<Transform>();
        int totalDamage = 0;

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            var stats = hit.GetComponentInParent<PlayerStatsManager>();
            if (stats != null)
            {
                if (stats.OwnerClientId == OwnerClientId)
                    continue;

                if (!unique.Add(stats.transform))
                    continue;

                var parry = hit.GetComponentInParent<ParryReceiver>();
                if (parry != null && parry.IsParryActive)
                {
                    var stun = GetComponent<StunReceiver>();
                    if (stun != null)
                        stun.ApplyStunServerRpc(0.4f);
                    continue;
                }

                stats.TakeDamageServerRpc(attackDamage);
                totalDamage += attackDamage;
                continue;
            }

            var minionHealth = hit.GetComponentInParent<MinionHealth>();
            if (minionHealth != null)
            {
                var owner = hit.GetComponentInParent<MinionOwner>();
                if (owner != null && owner.OwnerClientId == OwnerClientId)
                    continue;

                if (!unique.Add(minionHealth.transform))
                    continue;

                minionHealth.TakeDamage(attackDamage);
                totalDamage += attackDamage;
            }
        }

        if (totalDamage > 0)
        {
            var super = GetComponent<SuperCharge>();
            if (super != null)
                super.AddChargeFromDamageDealtServer(totalDamage);
        }
    }
}
