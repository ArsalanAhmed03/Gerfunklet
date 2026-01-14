using Unity.Netcode;
using UnityEngine;

public class MoveSpeedModifierReceiver : NetworkBehaviour
{
    private NetworkVariable<float> buffMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> debuffMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float _buffEndTime;
    private float _debuffEndTime;

    public float Multiplier => buffMultiplier.Value * debuffMultiplier.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyMoveSpeedBuffServerRpc(float multiplier, float duration)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;

        multiplier = Mathf.Max(1f, multiplier);
        if (multiplier < buffMultiplier.Value && Time.time < _buffEndTime)
            return;

        buffMultiplier.Value = multiplier;
        _buffEndTime = Time.time + duration;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyMoveSpeedDebuffServerRpc(float multiplier, float duration)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;

        multiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        if (multiplier > debuffMultiplier.Value && Time.time < _debuffEndTime)
            return;

        debuffMultiplier.Value = multiplier;
        _debuffEndTime = Time.time + duration;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        buffMultiplier.Value = 1f;
        debuffMultiplier.Value = 1f;
        _buffEndTime = 0f;
        _debuffEndTime = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (buffMultiplier.Value != 1f && Time.time >= _buffEndTime)
            buffMultiplier.Value = 1f;

        if (debuffMultiplier.Value != 1f && Time.time >= _debuffEndTime)
            debuffMultiplier.Value = 1f;
    }
}
