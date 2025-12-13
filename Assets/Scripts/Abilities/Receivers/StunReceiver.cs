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

    public bool IsStunned => isStunned.Value;

    [ServerRpc(RequireOwnership = false)]
    public void ApplyStunServerRpc(float duration)
    {
        Debug.Log($"ApplyStunServerRpc called on server for duration {duration}");
        if (duration <= 0f) return;

        isStunned.Value = true;
        stunEndTime = Time.time + duration;
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
}
