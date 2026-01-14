using Unity.Netcode;
using UnityEngine;

public class MinionGatherer : NetworkBehaviour
{
    [SerializeField] private float gatherSeconds = 3f;
    [SerializeField] private float harvestRange = 0.7f;
    [SerializeField] private float searchRadius = 8f;
    [SerializeField] private float retargetSeconds = 0.5f;
    [SerializeField] private float retreatHealth01 = 0.5f;

    private MinionAI _ai;
    private MinionStats _stats;
    private MinionOwner _owner;
    private MinionHealth _health;

    private ResourceNode _targetNode;
    private ResourceDeposit _deposit;
    private float _nextSearchTime;
    private float _harvestEndTime;
    private float _carriedAtp;
    private bool _isHarvesting;
    private bool _isScout;
    private bool _isRetreating;

    public bool HasCargo => _carriedAtp > 0f;

    private void Awake()
    {
        _ai = GetComponent<MinionAI>();
        _stats = GetComponent<MinionStats>();
        _owner = GetComponent<MinionOwner>();
        _health = GetComponent<MinionHealth>();
    }

    private void Start()
    {
        _isScout = _stats != null && _stats.RoleType == MinionStats.Role.Scout;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (_ai == null || _owner == null) return;
        if (_stats == null) return;

        if (_stats.RoleType != MinionStats.Role.Harvester && _stats.RoleType != MinionStats.Role.Scout)
            return;

        if (ShouldRetreat())
        {
            _isHarvesting = false;
            _targetNode = null;
            EnsureDepositTarget(true);
            return;
        }

        if (HasCargo)
        {
            EnsureDepositTarget();
            return;
        }

        if (_isHarvesting)
        {
            if (Time.time >= _harvestEndTime)
                CompleteHarvest();
            return;
        }

        if (Time.time < _nextSearchTime)
            return;

        _nextSearchTime = Time.time + retargetSeconds;
        FindNode();
        if (_targetNode == null)
        {
            RestoreCombat();
            return;
        }

        MoveToNode();
    }

    private void FindNode()
    {
        var nodes = FindObjectsOfType<ResourceNode>(true);
        ResourceNode best = null;
        float bestSqr = searchRadius * searchRadius;

        foreach (var node in nodes)
        {
            if (node == null) continue;
            if (node.energy.Value <= 0) continue;

            bool owned = node.ownerClientId.Value == ulong.MaxValue || node.ownerClientId.Value == _owner.OwnerClientId;
            if (_isScout && node.ownerClientId.Value == _owner.OwnerClientId)
                continue;
            if (!_isScout && !owned)
                continue;

            float sqr = (node.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = node;
            }
        }

        _targetNode = best;
    }

    private void MoveToNode()
    {
        if (_targetNode == null) return;

        _ai.AutoTargeting = false;
        _ai.AttacksEnabled = false;
        _ai.target = _targetNode.transform;

        float dist = Vector3.Distance(transform.position, _targetNode.transform.position);
        if (dist <= harvestRange)
            BeginHarvest();
    }

    private void BeginHarvest()
    {
        if (_targetNode == null) return;
        _isHarvesting = true;
        _harvestEndTime = Time.time + Mathf.Max(0.1f, gatherSeconds);
        _ai.target = null;
    }

    private void CompleteHarvest()
    {
        _isHarvesting = false;
        if (_targetNode == null)
            return;

        if (_targetNode.TryHarvestServer(_owner.OwnerClientId, _isScout, out float atpValue))
            _carriedAtp = atpValue;

        _targetNode = null;
        EnsureDepositTarget();
    }

    private void EnsureDepositTarget()
    {
        EnsureDepositTarget(false);
    }

    private void EnsureDepositTarget(bool forceRetreat)
    {
        if (!HasCargo && !forceRetreat)
        {
            RestoreCombat();
            return;
        }

        if (_deposit == null)
        {
            var deposits = FindObjectsOfType<ResourceDeposit>(true);
            foreach (var dep in deposits)
            {
                if (dep != null && dep.ownerClientId.Value == _owner.OwnerClientId)
                {
                    _deposit = dep;
                    break;
                }
            }
        }

        if (_deposit != null)
        {
            _ai.AutoTargeting = false;
            _ai.AttacksEnabled = false;
            _ai.target = _deposit.transform;
        }
        else if (!forceRetreat)
        {
            RestoreCombat();
        }
    }

    public float ConsumeCargo()
    {
        float amount = _carriedAtp;
        _carriedAtp = 0f;
        _deposit = null;
        RestoreCombat();
        return amount;
    }

    private void RestoreCombat()
    {
        _ai.AttacksEnabled = true;
        _ai.AutoTargeting = true;
    }

    private bool ShouldRetreat()
    {
        if (_health == null) return false;

        if (_health.Health01 <= retreatHealth01)
            _isRetreating = true;
        else if (_isRetreating && _health.Health01 > retreatHealth01)
            _isRetreating = false;

        return _isRetreating;
    }
}
