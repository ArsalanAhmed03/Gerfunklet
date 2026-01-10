using UnityEngine;
using Unity.Netcode;

public class DeploymentRules : NetworkBehaviour
{
    [Header("Deployment Ring (GDD defaults)")]
    [SerializeField] private float baseDeployRadius = 12f;
    [SerializeField] private float forwardDeployRadius = 18f;
    [SerializeField] private Transform homeAnchor;
    [SerializeField] private float midlineX = 0f;

    private Vector3 _homePosition;
    private bool _homeSet;
    private float _homeSign;

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

    public bool IsPlacementValid(Vector3 position, out string reason)
    {
        var anchor = GetAnchorPositionInternal(out bool forwardUnlocked);
        float radius = forwardUnlocked ? forwardDeployRadius : baseDeployRadius;

        if ((position - anchor).sqrMagnitude > radius * radius)
        {
            reason = "outside deploy radius";
            return false;
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
        float sign = _homeSign == 0f ? 1f : _homeSign;
        float side = (currentPosition.x - midlineX) * sign;
        return side < 0f;
    }
}
