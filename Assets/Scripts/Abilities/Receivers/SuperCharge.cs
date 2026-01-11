using Unity.Netcode;
using UnityEngine;

public class SuperCharge : NetworkBehaviour
{
    [Header("Charge Sources (tunable)")]
    [SerializeField] private float chargePerDamageDealt = 0.002f;
    [SerializeField] private float chargePerDamageTaken = 0.002f;
    [SerializeField] private float chargePerObjectiveThrow = 0.05f;

    public NetworkVariable<float> Charge01 = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsFull => Charge01.Value >= 1f - 0.0001f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            Charge01.Value = 0f;
    }

    public void AddChargeFromDamageDealtServer(int damage)
    {
        if (!IsServer) return;
        if (damage <= 0) return;
        AddChargeServer(damage * chargePerDamageDealt);
    }

    public void AddChargeFromDamageTakenServer(int damage)
    {
        if (!IsServer) return;
        if (damage <= 0) return;
        AddChargeServer(damage * chargePerDamageTaken);
    }

    public void AddChargeFromObjectiveThrowServer()
    {
        if (!IsServer) return;
        AddChargeServer(chargePerObjectiveThrow);
    }

    public bool TryConsumeFullServer()
    {
        if (!IsServer) return false;
        if (!IsFull) return false;
        Charge01.Value = 0f;
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        Charge01.Value = 0f;
    }

    private void AddChargeServer(float amount)
    {
        if (amount <= 0f) return;
        Charge01.Value = Mathf.Clamp01(Charge01.Value + amount);
    }
}
