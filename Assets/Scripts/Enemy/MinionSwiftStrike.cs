using Unity.Netcode;
using UnityEngine;

public class MinionSwiftStrike : NetworkBehaviour
{
    [SerializeField] private float moveSpeedMultiplier = 1.2f;
    [SerializeField] private float durationSeconds = 3f;

    private MoveSpeedModifierReceiver _move;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        _move = GetComponent<MoveSpeedModifierReceiver>();
        ApplyBoost();
    }

    public void NotifyHit(Transform target)
    {
        if (!IsServer) return;
        if (target == null) return;

        var ai = target.GetComponentInParent<MinionAI>();
        if (ai != null && ai.target == transform)
            return;

        ApplyBoost();
    }

    private void ApplyBoost()
    {
        if (_move == null) return;
        _move.ApplyMoveSpeedBuffServerRpc(moveSpeedMultiplier, durationSeconds);
    }
}
