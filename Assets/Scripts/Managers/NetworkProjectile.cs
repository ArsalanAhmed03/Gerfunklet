using Unity.Netcode;
using UnityEngine;

public class NetworkProjectile : NetworkBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private int damage = 40;
    [SerializeField] private float lifeSeconds = 2.5f;
    [SerializeField] private float hitRadius = 0.25f;
    [SerializeField] private float stunDuration = 4f;

    // was static readonly -> move to runtime init
    private int playerMask;

    private Vector3 dir;
    private float dieAt;
    private bool _done;
    private bool _useArc;
    private Vector3 _velocity;
    private float _gravity;

    // optional: who fired it so we don't hit them
    private ulong _ownerClientId;
    private float _knockbackDistance;
    private float _knockbackSeconds;

    private void Awake()
    {
        // Safe place to call Unity APIs
        playerMask = LayerMask.GetMask("Player");

        // Optional: warn if the layer doesn't exist (mask becomes 0)
        if (playerMask == 0)
            Debug.LogWarning("NetworkProjectile: LayerMask for 'Player' is 0. Make sure a layer named 'Player' exists and players are on it.");
    }

    public void InitServer(Vector3 direction, ulong ownerClientId, int dmg, float knockbackDistance, float knockbackSeconds, float arcUpVelocity, float arcGravity)
    {
        dir = direction.normalized;
        dieAt = Time.time + lifeSeconds;
        _ownerClientId = ownerClientId;
        damage = dmg;
        _knockbackDistance = knockbackDistance;
        _knockbackSeconds = knockbackSeconds;
        _useArc = arcUpVelocity > 0f || arcGravity > 0f;
        _gravity = arcGravity;
        if (_useArc)
            _velocity = dir * speed + Vector3.up * arcUpVelocity;
    }

    private SuperCharge GetOwnerSuperCharge()
    {
        if (NetworkManager.Singleton == null) return null;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(_ownerClientId, out var cc))
            return null;

        var playerObj = cc.PlayerObject;
        return playerObj != null ? playerObj.GetComponent<SuperCharge>() : null;
    }

    private StunReceiver GetOwnerStunReceiver()
    {
        if (NetworkManager.Singleton == null) return null;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(_ownerClientId, out var cc))
            return null;

        var playerObj = cc.PlayerObject;
        return playerObj != null ? playerObj.GetComponent<StunReceiver>() : null;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (_done) return;

        Vector3 start = transform.position;
        Vector3 delta;
        if (_useArc)
        {
            _velocity += Vector3.down * _gravity * Time.deltaTime;
            delta = _velocity * Time.deltaTime;
        }
        else
        {
            delta = dir * speed * Time.deltaTime;
        }

        // SphereCast to avoid tunneling
        float dist = delta.magnitude;

        if (dist > 0f)
        {
            if (Physics.SphereCast(
                    start,
                    hitRadius,
                    delta.normalized,
                    out RaycastHit hit,
                    dist,
                    playerMask,
                    QueryTriggerInteraction.Ignore))
            {
                // Ignore self-owner (important)
                var hitNetObj = hit.collider.GetComponentInParent<NetworkObject>();
                if (hitNetObj != null && hitNetObj.OwnerClientId == _ownerClientId)
                {
                    transform.position = start + delta;
                }
                else
                {
                    var stats = hit.collider.GetComponentInParent<PlayerStatsManager>();
                    if (stats != null)
                    {

                        var parry = hit.collider.GetComponentInParent<ParryReceiver>();
                        if (parry != null && parry.IsParryActive)
                        {
                            var attackerStun = GetOwnerStunReceiver();
                            if (attackerStun != null)
                                attackerStun.ApplyStunServerRpc(0.4f);
                            SafeDespawn();
                            return;
                        }
                        
                        stats.TakeDamageServerRpc(damage);

                        var super = GetOwnerSuperCharge();
                        if (super != null)
                            super.AddChargeFromDamageDealtServer(damage);

                        if (_knockbackDistance > 0f)
                        {
                            var knock = hit.collider.GetComponentInParent<KnockbackReceiver>();
                            if (knock != null)
                            {
                                knock.ApplyKnockbackServer(dir, _knockbackDistance, _knockbackSeconds);
                            }
                        }

                        // var stun = hit.collider.GetComponentInParent<StunReceiver>();
                        // if (stun != null)
                        //     stun.ApplyStunServerRpc(stunDuration);
                    }

                    var citadel = hit.collider.GetComponentInParent<CitadelHealth>();
                    if (citadel != null)
                    {
                        citadel.ApplyDamageServer(damage);
                    }

                    var minion = hit.collider.GetComponentInParent<MinionAI>();
                    if (minion != null && _knockbackDistance > 0f)
                    {
                        minion.transform.position += dir * _knockbackDistance;
                    }

                    SafeDespawn();
                    return;
                }
            }
        }

        transform.position = start + delta;

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
