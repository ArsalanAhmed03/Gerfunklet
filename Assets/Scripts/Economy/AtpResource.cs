using System;
using Unity.Netcode;
using UnityEngine;

public class AtpResource : NetworkBehaviour
{
    [Header("ATP Rules (GDD defaults)")]
    [SerializeField] private float atpCap = 10f;
    [SerializeField] private float atpRegenPerSec = 0.9f;
    [SerializeField] private float startAtp = 4f;
    [SerializeField] private float globalGcdSec = 0.5f;

    public NetworkVariable<float> Atp = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public event Action<float> OnAtpChanged;

    private double _nextSpendServerTime;

    public float AtpCap => atpCap;
    public float RegenPerSec => atpRegenPerSec;
    public float GlobalGcdSec => globalGcdSec;
    public float CurrentAtp => Atp.Value;

    public override void OnNetworkSpawn()
    {
        Atp.OnValueChanged += HandleAtpChanged;

        if (IsServer)
        {
            Atp.Value = Mathf.Clamp(startAtp, 0f, atpCap);
            _nextSpendServerTime = 0d;
        }

        OnAtpChanged?.Invoke(Atp.Value);
    }

    public override void OnNetworkDespawn()
    {
        Atp.OnValueChanged -= HandleAtpChanged;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Atp.Value < atpCap)
        {
            float regen = atpRegenPerSec * GetOvertimeRegenMultiplier();
            Atp.Value = Mathf.Min(atpCap, Atp.Value + regen * Time.deltaTime);
        }
    }

    public bool CanSpend(float cost)
    {
        if (!IsServer) return false;
        if (cost <= 0f) return true;
        return Atp.Value >= cost;
    }

    public bool TrySpendServer(float cost)
    {
        if (!IsServer) return false;
        if (cost <= 0f) return true;
        if (Atp.Value < cost) return false;
        if (GetServerTime() < _nextSpendServerTime) return false;

        Atp.Value = Mathf.Max(0f, Atp.Value - cost);
        _nextSpendServerTime = GetServerTime() + globalGcdSec;
        return true;
    }

    public void AddAtpServer(float amount)
    {
        if (!IsServer) return;
        if (amount <= 0f) return;
        Atp.Value = Mathf.Clamp(Atp.Value + amount, 0f, atpCap);
    }

    public bool TryDrainServer(float amount)
    {
        if (!IsServer) return false;
        if (amount <= 0f) return false;
        if (Atp.Value <= 0f) return false;

        Atp.Value = Mathf.Max(0f, Atp.Value - amount);
        return true;
    }

    [ServerRpc]
    public void TrySpendServerRpc(float cost, ServerRpcParams rpcParams = default)
    {
        bool ok = TrySpendServer(cost);

        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
            }
        };

        SpendResultClientRpc(ok, Atp.Value, target);
    }

    [ClientRpc]
    private void SpendResultClientRpc(bool ok, float remaining, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        if (!ok)
        {
            OnAtpChanged?.Invoke(Atp.Value);
            return;
        }

        OnAtpChanged?.Invoke(remaining);
    }

    private void HandleAtpChanged(float oldValue, float newValue)
    {
        if (!IsOwner) return;
        OnAtpChanged?.Invoke(newValue);
    }

    private double GetServerTime()
    {
        if (NetworkManager.Singleton != null)
            return NetworkManager.Singleton.ServerTime.Time;
        return Time.timeAsDouble;
    }

    private float GetOvertimeRegenMultiplier()
    {
        if (MatchManager.Instance == null) return 1f;
        return MatchManager.Instance.Phase.Value == (int)MatchManager.MatchPhase.Overtime ? 1.15f : 1f;
    }
}
