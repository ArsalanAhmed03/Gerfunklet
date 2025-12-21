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
    [SerializeField] private Collider[] collidersToDisable;     // optional; if empty we auto-find
    [SerializeField] private Renderer[] renderersToDisable;     // optional; if empty we auto-find

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

    // occupants stored as NetworkObjectId (NOT OwnerClientId)
    private readonly HashSet<ulong> occupants = new HashSet<ulong>();

    private float baseY;
    private Vector3 initialWorldPos;
    private Quaternion initialWorldRot;

    public bool IsAlive => isActive.Value; // for grid filtering
    public bool IsFalling { get; private set; }

    private List<ulong> fallOccupantsSnapshot;

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

        // apply current state on spawn (client + server)
        ApplyActiveState(isActive.Value);

        if (IsServer)
        {
            if (timeRemaining.Value <= 0f)
                timeRemaining.Value = maxCumulativeOccupancy;

            if (!isActive.Value)
                isActive.Value = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        isActive.OnValueChanged -= OnActiveChanged;
    }

    private void Update()
    {
        // if inactive, do nothing (no wobble/text)
        if (!isActive.Value)
            return;

        // Server controls timer + fall
        if (IsServer)
        {
            if (timeRemaining.Value <= 0f && !IsFalling)
                BeginFallIfNeeded();

            if (!IsFalling)
                TickTimer();
        }

        if (IsFalling)
            return;

        ApplyWobbleLocal();
        UpdateTimerTextLocal();
    }

    private void TickTimer()
    {
        if (occupants.Count == 0)
            return;

        timeRemaining.Value -= Time.deltaTime;

        if (timeRemaining.Value <= 0f)
        {
            timeRemaining.Value = 0f;
            BeginFallIfNeeded();
        }
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
        if (IsFalling) return;
        if (!isActive.Value) return;

        IsFalling = true;
        fallOccupantsSnapshot = new List<ulong>(occupants);

        StartCoroutine(FallAndDisableRoutine());
    }

    private IEnumerator FallAndDisableRoutine()
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

        if (postFallDelay > 0f)
            yield return new WaitForSeconds(postFallDelay);

        // eliminate occupants (lose condition)
        EliminateOccupants();

        // mark tile inactive for everyone (no despawn)
        isActive.Value = false;

        // server-side cleanup flags
        IsFalling = false;
        occupants.Clear();
        fallOccupantsSnapshot?.Clear();
    }

    private void EliminateOccupants()
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
    }

    private void ApplyActiveState(bool active)
    {
        // hide/show renderers
        if (renderersToDisable != null)
        {
            foreach (var r in renderersToDisable)
                if (r != null) r.enabled = active;
        }

        // enable/disable colliders (avoid standing on dead tiles)
        if (collidersToDisable != null)
        {
            foreach (var c in collidersToDisable)
                if (c != null) c.enabled = active;
        }

        // hide countdown text when inactive
        if (timeRemainingText != null)
            timeRemainingText.gameObject.SetActive(active);

        // snap wobble back to baseline when inactive
        if (!active && visualRoot != null)
        {
            var p = visualRoot.localPosition;
            p.y = baseY;
            visualRoot.localPosition = p;
        }
    }

    // ---- Round reset API (server calls this) ----
    public void ResetTileForNewRoundServer()
    {
        if (!IsServer) return;

        transform.SetPositionAndRotation(initialWorldPos, initialWorldRot);

        timeRemaining.Value = maxCumulativeOccupancy;
        isActive.Value = true;

        IsFalling = false;
        occupants.Clear();
        fallOccupantsSnapshot?.Clear();
    }
}
