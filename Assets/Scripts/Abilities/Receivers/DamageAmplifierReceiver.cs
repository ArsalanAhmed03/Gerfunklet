using Unity.Netcode;
using UnityEngine;

public class DamageAmplifierReceiver : NetworkBehaviour
{
    private NetworkVariable<float> damageMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float _endTime;

    public float DamageMultiplier => damageMultiplier.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyDamageAmplifierServerRpc(float multiplier, float duration)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;

        multiplier = Mathf.Clamp(multiplier, 1f, 2f);
        if (multiplier < damageMultiplier.Value && Time.time < _endTime)
            return;

        damageMultiplier.Value = multiplier;
        _endTime = Time.time + duration;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        damageMultiplier.Value = 1f;
        _endTime = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (damageMultiplier.Value <= 1f) return;
        if (Time.time >= _endTime)
            damageMultiplier.Value = 1f;
    }
}
