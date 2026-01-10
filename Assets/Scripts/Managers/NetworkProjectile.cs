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

    // optional: who fired it so we don't hit them
    private ulong _ownerClientId;

    private void Awake()
    {
        // Safe place to call Unity APIs
        playerMask = LayerMask.GetMask("Player");

        // Optional: warn if the layer doesn't exist (mask becomes 0)
        if (playerMask == 0)
            Debug.LogWarning("NetworkProjectile: LayerMask for 'Player' is 0. Make sure a layer named 'Player' exists and players are on it.");
    }

    public void InitServer(Vector3 direction, ulong ownerClientId, int dmg)
    {
        dir = direction.normalized;
        dieAt = Time.time + lifeSeconds;
        _ownerClientId = ownerClientId;
        damage = dmg;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (_done) return;

        Vector3 start = transform.position;
        Vector3 end = start + dir * speed * Time.deltaTime;

        // SphereCast to avoid tunneling
        Vector3 delta = end - start;
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
                    transform.position = end;
                }
                else
                {
                    var stats = hit.collider.GetComponentInParent<PlayerStatsManager>();
                    if (stats != null)
                    {

                        var parry = hit.collider.GetComponentInParent<ParryReceiver>();
                        if (parry != null && parry.IsParryActive)
                        {
                            // Optional: punish attacker on successful parry (same pattern as Stomp)
                            // var attackerStun = ??? (needs attacker reference; easiest is to just nullify hit for now)
                            SafeDespawn();
                            return;
                        }
                        
                        stats.TakeDamageServerRpc(damage);

                        // var stun = hit.collider.GetComponentInParent<StunReceiver>();
                        // if (stun != null)
                        //     stun.ApplyStunServerRpc(stunDuration);
                    }

                    var citadel = hit.collider.GetComponentInParent<CitadelHealth>();
                    if (citadel != null)
                    {
                        citadel.ApplyDamageServer(damage);
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
