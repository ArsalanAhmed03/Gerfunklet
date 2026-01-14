using Unity.Netcode;
using UnityEngine;

public class MissChanceReceiver : NetworkBehaviour
{
    private NetworkVariable<float> missChance = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float _endTime;

    public float MissChance => missChance.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyMissChanceServerRpc(float chance, float duration)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;

        chance = Mathf.Clamp01(chance);
        if (chance < missChance.Value && Time.time < _endTime)
            return;

        missChance.Value = chance;
        _endTime = Time.time + duration;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        missChance.Value = 0f;
        _endTime = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (missChance.Value <= 0f) return;
        if (Time.time >= _endTime)
            missChance.Value = 0f;
    }
}
