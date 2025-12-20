using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class ObjectiveZone : NetworkBehaviour
{
    [Header("Zone Owner (defender)")]
    [SerializeField] private ulong ownerClientId;

    [Header("Channel Settings")]
    [SerializeField] private float channelSeconds = 3f;
    [SerializeField] private string playerTag = "Player";

    public NetworkVariable<float> progress01 = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> contested = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Auto-assign owner (optional)")]
    [SerializeField] private bool autoAssignOwner = true;

    // prevents reassigning every frame
    private bool _ownerAssigned;

    private readonly HashSet<ulong> inside = new HashSet<ulong>();

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    public void SetOwnerClientId(ulong id)
    {
        if (!IsServer) return;
        ownerClientId = id;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag(playerTag)) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null) return;

        inside.Add(no.OwnerClientId);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag(playerTag)) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null) return;

        inside.Remove(no.OwnerClientId);
    }

    private void Update()
    {
        if (!IsServer) return;

        TryAutoAssignOwner();

        if (MatchManager.Instance == null) return;
        int phase = MatchManager.Instance.Phase.Value;
        bool isLive =
            phase == (int)MatchManager.MatchPhase.Playing ||
            phase == (int)MatchManager.MatchPhase.Overtime;

        if (!isLive)
        {
            ResetChannelServer();
            return;
        }

        bool ownerInside = inside.Contains(ownerClientId);

        ulong attackerId = ulong.MaxValue;
        foreach (var id in inside)
        {
            if (id != ownerClientId) { attackerId = id; break; }
        }
        bool attackerInside = attackerId != ulong.MaxValue;

        contested.Value = attackerInside && ownerInside;

        if (!attackerInside || ownerInside)
        {
            ResetChannelServer();
            return;
        }

        float delta = Time.deltaTime / channelSeconds;
        progress01.Value = Mathf.Clamp01(progress01.Value + delta);

        if (progress01.Value >= 1f)
        {
            MatchManager.Instance.EndMatchServer(attackerId);
            ResetChannelServer();
        }
    }

    private void ResetChannelServer()
    {
        progress01.Value = 0f;
        contested.Value = false;
    }

    private void TryAutoAssignOwner()
    {
        if (!IsServer) return;
        if (!autoAssignOwner) return;
        if (_ownerAssigned) return;

        // Need 2 players spawned to assign owners reliably
        if (LocalSpawner.Instance == null) return;
        if (LocalSpawner.Instance.GetSpawnedPlayerCount() < 2) return;

        // We assume 2 throne zones exist in the scene.
        // Rule:
        // - Zone with smaller X becomes Player 0's defended zone, other becomes Player 1's defended zone
        // This is deterministic and doesn't depend on client IDs order.
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients.Count < 2) return;

        ulong a = clients[0].ClientId;
        ulong b = clients[1].ClientId;

        // Decide owner by position (left/right)
        ownerClientId = transform.position.x <= 0f ? a : b;

        _ownerAssigned = true;
    }

}
