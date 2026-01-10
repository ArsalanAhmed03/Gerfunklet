using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ThroneCapture : NetworkBehaviour
{
    [Header("Rules")]
    [SerializeField] private float channelSeconds = 4f;
    [SerializeField] private CitadelHealth requiredCitadel;

    [Header("Owner")]
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

        if (!IsCaptureAllowed(_currentCarrier))
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

        if (!IsCaptureAllowed(carrier)) return;

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

    private bool IsCaptureAllowed(MillstoneCarrier carrier)
    {
        if (carrier == null) return false;
        if (ownerClientId.Value == ulong.MaxValue) return false;
        if (carrier.OwnerClientId == ownerClientId.Value) return false;

        if (requiredCitadel == null) return false;
        if (!requiredCitadel.destroyed.Value) return false;

        return true;
    }

    private void TryAutoAssignOwner()
    {
        if (_ownerAssigned) return;
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.TryGetTeamClientIds(out var a, out var b)) return;

        ownerClientId.Value = transform.position.x <= 0f ? a : b;
        _ownerAssigned = true;
    }
}
