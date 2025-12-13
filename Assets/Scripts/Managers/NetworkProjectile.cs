using Unity.Netcode;
using UnityEngine;

public class NetworkProjectile : NetworkBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private int damage = 40;
    [SerializeField] private float lifeSeconds = 2.5f;
    [SerializeField] private float hitRadius = 0.25f;

    [SerializeField] private float stunDuration = 4f;

    private Vector3 dir;
    private float dieAt;
    private bool _done;

    // optional: who fired it so we don't hit them
    private ulong _ownerClientId;

    public void InitServer(Vector3 direction, ulong ownerClientId)
    {
        dir = direction.normalized;
        dieAt = Time.time + lifeSeconds;
        _ownerClientId = ownerClientId;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (_done) return;

        Vector3 start = transform.position;
        Vector3 end = start + dir * speed * Time.deltaTime;

        // Only hit players (layer must exist)
        int playerMask = LayerMask.GetMask("Player");

        // SphereCast to avoid tunneling
        Vector3 delta = end - start;
        float dist = delta.magnitude;

        if (dist > 0f)
        {
            if (Physics.SphereCast(start, hitRadius, delta.normalized, out RaycastHit hit, dist, playerMask, QueryTriggerInteraction.Ignore))
            {
                var stats = hit.collider.GetComponentInParent<PlayerStatsManager>();

                // Ignore self-owner (important)
                var hitNetObj = hit.collider.GetComponentInParent<NetworkObject>();
                if (hitNetObj != null && hitNetObj.OwnerClientId == _ownerClientId)
                {
                    // just move forward, no hit
                    transform.position = end;
                }
                else
                {
                    if (stats != null)
                    {
                        stats.TakeDamageServerRpc(damage);

                        var stun = hit.collider.GetComponentInParent<StunReceiver>();
                        if (stun != null)
                            stun.ApplyStunServerRpc(stunDuration);
                    }

                    SafeDespawn();
                    return;
                }
            }
        }

        transform.position = end;

        if (Time.time >= dieAt)
            SafeDespawn();
    }

    private void SafeDespawn()
    {
        if (!IsServer) return;
        if (_done) return;
        _done = true;

        var nob = GetComponent<NetworkObject>();
        if (nob != null && nob.IsSpawned)
            nob.Despawn();
        else
            Destroy(gameObject);
    }
}
