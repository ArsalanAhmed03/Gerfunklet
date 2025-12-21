using Unity.Netcode;
using UnityEngine;

public class DamageReceiver : NetworkBehaviour
{
    private NetworkVariable<float> damageMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float endTime;

    public float DamageMultiplier => damageMultiplier.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyDamageReductionServerRpc(float multiplier, float duration)
    {
        multiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        duration = Mathf.Max(0f, duration);

        damageMultiplier.Value = multiplier;
        endTime = Time.time + duration;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        damageMultiplier.Value = 1f;
        endTime = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (damageMultiplier.Value != 1f && Time.time >= endTime)
        {
            damageMultiplier.Value = 1f;
        }
    }
}
