using Unity.Netcode;
using UnityEngine;

public class MinionRetreatOnLowHealth : NetworkBehaviour
{
    [SerializeField] private float retreatHealth01 = 0.3f;
    [SerializeField] private float reengageHealth01 = 0.35f;

    private MinionAI _ai;
    private MinionHealth _health;
    private MinionOwner _owner;
    private bool _isRetreating;
    private bool _savedAutoTargeting;
    private bool _savedAttacksEnabled;

    private void Awake()
    {
        _ai = GetComponent<MinionAI>();
        _health = GetComponent<MinionHealth>();
        _owner = GetComponent<MinionOwner>();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (_ai == null || _health == null || _owner == null) return;

        if (!_isRetreating && _health.Health01 <= retreatHealth01)
        {
            StartRetreat();
        }
        else if (_isRetreating && _health.Health01 >= reengageHealth01)
        {
            StopRetreat();
        }

        if (_isRetreating)
            SetRetreatTarget();
    }

    private void StartRetreat()
    {
        _isRetreating = true;
        _savedAutoTargeting = _ai.AutoTargeting;
        _savedAttacksEnabled = _ai.AttacksEnabled;
        _ai.AutoTargeting = false;
        _ai.AttacksEnabled = false;
    }

    private void StopRetreat()
    {
        _isRetreating = false;
        _ai.AutoTargeting = _savedAutoTargeting;
        _ai.AttacksEnabled = _savedAttacksEnabled;
    }

    private void SetRetreatTarget()
    {
        if (LocalSpawner.Instance == null) return;
        var player = LocalSpawner.Instance.GetPlayerForClient(_owner.OwnerClientId);
        if (player == null) return;
        _ai.target = player.transform;
    }
}
