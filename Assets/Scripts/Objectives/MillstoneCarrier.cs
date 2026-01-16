using Unity.Netcode;
using UnityEngine;

public class MillstoneCarrier : NetworkBehaviour
{
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private float manualDropSeconds = 0.2f;

    public NetworkVariable<bool> IsCarrying = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private MillstoneHead _carriedHead;
    private Coroutine _manualDropRoutine;

    public Transform CarryAnchor => carryAnchor != null ? carryAnchor : transform;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            IsCarrying.Value = false;
    }

    public void AttachHeadServer(MillstoneHead head)
    {
        if (!IsServer) return;
        _carriedHead = head;
        IsCarrying.Value = head != null;
    }

    public void DetachHeadServer()
    {
        if (!IsServer) return;
        _carriedHead = null;
        IsCarrying.Value = false;
        StopManualDropRoutine();
    }

    [ServerRpc]
    public void ThrowCarriedHeadServerRpc(Vector3 direction)
    {
        if (!IsServer) return;
        if (_carriedHead == null) return;
        _carriedHead.ThrowServer(direction);

        var super = GetComponent<SuperCharge>();
        if (super != null)
            super.AddChargeFromObjectiveThrowServer();
    }

    [ServerRpc]
    public void DropCarriedHeadServerRpc()
    {
        if (!IsServer) return;
        if (_carriedHead == null) return;

        if (manualDropSeconds <= 0f)
        {
            DropCarriedHeadServer();
            return;
        }

        if (_manualDropRoutine != null) return;
        _manualDropRoutine = StartCoroutine(ManualDropAfterDelay());
    }

    public void DropCarriedHeadServer()
    {
        if (!IsServer) return;
        if (_carriedHead == null) return;
        StopManualDropRoutine();
        _carriedHead.DropServer();
    }

    public bool IsCarryingHead(MillstoneHead head)
    {
        return _carriedHead == head;
    }

    private System.Collections.IEnumerator ManualDropAfterDelay()
    {
        yield return new WaitForSeconds(manualDropSeconds);

        _manualDropRoutine = null;
        if (_carriedHead == null) yield break;
        _carriedHead.DropServer();
    }

    private void StopManualDropRoutine()
    {
        if (_manualDropRoutine == null) return;
        StopCoroutine(_manualDropRoutine);
        _manualDropRoutine = null;
    }
}
