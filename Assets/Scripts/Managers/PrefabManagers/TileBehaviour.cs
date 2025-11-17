using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(NetworkObject))]
public class TileBehaviour : NetworkBehaviour
{
    [Header("Timing (seconds)")]
    [SerializeField] private float maxCumulativeOccupancy = 15f;
    [SerializeField] private float wobbleStartTime = 8f; // starts wobbling at last 8 seconds
    [SerializeField] private TextMeshProUGUI timeRemainingText;

    [Header("Wobble (Y axis only)")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float baseWobbleAmplitude = 0.05f;
    [SerializeField] private float baseWobbleSpeed = 3f;

    [Header("Top trigger settings")]
    [SerializeField] private string playerTag = "Player";

    [Header("Fall behaviour")]
    [SerializeField] private float fallDistance = 5f;   // how far down the tile falls
    [SerializeField] private float fallDuration = 2f;   // how long the fall takes
    [SerializeField] private float postFallDelay = 0.75f; // extra time at bottom before despawn+teleport

    // Server-authoritative timer
    private NetworkVariable<float> timeRemaining =
        new NetworkVariable<float>(writePerm: NetworkVariableWritePermission.Server);

    private readonly HashSet<ulong> occupants = new HashSet<ulong>();

    private float baseY;
    private NetworkObject cachedNetworkObject;

    public bool IsAlive => cachedNetworkObject != null && cachedNetworkObject.IsSpawned;
    public bool IsFalling { get; private set; }

    // snapshot of players on the tile when fall starts
    private List<ulong> fallOccupantsSnapshot;

    private void Awake()
    {
        cachedNetworkObject = GetComponent<NetworkObject>();

        if (visualRoot == null)
            visualRoot = transform;

        baseY = visualRoot.localPosition.y;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        if (timeRemaining.Value <= 0f)
            timeRemaining.Value = maxCumulativeOccupancy;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        // safety: if timer already 0 but fall hasn't started, start it
        if (timeRemaining.Value <= 0f && !IsFalling)
        {
            BeginFallIfNeeded();
            return;
        }

        if (IsFalling)
            return;

        TickTimer();
        ApplyWobble();
        UpdateTimerText();
    }

    // ------------------ Server timer logic ------------------
    private void TickTimer()
    {
        // Only count down while someone is standing on the tile
        if (occupants.Count == 0)
            return;

        timeRemaining.Value -= Time.deltaTime;

        if (timeRemaining.Value <= 0f)
        {
            timeRemaining.Value = 0f;
            BeginFallIfNeeded();
        }
    }

    // Called by TileGridManager to force this tile to fall, regardless of timer
    public void ForceFall()
    {
        if (!IsServer)
            return;

        if (timeRemaining.Value > 0f)
            timeRemaining.Value = 0f;

        BeginFallIfNeeded();
    }

    private void BeginFallIfNeeded()
    {
        if (!IsServer)
            return;

        if (IsFalling)
            return;

        IsFalling = true;

        // Snapshot who was on the tile at the moment it started to fall
        fallOccupantsSnapshot = new List<ulong>(occupants);

        StartCoroutine(FallAndDespawnRoutine());
    }

    private IEnumerator FallAndDespawnRoutine()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.down * fallDistance;

        float elapsed = 0f;

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        // small extra pause at the bottom before removing tile / rescuing player
        if (postFallDelay > 0f)
            yield return new WaitForSeconds(postFallDelay);

        // despawn tile first, so we only choose from remaining tiles
        if (cachedNetworkObject != null && cachedNetworkObject.IsSpawned)
            cachedNetworkObject.Despawn(true);

        // After tile is gone, move players to a new tile
        RelocateOccupants();
    }

    private void RelocateOccupants()
    {
        if (!IsServer)
            return;

        if (fallOccupantsSnapshot == null || fallOccupantsSnapshot.Count == 0)
            return;

        var grid = TileGridManager.Instance;
        if (grid == null)
            return;

        foreach (var occupantId in fallOccupantsSnapshot)
        {
            // hard clear this player from all tiles to avoid ghost occupancy
            grid.ClearPlayerFromAllTiles(occupantId);

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                    .TryGetValue(occupantId, out var playerNO))
                continue;

            // try to find a safe tile (ignoring this tile, which is already despawned)
            var safeTile = grid.GetRandomSafeTile(null);

            if (safeTile == null)
            {
                // no safe tiles left – you can plug in your "player eliminated" logic here later
                continue;
            }

            Vector3 targetPos = safeTile.transform.position;
            targetPos.y += 1f;

            playerNO.transform.position = targetPos;
        }

        // drop our snapshot so we don't accidentally reuse it
        fallOccupantsSnapshot.Clear();
    }

    // ------------------ Trigger handling (top collider should call these) ------------------
    public void HandleTriggerEnter(Collider other)
    {
        if (!IsServer || other.isTrigger)
            return;

        if (!other.CompareTag(playerTag))
            return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no != null)
            occupants.Add(no.NetworkObjectId);
    }

    public void HandleTriggerExit(Collider other)
    {
        if (!IsServer || other.isTrigger)
            return;

        if (!other.CompareTag(playerTag))
            return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no != null)
            occupants.Remove(no.NetworkObjectId);
    }

    // called by manager to make sure a player is not tracked on this tile anymore
    public void RemoveOccupant(ulong playerId)
    {
        occupants.Remove(playerId);
    }

    // ------------------ Wobble (server-driven, synced via NetworkTransform) ------------------
    private void ApplyWobble()
    {
        if (visualRoot == null)
            return;

        if (timeRemaining.Value <= 0f)
        {
            Vector3 pos = visualRoot.localPosition;
            pos.y = baseY;
            visualRoot.localPosition = pos;
            return;
        }

        if (timeRemaining.Value <= wobbleStartTime)
        {
            float tNorm = 1f - (timeRemaining.Value / wobbleStartTime);
            float currentSpeed = baseWobbleSpeed + (tNorm * 10f);
            float currentAmp = baseWobbleAmplitude + (tNorm * 0.05f);

            double serverTime = NetworkManager.ServerTime.Time;
            float offset = Mathf.Sin((float)serverTime * currentSpeed) * currentAmp;

            Vector3 pos = visualRoot.localPosition;
            pos.y = baseY + offset;
            visualRoot.localPosition = pos;
        }
        else
        {
            Vector3 pos = visualRoot.localPosition;
            pos.y = Mathf.Lerp(pos.y, baseY, 0.2f);
            visualRoot.localPosition = pos;
        }
    }

    // ------------------ UI (server-only) ------------------
    private void UpdateTimerText()
    {
        if (timeRemainingText == null)
            return;

        int seconds = Mathf.CeilToInt(timeRemaining.Value);
        timeRemainingText.text = seconds.ToString();
    }
}
