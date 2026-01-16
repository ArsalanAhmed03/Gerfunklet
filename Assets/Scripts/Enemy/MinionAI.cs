using Unity.Netcode;
using UnityEngine;

public class MinionAI : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public Transform target; // usually enemy base or nearest enemy
    [SerializeField] private float stopDistance = 0.1f;

    [Header("Combat")]
    public int damage = 10;
    public float attackRange = 1.5f;
    public float attackIntervalSeconds = 1f;
    [SerializeField] private bool destroyOnAttack = true;
    private float _nextAttackTime;
    public bool AttacksEnabled { get; set; } = true;
    public bool AutoTargeting { get; set; } = true;
    private float _nextRetargetTime;
    private float _nextHealTime;

    [Header("Targeting")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float retargetSeconds = 0.5f;
    [SerializeField] private LayerMask enemyMask;

    [Header("Guard (sleep protect)")]
    [SerializeField] private float guardRadius = 2.3f;
    [SerializeField] private float guardScanRadius = 3.5f;
    [SerializeField] private float guardStopDistance = 0.2f;
    [SerializeField] private float guardRetargetSeconds = 0.4f;
    private Transform _guardAnchor;
    private Vector3 _guardOffset;
    private float _nextGuardScanTime;
    private Transform _savedTarget;

    private MinionStats _stats;
    private MinionOwner _owner;

    private void Start()
    {
        // if (!IsOwner) return;

        // if (target == null)
        // {
        //     foreach (Transform child in GameManager.Instance.playerSpawns)
        //     {
        //         var stats = child.GetComponent<PlayerStatsManager>();
        //         if (stats != null && !stats.IsOwnedByLocalPlayer())
        //         {
        //             target = child;
        //             break;
        //         }
        //     }
        // }

        ApplyStatsOverrides();
        _stats = GetComponent<MinionStats>();
        _owner = GetComponent<MinionOwner>();

        if (!IsOwner) return;

        GetComponent<Animator>()?.SetBool("isWalking", true);
    }

    private void Update()
    {
        if (!IsServer) return;

        var stun = GetComponent<StunReceiver>();
        if (stun != null && stun.IsStunned)
            return;

        if (_guardAnchor != null)
        {
            UpdateGuard();
            return;
        }

        if (AutoTargeting)
            RetargetIfNeeded();

        if (target == null) return;

        // Move towards target (kite if configured)
        var kite = GetComponent<MinionKiteBehavior>();
        if (kite != null && kite.TryGetKiteDestination(transform, target, out var kiteDest))
            MoveTowards(kiteDest, kite.StopDistance);
        else
            MoveTowards(target.position, stopDistance);
        TryAttackTarget(target);
    }

    private void AttackTarget()
    {
        if (target == null) return;

        var miss = GetComponent<MissChanceReceiver>();
        if (miss != null && miss.MissChance > 0f && Random.value < miss.MissChance)
        {
            ScheduleNextAttack();
            return;
        }

        var parry = target.GetComponent<ParryReceiver>();
        if (parry != null && parry.IsParryActive)
        {
            var selfStun = GetComponent<StunReceiver>();
            if (selfStun != null)
                selfStun.ApplyStunServerRpc(0.4f);

            _nextAttackTime = Time.time + attackIntervalSeconds;
            return;
        }

        if (_stats != null && _stats.UseAoeAttack)
        {
            int hitCount = CountEnemiesInRadius(target.position, _stats.AoeRadius);
            if (hitCount >= _stats.AoeThreshold)
            {
                ApplyAoeDamage(target.position, _stats.AoeRadius, _stats.AoeDamage);
                ScheduleNextAttack();
                if (destroyOnAttack)
                    Destroy(gameObject);
                return;
            }
        }

        Debug.Log($"{gameObject.name} attacks {target.name} for {damage} damage!");
        int structureDamage = damage;
        if (_stats != null)
            structureDamage = Mathf.CeilToInt(damage * _stats.StructureDamageMultiplier);

        var citadel = target.GetComponent<CitadelHealth>();
        if (citadel != null)
        {
            citadel.ApplyDamageServer(structureDamage);
        }
        else
        {
            var targetStats = target.GetComponent<PlayerStatsManager>();
            if (targetStats != null)
            {
                targetStats.TakeDamageServerRpc(damage);
            }
            else
            {
                var buildable = target.GetComponent<BuildableHealth>();
                if (buildable != null)
                {
                    buildable.ApplyDamageServer(structureDamage);
                }
                else
                {
                    var targetMinion = target.GetComponentInParent<MinionHealth>();
                    if (targetMinion != null)
                        targetMinion.TakeDamage(damage);
                }
            }
        }
        ScheduleNextAttack();
        NotifyHit(target);

        if (destroyOnAttack)
            Destroy(gameObject);
    }

    private void TryAttackTarget(Transform activeTarget)
    {
        if (activeTarget == null) return;
        if (!AttacksEnabled) return;

        var disable = GetComponent<CombatDisableReceiver>();
        if (disable != null && disable.IsDisabled)
            return;

        if (_stats != null && _stats.CanHealAllies)
        {
            if (Time.time >= _nextHealTime)
            {
                if (TryHealAlly())
                {
                    _nextHealTime = Time.time + Mathf.Max(0.1f, _stats.HealIntervalSeconds);
                    return;
                }
            }
        }

        float distance = Vector3.Distance(transform.position, activeTarget.position);
        if (distance > attackRange) return;

        if (Time.time >= _nextAttackTime)
            AttackTarget();
    }

    private void ScheduleNextAttack()
    {
        float attackSpeedMul = 1f;
        var buff = GetComponent<BuffReceiver>();
        if (buff != null)
            attackSpeedMul = Mathf.Max(0.1f, buff.AttackSpeedMultiplier);

        var mod = GetComponent<AttackSpeedModifierReceiver>();
        if (mod != null)
            attackSpeedMul *= mod.Multiplier;

        _nextAttackTime = Time.time + (attackIntervalSeconds / Mathf.Max(0.1f, attackSpeedMul));
    }

    private void MoveTowards(Vector3 destination, float stopDist)
    {
        var root = GetComponent<RootReceiver>();
        if (root != null && root.IsRooted)
            return;

        Vector3 delta = destination - transform.position;
        float dist = delta.magnitude;
        if (dist <= stopDist)
            return;

        Vector3 direction = delta.normalized;
        float speedMul = 1f;
        var buff = GetComponent<BuffReceiver>();
        if (buff != null)
            speedMul *= buff.MoveSpeedMultiplier;

        var moveMod = GetComponent<MoveSpeedModifierReceiver>();
        if (moveMod != null)
            speedMul *= moveMod.Multiplier;

        transform.position += direction * moveSpeed * speedMul * Time.deltaTime;
        transform.forward = direction;
    }

    private void RetargetIfNeeded()
    {
        if (Time.time < _nextRetargetTime) return;
        _nextRetargetTime = Time.time + retargetSeconds;

        var next = AcquireTarget();
        if (next != null)
            target = next;
    }

    private Transform AcquireTarget()
    {
        var profile = GetComponent<MinionTargetingProfile>();
        if (profile != null)
        {
            var preferred = FindPreferredEnemyUnit(profile, detectionRadius);
            if (preferred != null) return preferred;
        }

        if (_stats != null && _stats.TargetingMode == MinionStats.Targeting.StructuresFirst)
        {
            var structure = FindNearestStructure(detectionRadius);
            if (structure != null) return structure;
            return FindNearestEnemyUnit(detectionRadius);
        }

        var unit = FindNearestEnemyUnit(detectionRadius);
        if (unit != null) return unit;
        return FindNearestStructure(detectionRadius);
    }

    private Transform FindPreferredEnemyUnit(MinionTargetingProfile profile, float radius)
    {
        if (profile == null || profile.PreferredRoles == null || profile.PreferredRoles.Length == 0)
            return null;

        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);
        Transform best = null;
        float bestScore = float.MaxValue;

        foreach (var col in hits)
        {
            var owner = col.GetComponentInParent<MinionOwner>();
            if (owner == null || IsFriendly(owner.OwnerClientId))
                continue;

            var stats = col.GetComponentInParent<MinionStats>();
            if (stats == null)
                continue;

            if (!IsPreferredRole(stats.RoleType, profile.PreferredRoles))
                continue;

            float score;
            if (profile.PreferLowestHp)
            {
                var health = col.GetComponentInParent<MinionHealth>();
                score = health != null ? health.Health01 : 1f;
            }
            else
            {
                score = (stats.transform.position - transform.position).sqrMagnitude;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = stats.transform;
            }
        }

        return best;
    }

    private bool IsPreferredRole(MinionStats.Role role, MinionStats.Role[] preferred)
    {
        foreach (var pref in preferred)
        {
            if (pref == role)
                return true;
        }
        return false;
    }

    private Transform FindNearestEnemyUnit(float radius)
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);
        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (var col in hits)
        {
            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && !IsFriendly(stats.OwnerClientId))
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
            if (owner != null && !IsFriendly(owner.OwnerClientId))
            {
                float sqr = (owner.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = owner.transform;
                }
            }
        }

        return best;
    }

    private Transform FindNearestStructure(float radius)
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);
        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (var col in hits)
        {
            var citadel = col.GetComponentInParent<CitadelHealth>();
            if (citadel != null && !IsFriendly(citadel.ownerClientId.Value))
            {
                float sqr = (citadel.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = citadel.transform;
                }
                continue;
            }
            var buildable = col.GetComponentInParent<BuildableInstance>();
            if (buildable != null && !IsFriendly(buildable.OwnerClientId))
            {
                float sqr = (buildable.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = buildable.transform;
                }
            }
        }

        return best;
    }

    private bool IsFriendly(ulong ownerClientId)
    {
        if (_owner == null) return false;
        return ownerClientId == _owner.OwnerClientId;
    }

    private bool TryHealAlly()
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, _stats.HealRange, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            var allyStats = col.GetComponentInParent<PlayerStatsManager>();
            if (allyStats != null && IsFriendly(allyStats.OwnerClientId))
            {
                float hp01 = allyStats.MaxHealth > 0 ? (float)allyStats.Health / allyStats.MaxHealth : 0f;
                if (hp01 <= _stats.HealBelowPercent)
                {
                    allyStats.Heal(_stats.HealAmount);
                    return true;
                }
                continue;
            }

            var allyHealth = col.GetComponentInParent<MinionHealth>();
            if (allyHealth != null)
            {
                var owner = col.GetComponentInParent<MinionOwner>();
                if (owner != null && IsFriendly(owner.OwnerClientId))
                {
                    if (allyHealth.Health01 <= _stats.HealBelowPercent)
                    {
                        allyHealth.Heal(_stats.HealAmount);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private int CountEnemiesInRadius(Vector3 center, float radius)
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(center, radius, mask, QueryTriggerInteraction.Ignore);
        int count = 0;

        foreach (var col in hits)
        {
            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && !IsFriendly(stats.OwnerClientId))
            {
                count++;
                continue;
            }

            var owner = col.GetComponentInParent<MinionOwner>();
            if (owner != null && !IsFriendly(owner.OwnerClientId))
                count++;
        }

        return count;
    }

    private void ApplyAoeDamage(Vector3 center, float radius, int aoeDamage)
    {
        int mask = enemyMask.value != 0 ? enemyMask.value : ~0;
        var hits = Physics.OverlapSphere(center, radius, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            var stats = col.GetComponentInParent<PlayerStatsManager>();
            if (stats != null && !IsFriendly(stats.OwnerClientId))
            {
                stats.TakeDamageServerRpc(aoeDamage);
                continue;
            }

            var minionHealth = col.GetComponentInParent<MinionHealth>();
            if (minionHealth != null)
            {
                var owner = col.GetComponentInParent<MinionOwner>();
                if (owner != null && !IsFriendly(owner.OwnerClientId))
                    minionHealth.TakeDamage(aoeDamage);
            }
        }
    }

    public void SetGuardAnchor(Transform anchor)
    {
        if (anchor == null)
        {
            ClearGuard();
            return;
        }

        if (_guardAnchor == anchor)
            return;

        _guardAnchor = anchor;
        _guardOffset = Random.insideUnitSphere;
        _guardOffset.y = 0f;
        if (_guardOffset.sqrMagnitude < 0.01f)
            _guardOffset = Vector3.forward;

        _guardOffset = _guardOffset.normalized * guardRadius;
        _savedTarget = target;
    }

    public void ClearGuard()
    {
        _guardAnchor = null;
        if (_savedTarget != null)
            target = _savedTarget;
        _savedTarget = null;
        AttacksEnabled = true;
    }

    private void UpdateGuard()
    {
        if (_guardAnchor == null) return;

        if (Time.time >= _nextGuardScanTime)
        {
            _nextGuardScanTime = Time.time + guardRetargetSeconds;
            var enemy = FindNearestEnemyUnit(guardScanRadius);
            if (enemy != null)
                target = enemy;
            else
                target = null;
        }

        if (target != null)
        {
            AttacksEnabled = true;
            MoveTowards(target.position, stopDistance);
            TryAttackTarget(target);
        }
        else
        {
            AttacksEnabled = false;
            Vector3 hold = _guardAnchor.position + _guardOffset;
            MoveTowards(hold, guardStopDistance);
        }
    }

    private void ApplyStatsOverrides()
    {
        var stats = GetComponent<MinionStats>();
        if (stats == null) return;

        damage = stats.Damage;
        moveSpeed = stats.MoveSpeed;
        attackRange = stats.AttackRange;
        attackIntervalSeconds = stats.AttackIntervalSeconds;
        destroyOnAttack = stats.DestroyOnAttack;
        if (stats.VisionRange > 0f)
            detectionRadius = stats.VisionRange;
    }

    private void NotifyHit(Transform hitTarget)
    {
        var swift = GetComponent<MinionSwiftStrike>();
        if (swift != null)
            swift.NotifyHit(hitTarget);

        var frenzy = GetComponent<MinionFrenziedAssault>();
        if (frenzy != null)
            frenzy.NotifyHit();

        var mark = GetComponent<MinionMarkOnHit>();
        if (mark != null)
            mark.NotifyHit(hitTarget);

        var burst = GetComponent<MinionBurstingImpact>();
        if (burst != null)
            burst.NotifyHit(hitTarget);

        var overcharge = GetComponent<MinionOvercharge>();
        if (overcharge != null)
            overcharge.NotifyHit(hitTarget);
    }
}
