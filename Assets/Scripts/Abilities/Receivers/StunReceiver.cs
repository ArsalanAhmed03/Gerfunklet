using Unity.Netcode;
using UnityEngine;

public class StunReceiver : NetworkBehaviour
{
    private NetworkVariable<bool> isStunned = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float stunEndTime;
    private float ccImmuneUntil;

    public bool IsStunned => isStunned.Value;
    public bool IsCcImmune => Time.time < ccImmuneUntil;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyStunServerRpc(float duration)
    {
        Debug.Log($"ApplyStunServerRpc called on server for duration {duration}");
        if (duration <= 0f) return;
        if (IsCcImmune) return;

        isStunned.Value = true;
        stunEndTime = Time.time + duration;

        var carrier = GetComponent<MillstoneCarrier>();
        if (carrier != null && carrier.IsCarrying.Value)
            carrier.DropCarriedHeadServer();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!isStunned.Value) return;

        if (Time.time >= stunEndTime)
        {
            isStunned.Value = false;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;
        isStunned.Value = false;
        stunEndTime = 0f;
        ccImmuneUntil = 0f;
    }

    public void ApplyCcImmunityServer(float duration)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;
        ccImmuneUntil = Mathf.Max(ccImmuneUntil, Time.time + duration);
    }
}
