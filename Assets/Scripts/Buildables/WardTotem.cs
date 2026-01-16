using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class WardTotem : NetworkBehaviour
{
    [SerializeField] private float blockRadius = 3f;

    private BuildableInstance _owner;

    public float BlockRadius => blockRadius;

    private void Awake()
    {
        _owner = GetComponent<BuildableInstance>();
    }

    public bool BlocksOwner(ulong ownerClientId)
    {
        if (_owner == null) return false;
        return _owner.OwnerClientId != ownerClientId;
    }

    public static bool IsBlockedForOwner(ulong ownerClientId, Vector3 position)
    {
        var totems = Object.FindObjectsOfType<WardTotem>(true);
        foreach (var totem in totems)
        {
            if (totem == null) continue;
            if (!totem.BlocksOwner(ownerClientId)) continue;

            float sqr = (totem.transform.position - position).sqrMagnitude;
            if (sqr <= totem.blockRadius * totem.blockRadius)
                return true;
        }

        return false;
    }
}
