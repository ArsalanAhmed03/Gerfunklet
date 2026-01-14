using Unity.Netcode;
using UnityEngine;

public class ThrownObject : NetworkBehaviour
{
    private Vector3 _velocity;
    private float _gravity;
    private float _dieAt;
    private bool _done;
    private ulong _ownerClientId;
    private int _damage;
    private float _knockbackDistance;
    private float _knockbackSeconds;
    private float _hitRadius;
    private int _hitMask;

    private MinionAI _minionAi;
    private Rigidbody _rb;

    public void BeginThrowServer(ulong ownerClientId, Vector3 direction, float speed, float upVelocity, float gravity,
        int damage, float knockbackDistance, float knockbackSeconds, float lifeSeconds, float hitRadius, int hitMask)
    {
        if (!IsServer) return;

        _ownerClientId = ownerClientId;
        _velocity = direction.normalized * speed + Vector3.up * upVelocity;
        _gravity = gravity;
        _damage = damage;
        _knockbackDistance = knockbackDistance;
        _knockbackSeconds = knockbackSeconds;
        _dieAt = Time.time + lifeSeconds;
        _hitRadius = Mathf.Max(0.05f, hitRadius);
        _hitMask = hitMask != 0 ? hitMask : ~0;

        _minionAi = GetComponent<MinionAI>();
        if (_minionAi != null)
            _minionAi.enabled = false;

        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        if (_done) return;

        Vector3 start = transform.position;
        _velocity += Vector3.down * _gravity * Time.deltaTime;
        Vector3 delta = _velocity * Time.deltaTime;
        float dist = delta.magnitude;

        if (dist > 0f)
        {
            if (Physics.SphereCast(start, _hitRadius, delta.normalized, out RaycastHit hit, dist, _hitMask, QueryTriggerInteraction.Ignore))
            {
                var hitNetObj = hit.collider.GetComponentInParent<NetworkObject>();
                if (hitNetObj != null && hitNetObj.OwnerClientId == _ownerClientId)
                {
                    transform.position = start + delta;
                }
                else
                {
                    HandleHit(hit);
                    return;
                }
            }
        }

        transform.position = start + delta;

        if (Time.time >= _dieAt)
            SafeDespawn();
    }

    private void HandleHit(RaycastHit hit)
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

        var stats = hit.collider.GetComponentInParent<PlayerStatsManager>();
        if (stats != null && _damage > 0)
        {
            stats.TakeDamageServerRpc(_damage);
            ApplyKnockback(hit.collider.transform);
            AddOwnerSuperCharge(_damage);
            SafeDespawn();
            return;
        }

        var minion = hit.collider.GetComponentInParent<MinionAI>();
        if (minion != null && _damage > 0)
        {
            var health = minion.GetComponent<MinionHealth>();
            if (health != null)
                health.TakeDamage(_damage);

            ApplyKnockback(minion.transform);
            AddOwnerSuperCharge(_damage);
            SafeDespawn();
            return;
        }

        SafeDespawn();
    }

    private void ApplyKnockback(Transform target)
    {
        if (_knockbackDistance <= 0f) return;

        var knock = target.GetComponentInParent<KnockbackReceiver>();
        if (knock != null)
        {
            knock.ApplyKnockbackServer(_velocity.normalized, _knockbackDistance, _knockbackSeconds);
        }
        else
        {
            target.position += _velocity.normalized * _knockbackDistance;
        }
    }

    private void AddOwnerSuperCharge(int damage)
    {
        var super = GetOwnerSuperCharge();
        if (super != null)
            super.AddChargeFromDamageDealtServer(damage);
    }

    private SuperCharge GetOwnerSuperCharge()
    {
        if (NetworkManager.Singleton == null) return null;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(_ownerClientId, out var cc)) return null;
        return cc.PlayerObject != null ? cc.PlayerObject.GetComponent<SuperCharge>() : null;
    }

    private StunReceiver GetOwnerStunReceiver()
    {
        if (NetworkManager.Singleton == null) return null;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(_ownerClientId, out var cc)) return null;
        return cc.PlayerObject != null ? cc.PlayerObject.GetComponent<StunReceiver>() : null;
    }

    private void SafeDespawn()
    {
        if (!IsServer) return;
        if (_done) return;
        _done = true;

        var nob = GetComponent<NetworkObject>();
        if (nob != null && nob.IsSpawned)
            nob.Despawn(true);
        else
            Destroy(gameObject);
    }
}
