using Unity.Netcode;
using UnityEngine;

public class MinionForageAgent : NetworkBehaviour
{
    [Header("Forage Rules")]
    [SerializeField] private float searchRadius = 8f;
    [SerializeField] private float retargetSeconds = 0.5f;

    private MinionAI _ai;
    private FoodCarrier _carrier;
    private MinionOwner _owner;
    private MinionStats _stats;
    private FeastRing _ring;
    private Transform _combatTarget;
    private FoodPile _foodTarget;
    private bool _forageEnabled;
    private float _nextSearchTime;

    private void Awake()
    {
        _ai = GetComponent<MinionAI>();
        _carrier = GetComponent<FoodCarrier>();
        _owner = GetComponent<MinionOwner>();
        _stats = GetComponent<MinionStats>();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!_forageEnabled) return;
        if (_ai == null || _carrier == null || _owner == null || _ring == null) return;
        if (_stats != null && _stats.TargetingMode == MinionStats.Targeting.StructuresFirst) return;

        if (_carrier.HasFood)
        {
            SetTarget(_ring.transform);
            return;
        }

        if (Time.time < _nextSearchTime)
            return;

        _nextSearchTime = Time.time + retargetSeconds;

        if (_foodTarget == null)
            _foodTarget = FindNearestFood();

        if (_foodTarget == null)
        {
            RestoreCombatTarget();
            return;
        }

        SetTarget(_foodTarget.transform);
    }

    public void SetForageEnabled(bool enabled, FeastRing ring)
    {
        if (!IsServer) return;

        _forageEnabled = enabled;
        _ring = ring;
        _foodTarget = null;
        _nextSearchTime = 0f;

        if (_ai != null)
            _ai.AttacksEnabled = !enabled;

        if (enabled)
        {
            if (_combatTarget == null && _ai != null)
                _combatTarget = _ai.target;
        }
        else
        {
            RestoreCombatTarget();
        }
    }

    private void RestoreCombatTarget()
    {
        if (_ai == null) return;
        if (_combatTarget != null)
            _ai.target = _combatTarget;
    }

    private void SetTarget(Transform target)
    {
        if (_ai == null) return;
        _ai.target = target;
    }

    private FoodPile FindNearestFood()
    {
        FoodPile best = null;
        float bestSqr = searchRadius * searchRadius;

        var piles = FindObjectsOfType<FoodPile>(true);
        foreach (var pile in piles)
        {
            if (pile == null) continue;
            float sqr = (pile.transform.position - transform.position).sqrMagnitude;
            if (sqr > bestSqr) continue;
            bestSqr = sqr;
            best = pile;
        }

        return best;
    }
}
