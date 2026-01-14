using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BuildableRallyAura : NetworkBehaviour
{
    [SerializeField] private float radius = 3.5f;
    [SerializeField] private float moveSpeedMultiplier = 1.1f;
    [SerializeField] private float attackSpeedMultiplier = 1.15f;
    [SerializeField] private float buffDurationSeconds = 1.0f;
    [SerializeField] private float tickSeconds = 0.5f;
    [SerializeField] private LayerMask targetMask;

    private float _nextTickTime;
    private BuildableInstance _owner;

    private void Awake()
    {
        _owner = GetComponent<BuildableInstance>();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (Time.time < _nextTickTime) return;

        _nextTickTime = Time.time + tickSeconds;
        ApplyBuffs();
    }

    private void ApplyBuffs()
    {
        int mask = targetMask.value != 0 ? targetMask.value : ~0;
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            if (IsAlly(col.transform))
            {
                var buff = col.GetComponentInParent<BuffReceiver>();
                if (buff != null)
                    buff.ApplyBuffServerRpc(moveSpeedMultiplier, attackSpeedMultiplier, buffDurationSeconds);
            }
        }
    }

    private bool IsAlly(Transform target)
    {
        if (_owner == null) return false;

        var no = target.GetComponentInParent<NetworkObject>();
        if (no != null && no.OwnerClientId == _owner.OwnerClientId)
            return true;

        var minionOwner = target.GetComponentInParent<MinionOwner>();
        return minionOwner != null && minionOwner.OwnerClientId == _owner.OwnerClientId;
    }
}
