using Unity.Netcode;
using UnityEngine;

public class MinionZoneDeployment : NetworkBehaviour
{
    [SerializeField] private ZoneField.ZoneType zoneType = ZoneField.ZoneType.Defensive;
    [SerializeField] private float radius = 3f;
    [SerializeField] private float durationSeconds = 5f;
    [SerializeField] private float cooldownSeconds = 12f;
    [SerializeField] private ZoneField zonePrefab;

    private MinionOwner _owner;
    private float _readyTime;

    private void Awake()
    {
        _owner = GetComponent<MinionOwner>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        DeployZone();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (Time.time < _readyTime) return;
        DeployZone();
    }

    private void DeployZone()
    {
        if (zonePrefab == null) return;
        if (_owner == null) _owner = GetComponent<MinionOwner>();
        if (_owner == null) return;

        var zone = Instantiate(zonePrefab, transform.position, Quaternion.identity);
        var no = zone.GetComponent<NetworkObject>();
        if (no != null)
            no.Spawn();

        zone.InitServer(_owner.OwnerClientId, zoneType, radius, durationSeconds);
        _readyTime = Time.time + cooldownSeconds;
    }
}
