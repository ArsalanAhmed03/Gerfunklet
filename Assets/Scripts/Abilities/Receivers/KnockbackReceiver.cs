using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class KnockbackReceiver : NetworkBehaviour
{
    private Coroutine _knockRoutine;
    private float _knockbackEndTime;

    public bool IsKnockedBack => Time.time < _knockbackEndTime;

    public void ApplyKnockbackServer(Vector3 direction, float distance, float duration)
    {
        if (!IsServer) return;
        if (distance <= 0f) return;

        var stun = GetComponent<StunReceiver>();
        if (stun != null && stun.IsCcImmune)
            return;

        var carrier = GetComponent<MillstoneCarrier>();
        if (carrier != null && carrier.IsCarrying.Value)
            carrier.DropCarriedHeadServer();

        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        };

        ApplyKnockbackClientRpc(direction.normalized, distance, duration, target);
    }

    [ClientRpc]
    private void ApplyKnockbackClientRpc(Vector3 direction, float distance, float duration, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        if (_knockRoutine != null)
            StopCoroutine(_knockRoutine);

        if (duration <= 0f)
        {
            transform.position += direction.normalized * distance;
            _knockbackEndTime = 0f;
            return;
        }

        _knockRoutine = StartCoroutine(KnockbackRoutine(direction.normalized, distance, duration));
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float distance, float duration)
    {
        _knockbackEndTime = Time.time + duration;

        float remaining = duration;
        float speed = distance / Mathf.Max(0.0001f, duration);

        while (remaining > 0f)
        {
            float dt = Time.deltaTime;
            transform.position += direction * (speed * dt);
            remaining -= dt;
            yield return null;
        }

        _knockbackEndTime = 0f;
        _knockRoutine = null;
    }
}
