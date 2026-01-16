using UnityEngine;
using Unity.Netcode;

public class DeploymentRules : NetworkBehaviour
{
    [Header("Deployment Ring (GDD defaults)")]
    [SerializeField] private float baseDeployRadius = 8f;
    [SerializeField] private float forwardDeployRadius = 8f;
    [SerializeField] private Transform homeAnchor;
    [SerializeField] private float midlineX = 0f;
    [SerializeField] private bool allowForwardBeacon = true;
    [SerializeField] private float forwardBeaconRecheckSeconds = 0.25f;

    [Header("Placement Blocking (optional)")]
    [SerializeField] private LayerMask blockingMask;
    [SerializeField] private float blockingRadius = 0.35f;

    private Vector3 _homePosition;
    private bool _homeSet;
    private float _homeSign;
    private float _nextBeaconCheckTime;
    private bool _cachedForwardBeacon;

    public override void OnNetworkSpawn()
    {
        if (!_homeSet)
            SetHomePosition(transform.position);
    }

    public void SetHomePosition(Vector3 position)
    {
        _homePosition = position;
        _homeSet = true;
        _homeSign = position.x >= 0f ? 1f : -1f;
    }

    public Vector3 GetAnchorPosition(out bool forwardUnlocked)
    {
        return GetAnchorPositionInternal(out forwardUnlocked);
    }

    public void GetDeploymentRing(out Vector3 center, out float radius, out bool forwardUnlocked)
    {
        center = GetAnchorPositionInternal(out forwardUnlocked);
        radius = forwardUnlocked ? forwardDeployRadius : baseDeployRadius;
    }

    public bool IsPlacementValid(Vector3 position, out string reason)
    {
        ulong ownerId = ulong.MaxValue;
        var no = GetComponent<NetworkObject>();
        if (no != null)
            ownerId = no.OwnerClientId;

        if (ownerId != ulong.MaxValue && WardTotem.IsBlockedForOwner(ownerId, position))
        {
            reason = "blocked by ward totem";
            return false;
        }

        var anchor = GetAnchorPositionInternal(out bool forwardUnlocked);
        float radius = forwardUnlocked ? forwardDeployRadius : baseDeployRadius;

        if ((position - anchor).sqrMagnitude > radius * radius)
        {
            reason = "outside deploy radius";
            return false;
        }

        if (blockingMask != 0)
        {
            if (Physics.CheckSphere(position, blockingRadius, blockingMask, QueryTriggerInteraction.Ignore))
            {
                reason = "blocked";
                return false;
            }
        }

        reason = null;
        return true;
    }

    private Vector3 GetAnchorPositionInternal(out bool forwardUnlocked)
    {
        if (!_homeSet)
            SetHomePosition(transform.position);

        forwardUnlocked = IsForwardUnlocked(transform.position);
        if (forwardUnlocked)
            return transform.position;

        return homeAnchor != null ? homeAnchor.position : _homePosition;
    }

    private bool IsForwardUnlocked(Vector3 currentPosition)
    {
        if (HasForwardBeacon())
            return true;

        float sign = _homeSign == 0f ? 1f : _homeSign;
        float side = (currentPosition.x - midlineX) * sign;
        return side < 0f;
    }

    private bool HasForwardBeacon()
    {
        if (!allowForwardBeacon)
            return false;

        if (forwardBeaconRecheckSeconds > 0f && Time.time < _nextBeaconCheckTime)
            return _cachedForwardBeacon;

        _nextBeaconCheckTime = Time.time + Mathf.Max(0.05f, forwardBeaconRecheckSeconds);
        _cachedForwardBeacon = false;

        var no = GetComponent<NetworkObject>();
        ulong owner = no != null ? no.OwnerClientId : ulong.MaxValue;
        if (owner == ulong.MaxValue)
            return false;

        var beacons = FindObjectsOfType<ForwardBeacon>(true);
        foreach (var beacon in beacons)
        {
            if (beacon == null) continue;
            if (beacon.OwnerClientId == owner)
            {
                _cachedForwardBeacon = true;
                break;
            }
        }

        return _cachedForwardBeacon;
    }
}
