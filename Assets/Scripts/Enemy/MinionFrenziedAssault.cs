using Unity.Netcode;
using UnityEngine;

public class MinionFrenziedAssault : NetworkBehaviour
{
    [SerializeField] private float attackSpeedPerStack = 0.05f;
    [SerializeField] private int maxStacks = 3;
    [SerializeField] private float stackDurationSeconds = 2f;
    [SerializeField] private float lowHpThreshold = 0.5f;
    [SerializeField] private float damageReductionMultiplier = 0.9f;
    [SerializeField] private float damageReductionSeconds = 4f;

    private int _stacks;
    private float _lastHitTime;
    private bool _damageReductionUsed;
    private AttackSpeedModifierReceiver _attackSpeed;
    private MinionHealth _health;

    private void Awake()
    {
        _attackSpeed = GetComponent<AttackSpeedModifierReceiver>();
        _health = GetComponent<MinionHealth>();
    }

    private void Update()
    {
        if (!IsServer) return;

        if (_stacks > 0 && Time.time - _lastHitTime > stackDurationSeconds)
        {
            _stacks = 0;
            ApplyAttackSpeed();
        }

        if (!_damageReductionUsed && _health != null && _health.Health01 <= lowHpThreshold)
        {
            var dr = GetComponent<DamageReceiver>();
            if (dr != null)
                dr.ApplyDamageReductionServerRpc(damageReductionMultiplier, damageReductionSeconds);
            _damageReductionUsed = true;
        }
    }

    public void NotifyHit()
    {
        if (!IsServer) return;
        _stacks = Mathf.Min(maxStacks, _stacks + 1);
        _lastHitTime = Time.time;
        ApplyAttackSpeed();
    }

    private void ApplyAttackSpeed()
    {
        if (_attackSpeed == null) return;
        float multiplier = 1f + (_stacks * attackSpeedPerStack);
        _attackSpeed.ApplyAttackSpeedBuffServerRpc(multiplier, stackDurationSeconds);
    }
}
