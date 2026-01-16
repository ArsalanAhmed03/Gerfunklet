using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class ObjectiveZone : NetworkBehaviour
{
    [Header("Zone Owner (defender)")]
    public NetworkVariable<ulong> ownerClientId =
        new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [Header("Channel Settings")]
    [SerializeField] private float channelSeconds = 3f;
    [SerializeField] private string playerTag = "Player";

    // 0..1 capture progress
    public NetworkVariable<float> progress01 = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // true when both owner and attacker are inside
    public NetworkVariable<bool> contested = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // WHO is currently channeling this zone (needed for player-centric UI)
    public NetworkVariable<ulong> currentAttackerClientId =
        new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [Header("Auto-assign owner (optional)")]
    [SerializeField] private bool autoAssignOwner = true;

    // prevents reassigning every frame
    private bool _ownerAssigned;

    public ulong OwnerClientId => ownerClientId.Value;

    // store ClientIds inside this trigger
    private readonly HashSet<ulong> inside = new HashSet<ulong>();

    private void OnDisable()
    {
        if (IsServer)
            inside.Clear();
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    public void SetOwnerClientId(ulong id)
    {
        if (!IsServer) return;
        ownerClientId.Value = id;
        _ownerAssigned = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRegisterOccupant(other, add: true);
    }

    private void OnTriggerExit(Collider other)
    {
        TryRegisterOccupant(other, add: false);
    }

    private void Update()
    {
        if (!IsServer) return;

        TryAutoAssignOwner();

        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.EnableObjectiveZones)
        {
            ResetChannelServer();
            return;
        }

        int phase = MatchManager.Instance.Phase.Value;
        bool isLive =
            phase == (int)MatchManager.MatchPhase.Playing ||
            phase == (int)MatchManager.MatchPhase.Overtime;

        if (!isLive)
        {
            ResetChannelServer();
            return;
        }

        // if owner not assigned yet, do nothing
        if (ownerClientId.Value == ulong.MaxValue)
        {
            ResetChannelServer();
            return;
        }

        bool ownerInside = inside.Contains(ownerClientId.Value);

        ulong attackerId = GetAttackerClientId();
        bool attackerInside = attackerId != ulong.MaxValue;

        contested.Value = attackerInside && ownerInside;

        // capture only when attacker is inside AND owner is NOT inside
        if (!attackerInside || ownerInside)
        {
            ResetChannelServer();
            return;
        }

        // attacker is actively channeling
        currentAttackerClientId.Value = attackerId;

        float delta = Time.deltaTime / channelSeconds;
        progress01.Value = Mathf.Clamp01(progress01.Value + delta);

        if (progress01.Value >= 1f)
        {
            MatchManager.Instance.ReportCaptureServer(attackerId);
            ResetChannelServer();
        }
    }

    private void ResetChannelServer()
    {
        progress01.Value = 0f;
        contested.Value = false;
        currentAttackerClientId.Value = ulong.MaxValue;
    }

    private void TryAutoAssignOwner()
    {
        if (!IsServer) return;
        if (!autoAssignOwner) return;
        if (_ownerAssigned) return;

        // Need 2 players spawned to assign owners reliably
        if (LocalSpawner.Instance == null) return;
        if (LocalSpawner.Instance.GetSpawnedPlayerCount() < 2) return;

        if (MatchManager.Instance == null) return;

        ulong a = MatchManager.Instance.PlayerAClientId.Value;
        ulong b = MatchManager.Instance.PlayerBClientId.Value;

        if (a == ulong.MaxValue || b == ulong.MaxValue) return;

        // Decide owner by position (left/right)
        ownerClientId.Value = transform.position.x <= 0f ? a : b;

        _ownerAssigned = true;
    }

    private void TryRegisterOccupant(Collider other, bool add)
    {
        if (!IsServer) return;
        if (!other.CompareTag(playerTag)) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null) return;

        if (add)
            inside.Add(no.OwnerClientId);
        else
            inside.Remove(no.OwnerClientId);
    }

    private ulong GetAttackerClientId()
    {
        foreach (var id in inside)
        {
            if (id != ownerClientId.Value)
                return id;
        }

        return ulong.MaxValue;
    }
}
