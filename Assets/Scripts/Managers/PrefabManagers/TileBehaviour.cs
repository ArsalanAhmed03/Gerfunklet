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
    [SerializeField] private float fallDistance = 5f;
    [SerializeField] private float fallDuration = 2f;
    [SerializeField] private float postFallDelay = 0.75f;

    [Header("Disable visuals/colliders on collapse")]
    [SerializeField] private Collider[] collidersToDisable; // optional; if empty we auto-find
    [SerializeField] private Renderer[] renderersToDisable; // optional; if empty we auto-find

    // Server-authoritative timer (readable by all)
    public NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Tile active state (instead of Despawn)
    public NetworkVariable<bool> isActive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Fall state broadcast: when > 0, everyone animates fall locally using server time
    // 0 means "not falling"
    public NetworkVariable<double> fallStartServerTime = new NetworkVariable<double>(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // occupants stored as NetworkObjectId (NOT OwnerClientId)
    private readonly HashSet<ulong> occupants = new HashSet<ulong>();

    private float baseY;
    private Vector3 initialWorldPos;
    private Quaternion initialWorldRot;

    private bool _localFallingVisual;          // local visual state (client + server)
    private Vector3 _fallStartPos;             // where the fall started (for visuals)
    private List<ulong> fallOccupantsSnapshot; // server only

    public bool IsAlive => isActive.Value; // for grid filtering

    private void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

        baseY = visualRoot.localPosition.y;

        initialWorldPos = transform.position;
        initialWorldRot = transform.rotation;

        // auto-fill if not assigned
        if (collidersToDisable == null || collidersToDisable.Length == 0)
            collidersToDisable = GetComponentsInChildren<Collider>(true);

        if (renderersToDisable == null || renderersToDisable.Length == 0)
            renderersToDisable = GetComponentsInChildren<Renderer>(true);
    }

    public override void OnNetworkSpawn()
    {
        isActive.OnValueChanged += OnActiveChanged;
        fallStartServerTime.OnValueChanged += OnFallStartChanged;

        ApplyActiveState(isActive.Value);

        if (IsServer)
        {
            if (timeRemaining.Value <= 0f)
                timeRemaining.Value = maxCumulativeOccupancy;

            if (!isActive.Value)
                isActive.Value = true;

            // ensure clean state on spawn
            if (fallStartServerTime.Value != 0d)
                fallStartServerTime.Value = 0d;
        }
    }

    public override void OnNetworkDespawn()
    {
        isActive.OnValueChanged -= OnActiveChanged;
        fallStartServerTime.OnValueChanged -= OnFallStartChanged;
    }

    private void Update()
    {
        if (!isActive.Value)
            return;

        // Server controls timer and triggers fall start
        if (IsServer)
        {
            // If already falling, don't tick timer
            if (fallStartServerTime.Value == 0d)
                TickTimer();

            // Safety: if timer is 0 and fall not started yet -> start
            if (timeRemaining.Value <= 0f && fallStartServerTime.Value == 0d)
                BeginFallIfNeeded();
        }

        // Everyone renders wobble + timer text while not falling
        if (!_localFallingVisual)
        {
            ApplyWobbleLocal();
            UpdateTimerTextLocal();
        }

        // Everyone animates fall if it has started (based on server time)
        if (fallStartServerTime.Value != 0d)
        {
            AnimateFallLocal();
        }
    }

    private void TickTimer()
    {
        if (occupants.Count == 0)
            return;

        timeRemaining.Value -= Time.deltaTime;

        if (timeRemaining.Value <= 0f)
            timeRemaining.Value = 0f;
    }

    public void ForceFall()
    {
        if (!IsServer)
            return;

        if (!isActive.Value)
            return;

        if (timeRemaining.Value > 0f)
            timeRemaining.Value = 0f;

        BeginFallIfNeeded();
    }

    private void BeginFallIfNeeded()
    {
        if (!IsServer) return;
        if (!isActive.Value) return;
        if (fallStartServerTime.Value != 0d) return; // already falling

        // Snapshot who was on the tile at the moment it started to fall (server only)
        fallOccupantsSnapshot = new List<ulong>(occupants);

        // broadcast fall start time
        fallStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;

        // server will handle elimination + deactivate at the right time
        StartCoroutine(ServerFallCompleteRoutine());
    }

    private IEnumerator ServerFallCompleteRoutine()
    {
        // wait for fall duration + delay (server uses real time)
        yield return new WaitForSeconds(fallDuration + postFallDelay);

        // eliminate occupants (lose condition) - server only
        EliminateOccupantsServer();

        // mark tile inactive for everyone
        isActive.Value = false;

        // cleanup server-side sets
        occupants.Clear();
        fallOccupantsSnapshot?.Clear();

        // reset fall state (tile is now inactive anyway)
        fallStartServerTime.Value = 0d;
    }

    private void EliminateOccupantsServer()
    {
        if (!IsServer) return;

        if (fallOccupantsSnapshot == null || fallOccupantsSnapshot.Count == 0)
            return;

        foreach (var occupantObjectId in fallOccupantsSnapshot)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(occupantObjectId, out var playerNO))
                continue;

            ulong deadClientId = playerNO.OwnerClientId;

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.NotifyPlayerDied(deadClientId);
                break;
            }
        }
    }

    // Trigger relay calls these (server only)
    public void HandleTriggerEnter(Collider other)
    {
        if (!IsServer || other.isTrigger) return;
        if (!isActive.Value) return;
        if (!other.CompareTag(playerTag)) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no != null)
            occupants.Add(no.NetworkObjectId);
    }

    public void HandleTriggerExit(Collider other)
    {
        if (!IsServer || other.isTrigger) return;
        if (!other.CompareTag(playerTag)) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no != null)
            occupants.Remove(no.NetworkObjectId);
    }

    public void RemoveOccupant(ulong playerObjectId)
    {
        occupants.Remove(playerObjectId);
    }

    // ---- Wobble local (deterministic) ----
    private void ApplyWobbleLocal()
    {
        if (visualRoot == null)
            return;

        if (timeRemaining.Value <= 0f)
        {
            Vector3 pos0 = visualRoot.localPosition;
            pos0.y = baseY;
            visualRoot.localPosition = pos0;
            return;
        }

        if (timeRemaining.Value <= wobbleStartTime)
        {
            float tNorm = 1f - (timeRemaining.Value / wobbleStartTime);
            float currentSpeed = baseWobbleSpeed + (tNorm * 10f);
            float currentAmp = baseWobbleAmplitude + (tNorm * 0.05f);

            double serverTime = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.ServerTime.Time
                : Time.timeAsDouble;

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

    private void UpdateTimerTextLocal()
    {
        if (timeRemainingText == null)
            return;

        int seconds = Mathf.CeilToInt(timeRemaining.Value);
        timeRemainingText.text = seconds.ToString();
    }

    // ---- Active/Inactive visuals ----
    private void OnActiveChanged(bool oldV, bool newV)
    {
        ApplyActiveState(newV);

        // if deactivated mid-fall, make sure local state stops
        if (!newV)
        {
            _localFallingVisual = false;
        }
    }

    private void ApplyActiveState(bool active)
    {
        if (renderersToDisable != null)
        {
            foreach (var r in renderersToDisable)
                if (r != null) r.enabled = active;
        }

        if (collidersToDisable != null)
        {
            foreach (var c in collidersToDisable)
                if (c != null) c.enabled = active;
        }

        if (timeRemainingText != null)
            timeRemainingText.gameObject.SetActive(active);

        if (!active && visualRoot != null)
        {
            var p = visualRoot.localPosition;
            p.y = baseY;
            visualRoot.localPosition = p;
        }

        if (active)
        {
            // snap tile back to initial pos on activation (clients too)
            transform.SetPositionAndRotation(initialWorldPos, initialWorldRot);
        }
    }

    // ---- Fall visuals sync ----
    private void OnFallStartChanged(double oldV, double newV)
    {
        if (newV == 0d)
        {
            _localFallingVisual = false;
            return;
        }

        // fall starts now (from server time)
        _localFallingVisual = true;
        _fallStartPos = transform.position;

        // ensure wobble resets so fall looks clean
        if (visualRoot != null)
        {
            var p = visualRoot.localPosition;
            p.y = baseY;
            visualRoot.localPosition = p;
        }
    }

    private void AnimateFallLocal()
    {
        double start = fallStartServerTime.Value;
        if (start == 0d) return;

        double now = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ServerTime.Time
            : Time.timeAsDouble;

        float elapsed = (float)(now - start);
        float t = Mathf.Clamp01(elapsed / fallDuration);

        Vector3 endPos = _fallStartPos + Vector3.down * fallDistance;
        transform.position = Vector3.Lerp(_fallStartPos, endPos, t);

        // After fall completes locally, we keep it at bottom until isActive turns false
        // (server will flip isActive after postFallDelay)
    }

    // ---- Round reset API (server calls this) ----
    public void ResetTileForNewRoundServer()
    {
        if (!IsServer) return;

        transform.SetPositionAndRotation(initialWorldPos, initialWorldRot);

        timeRemaining.Value = maxCumulativeOccupancy;
        isActive.Value = true;

        occupants.Clear();
        fallOccupantsSnapshot?.Clear();

        // stop fall everywhere
        fallStartServerTime.Value = 0d;
    }
}
