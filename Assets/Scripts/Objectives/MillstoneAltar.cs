using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MillstoneAltar : NetworkBehaviour
{
    [Header("Rules")]
    [SerializeField] private float channelSeconds = 2.5f;

    [Header("Auto assign owner")]
    [SerializeField] private bool autoAssignOwner = true;

    public NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> progress01 = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool _ownerAssigned;
    private MillstoneCarrier _currentCarrier;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        TryAutoAssignOwner();
    }

    private void Update()
    {
        if (!IsServer) return;

        TryAutoAssignOwner();

        if (_currentCarrier == null)
        {
            progress01.Value = 0f;
            return;
        }

        if (!IsCarrierValidForWin(_currentCarrier))
        {
            progress01.Value = 0f;
            _currentCarrier = null;
            return;
        }

        progress01.Value = Mathf.Clamp01(progress01.Value + Time.deltaTime / channelSeconds);
        if (progress01.Value >= 1f)
        {
            if (MatchManager.Instance != null)
                MatchManager.Instance.EndMatchImmediateServer(_currentCarrier.OwnerClientId);

            progress01.Value = 0f;
            _currentCarrier = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var carrier = other.GetComponentInParent<MillstoneCarrier>();
        if (carrier == null) return;

        if (!IsCarrierValidForWin(carrier)) return;

        _currentCarrier = carrier;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        var carrier = other.GetComponentInParent<MillstoneCarrier>();
        if (carrier == null) return;
        if (carrier != _currentCarrier) return;

        progress01.Value = 0f;
        _currentCarrier = null;
    }

    private bool IsCarrierValidForWin(MillstoneCarrier carrier)
    {
        if (carrier == null) return false;
        if (!carrier.IsCarrying.Value) return false;
        if (ownerClientId.Value == ulong.MaxValue) return false;
        if (carrier.OwnerClientId == ownerClientId.Value) return false;

        return true;
    }

    private void TryAutoAssignOwner()
    {
        if (!autoAssignOwner) return;
        if (_ownerAssigned) return;
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.TryGetTeamClientIds(out var a, out var b)) return;

        ownerClientId.Value = transform.position.x <= 0f ? a : b;
        _ownerAssigned = true;
    }
}
