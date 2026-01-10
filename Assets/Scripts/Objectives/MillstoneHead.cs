using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MillstoneHead : NetworkBehaviour
{
    //reload
    [Header("Rules")]
    [SerializeField] private float pickupHoldSeconds = 1f;
    [SerializeField] private float dropOffsetUp = 0.25f;
    [SerializeField] private float dropOffsetForward = 0.4f;
    [SerializeField] private float throwForce = 8f;
    [SerializeField] private float throwUpForce = 2f;
    [SerializeField] private float droppedLinearDrag = 2f;
    [SerializeField] private float droppedAngularDrag = 6f;
    [SerializeField] private float throwStopAfterSeconds = 1.5f;

    [Header("Collision")]
    [SerializeField] private Collider pickupCollider;

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

    private MillstoneCarrier _carrier;
    private Rigidbody _rb;
    private bool _isDropped;
    private Coroutine _throwStopRoutine;
    private double _pickupStartServerTime = -1;
    private ulong _pickupClientId = ulong.MaxValue;

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
            double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
            ulong candidate = FindPickupCandidate();

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

        CarrierClientId.Value = ulong.MaxValue;
        if (_carrier != null)
            _carrier.DetachHeadServer();

        _carrier = null;
        _isDropped = true;
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

    private ulong FindPickupCandidate()
    {
        if (CarrierClientId.Value != ulong.MaxValue) return ulong.MaxValue;

        int mask = LayerMask.GetMask("Player");
        if (mask == 0) mask = ~0;

        float radius = 0.6f;
        if (pickupCollider != null)
        {
            var ext = pickupCollider.bounds.extents;
            radius = Mathf.Max(ext.x, ext.z, 0.3f);
        }

        var hits = Physics.OverlapSphere(transform.position, radius, mask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var no = hit.GetComponentInParent<NetworkObject>();
            if (no == null) continue;
            return no.OwnerClientId;
        }

        return ulong.MaxValue;
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
    }
}
