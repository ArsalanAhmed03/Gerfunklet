using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MillstoneHead : NetworkBehaviour
{
    //reload
    [Header("Rules")]
    [SerializeField] private float pickupHoldSeconds = 1f;
    [SerializeField] private float contestRadius = 0.8f;
    [SerializeField] private float dropOffsetUp = 0.25f;
    [SerializeField] private float dropOffsetForward = 0.4f;
    [SerializeField] private float throwForce = 8f;
    [SerializeField] private float throwUpForce = 2f;
    [SerializeField] private float droppedLinearDrag = 2f;
    [SerializeField] private float droppedAngularDrag = 6f;
    [SerializeField] private float throwStopAfterSeconds = 1.5f;
    [SerializeField] private int throwDamage = 40;
    [SerializeField] private float throwKnockbackDistance = 2.5f;
    [SerializeField] private float throwKnockbackSeconds = 0.2f;
    [SerializeField] private float throwStunSeconds = 0.2f;

    [Header("Collision")]
    [SerializeField] private Collider pickupCollider;
    [SerializeField] private GameObject impactFxPrefab;
    [SerializeField] private float impactFxLifeSeconds = 1.5f;

    [Header("Owner (home)")]
    public NetworkVariable<ulong> OwnerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<ulong> CarrierClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsDropped = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsContested = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private MillstoneCarrier _carrier;
    private Rigidbody _rb;
    private bool _isDropped;
    private Coroutine _throwStopRoutine;
    private double _pickupStartServerTime = -1;
    private ulong _pickupClientId = ulong.MaxValue;
    private bool _isThrown;
    private ulong _throwOwnerClientId = ulong.MaxValue;

    private Vector3 _homePosition;
    private Quaternion _homeRotation;

    private void Awake()
    {
        CacheCollider();
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CarrierClientId.Value = ulong.MaxValue;
            _pickupStartServerTime = -1;
            _pickupClientId = ulong.MaxValue;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _isDropped = false;
            IsDropped.Value = false;
            IsContested.Value = false;
            _isThrown = false;
            _throwOwnerClientId = ulong.MaxValue;
            SetAtRestState();
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (_carrier != null)
        {
            transform.SetPositionAndRotation(_carrier.CarryAnchor.position, _carrier.CarryAnchor.rotation);
        }
        else
        {
            if (!_isDropped)
            {
                // Home pickup: owner can grab immediately.
                if (OwnerClientId.Value != ulong.MaxValue && IsOwnerInRing())
                    TryPickupServer(OwnerClientId.Value);
            }
            else
            {
                double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
                bool contested;
                ulong candidate = FindPickupCandidate(out contested);

                IsContested.Value = contested;

                if (contested)
                {
                    _pickupClientId = ulong.MaxValue;
                    _pickupStartServerTime = -1;
                    return;
                }

                if (candidate != _pickupClientId)
                {
                    _pickupClientId = candidate;
                    _pickupStartServerTime = candidate != ulong.MaxValue ? now : -1;
                }

                if (_pickupStartServerTime > 0d && _pickupClientId != ulong.MaxValue)
                {
                    if (now - _pickupStartServerTime >= pickupHoldSeconds)
                        TryPickupServer(_pickupClientId);
                }
            }
        }
    }

    private void TryPickupServer(ulong clientId)
    {
        if (!IsServer) return;
        if (CarrierClientId.Value != ulong.MaxValue) return;

        if (OwnerClientId.Value == ulong.MaxValue) return;
        if (!_isDropped && OwnerClientId.Value != clientId) return;

        var player = LocalSpawner.Instance != null ? LocalSpawner.Instance.GetPlayerForClient(clientId) : null;
        if (player == null) return;

        var carrier = player.GetComponent<MillstoneCarrier>();
        if (carrier == null) return;
        if (carrier.IsCarrying.Value) return;

        CarrierClientId.Value = clientId;
        _carrier = carrier;
        _carrier.AttachHeadServer(this);
        _isDropped = false;
        IsDropped.Value = false;
        IsContested.Value = false;
        _isThrown = false;
        _throwOwnerClientId = ulong.MaxValue;
        SetCarriedState();

        _pickupClientId = ulong.MaxValue;
        _pickupStartServerTime = -1;
    }

    public void DropServer()
    {
        if (!IsServer) return;
        if (CarrierClientId.Value == ulong.MaxValue) return;

        Vector3 forward = _carrier != null ? _carrier.transform.forward : transform.forward;

        CarrierClientId.Value = ulong.MaxValue;
        if (_carrier != null)
            _carrier.DetachHeadServer();

        _carrier = null;
        _isDropped = true;
        IsDropped.Value = true;
        IsContested.Value = false;
        _isThrown = false;
        _throwOwnerClientId = ulong.MaxValue;
        SetDroppedState();

        var pos = transform.position;
        pos.y += dropOffsetUp;
        pos += forward.normalized * dropOffsetForward;
        transform.position = pos;
    }

    public void ThrowServer(Vector3 direction)
    {
        if (!IsServer) return;
        if (CarrierClientId.Value == ulong.MaxValue) return;

        _throwOwnerClientId = CarrierClientId.Value;
        CarrierClientId.Value = ulong.MaxValue;
        if (_carrier != null)
            _carrier.DetachHeadServer();

        _carrier = null;
        _isDropped = true;
        IsDropped.Value = true;
        IsContested.Value = false;
        _isThrown = true;
        SetDroppedState();

        if (_rb != null)
        {
            Vector3 dir = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.AddForce(dir * throwForce + Vector3.up * throwUpForce, ForceMode.VelocityChange);
        }

        StopThrowRoutineIfNeeded();
        _throwStopRoutine = StartCoroutine(StopThrowAfterSeconds());
    }

    public void ResetToHomeServer()
    {
        if (!IsServer) return;

        DropServer();
        transform.SetPositionAndRotation(_homePosition, _homeRotation);
        _isDropped = false;
        IsDropped.Value = false;
        IsContested.Value = false;
        _isThrown = false;
        _throwOwnerClientId = ulong.MaxValue;
        SetAtRestState();
    }

    private void SetCarriedState()
    {
        SetRigidbodyKinematic(true);
        SetRigidbodyDrag(false);
        StopThrowRoutineIfNeeded();
        SetTriggerMode(true);
    }

    private void SetAtRestState()
    {
        SetRigidbodyKinematic(true);
        SetRigidbodyDrag(false);
        StopThrowRoutineIfNeeded();
        SetTriggerMode(true);
    }

    private void SetDroppedState()
    {
        SetRigidbodyKinematic(false);
        SetRigidbodyDrag(true);
        SetTriggerMode(false);
    }

    private void SetRigidbodyKinematic(bool kinematic)
    {
        if (_rb == null) return;
        _rb.isKinematic = kinematic;
        _rb.detectCollisions = !kinematic;
        if (kinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    private void SetRigidbodyDrag(bool dropped)
    {
        if (_rb == null) return;
        _rb.linearDamping = dropped ? droppedLinearDrag : 0f;
        _rb.angularDamping = dropped ? droppedAngularDrag : 0.05f;
    }

    private void SetTriggerMode(bool isTrigger)
    {
        if (pickupCollider == null) return;
        pickupCollider.isTrigger = isTrigger;
    }

    private void CacheCollider()
    {
        if (pickupCollider == null)
            pickupCollider = GetComponent<Collider>();

        if (pickupCollider != null)
            pickupCollider.isTrigger = true;
    }

    private bool IsOwnerInRing()
    {
        if (OwnerClientId.Value == ulong.MaxValue) return false;

        int mask = LayerMask.GetMask("Player");
        if (mask == 0) mask = ~0;

        float radius = GetPickupRadius();
        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var no = hit.GetComponentInParent<NetworkObject>();
            if (no == null) continue;
            if (no.OwnerClientId == OwnerClientId.Value)
                return true;
        }

        return false;
    }

    private ulong FindPickupCandidate(out bool contested)
    {
        if (CarrierClientId.Value != ulong.MaxValue)
        {
            contested = false;
            return ulong.MaxValue;
        }

        int mask = LayerMask.GetMask("Player");
        if (mask == 0) mask = ~0;

        float radius = Mathf.Max(contestRadius, GetPickupRadius());

        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);
        ulong teamA = ulong.MaxValue;
        ulong teamB = ulong.MaxValue;

        foreach (var hit in hits)
        {
            var no = hit.GetComponentInParent<NetworkObject>();
            if (no == null) continue;
            if (teamA == ulong.MaxValue || teamA == no.OwnerClientId)
            {
                teamA = no.OwnerClientId;
            }
            else
            {
                teamB = no.OwnerClientId;
                break;
            }
        }

        contested = teamA != ulong.MaxValue && teamB != ulong.MaxValue;
        return contested ? ulong.MaxValue : teamA;
    }

    private float GetPickupRadius()
    {
        float radius = 0.6f;
        if (pickupCollider != null)
        {
            var ext = pickupCollider.bounds.extents;
            radius = Mathf.Max(ext.x, ext.z, 0.3f);
        }

        return radius;
    }

    private void StopThrowRoutineIfNeeded()
    {
        if (_throwStopRoutine != null)
        {
            StopCoroutine(_throwStopRoutine);
            _throwStopRoutine = null;
        }
    }

    private System.Collections.IEnumerator StopThrowAfterSeconds()
    {
        if (throwStopAfterSeconds <= 0f) yield break;

        yield return new WaitForSeconds(throwStopAfterSeconds);

        if (_rb == null) yield break;
        if (CarrierClientId.Value != ulong.MaxValue) yield break;

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _isThrown = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (!_isThrown) return;

        var hitTransform = collision.collider.transform;

        Vector3 hitPoint = collision.GetContact(0).point;
        Vector3 hitNormal = collision.GetContact(0).normal;
        SpawnImpactFxClientRpc(hitPoint, hitNormal);

        if (IsFriendly(hitTransform))
            return;

        var stats = hitTransform.GetComponentInParent<PlayerStatsManager>();
        if (stats != null)
        {
            if (throwDamage > 0)
                stats.TakeDamageServerRpc(throwDamage);

            var stun = hitTransform.GetComponentInParent<StunReceiver>();
            if (stun != null && throwStunSeconds > 0f)
                stun.ApplyStunServerRpc(throwStunSeconds);

            var knock = hitTransform.GetComponentInParent<KnockbackReceiver>();
            if (knock != null && throwKnockbackDistance > 0f)
                knock.ApplyKnockbackServer(_rb != null ? _rb.linearVelocity : transform.forward, throwKnockbackDistance, throwKnockbackSeconds);
        }

        var minion = hitTransform.GetComponentInParent<MinionAI>();
        if (minion != null)
        {
            var health = minion.GetComponent<MinionHealth>();
            if (health != null && throwDamage > 0)
                health.TakeDamage(throwDamage);

            if (throwKnockbackDistance > 0f)
                minion.transform.position += transform.forward * throwKnockbackDistance;
        }

        _isThrown = false;
    }

    [ClientRpc]
    private void SpawnImpactFxClientRpc(Vector3 position, Vector3 normal)
    {
        if (impactFxPrefab == null) return;
        var rot = normal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
        var fx = Instantiate(impactFxPrefab, position, rot);
        if (impactFxLifeSeconds > 0f)
            Destroy(fx, impactFxLifeSeconds);
    }

    private bool IsFriendly(Transform target)
    {
        if (_throwOwnerClientId == ulong.MaxValue) return false;

        var no = target.GetComponentInParent<NetworkObject>();
        if (no != null && no.OwnerClientId == _throwOwnerClientId)
            return true;

        var minionOwner = target.GetComponentInParent<MinionOwner>();
        return minionOwner != null && minionOwner.OwnerClientId == _throwOwnerClientId;
    }
}
