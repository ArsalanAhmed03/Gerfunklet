using Unity.Netcode;
using UnityEngine;

public class BoulderPitchProjectile : NetworkBehaviour
{
    [SerializeField] private float gravity = 25f;
    [SerializeField] private float lifeSeconds = 4f;
    [SerializeField] private float hitRadius = 0.4f;

    private Vector3 _velocity;
    private float _dieAt;
    private bool _done;
    private ulong _ownerClientId;
    private int _damageToStructures;
    private int _damageToPlayers;
    private int _hitMask;

    public void InitServer(Vector3 forward, ulong ownerClientId, float forwardSpeed, float upVelocity, int damageToStructures, int damageToPlayers, int hitMask)
    {
        _ownerClientId = ownerClientId;
        _velocity = forward.normalized * forwardSpeed + Vector3.up * upVelocity;
        _damageToStructures = damageToStructures;
        _damageToPlayers = damageToPlayers;
        _hitMask = hitMask != 0 ? hitMask : ~0;
        _dieAt = Time.time + lifeSeconds;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (_done) return;

        Vector3 start = transform.position;
        _velocity += Vector3.down * gravity * Time.deltaTime;
        Vector3 delta = _velocity * Time.deltaTime;
        float dist = delta.magnitude;

        if (dist > 0f)
        {
            if (Physics.SphereCast(start, hitRadius, delta.normalized, out RaycastHit hit, dist, _hitMask, QueryTriggerInteraction.Ignore))
            {
                var hitNetObj = hit.collider.GetComponentInParent<NetworkObject>();
                if (hitNetObj != null && hitNetObj.OwnerClientId == _ownerClientId)
                {
                    transform.position = start + delta;
                }
                else
                {
                    var citadel = hit.collider.GetComponentInParent<CitadelHealth>();
                    if (citadel != null && _damageToStructures > 0)
                        citadel.ApplyDamageServer(_damageToStructures);

                    var stats = hit.collider.GetComponentInParent<PlayerStatsManager>();
                    if (stats != null && _damageToPlayers > 0)
                        stats.TakeDamageServerRpc(_damageToPlayers);

                    SafeDespawn();
                    return;
                }
            }
        }

        transform.position = start + delta;

        if (Time.time >= _dieAt)
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
