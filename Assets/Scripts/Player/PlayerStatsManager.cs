using Unity.Netcode;
using UnityEngine;
using System;
using UnityEngine.UI;
using TMPro;

public class PlayerStatsManager : NetworkBehaviour
{
    [Header("Player Stats Configuration")]
    [SerializeField] private int maxHealth = 100;
    [Header("Stamina (GDD defaults)")]
    [SerializeField] private float maxStamina = 600f;
    [SerializeField] private float activeDrainPercentPerSec = 0.40f;
    [SerializeField] private float carryExtraDrainPercentPerSec = 0.5f;
    [SerializeField] private float regenOnThronePerSec = 1.3f;
    [SerializeField] private float regenOnGroundPerSec = 0.8f;
    [SerializeField] private float underFirePenalty = 0.4f;
    [SerializeField] private float underFireSeconds = 1.0f;
    [SerializeField] private float manualRestMinPercent = 0.10f;
    [SerializeField] private float autoWakePercent = 0.25f;
    [SerializeField] private float safeEnemyRadius = 2.5f;
    [SerializeField] private float safeEnemySeconds = 1.0f;
    [SerializeField] private float throneRegenRadius = 2.5f;
    [SerializeField] private LayerMask enemyCheckMask;

    [SerializeField] private int startingPoints = 0;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Network Variables - synchronized across all clients
    private NetworkVariable<int> health = new NetworkVariable<int>(
    100,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

    private NetworkVariable<int> points = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isAlive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> stamina = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isSleeping = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Events for UI and other systems to subscribe to
    public event Action<int, int> OnHealthChanged; // (newHealth, maxHealth)
    public event Action<int> OnPointsChanged; // (newPoints)
    public event Action<float, float, bool> OnStaminaChanged; // (current, max, sleeping)
    public event Action OnPlayerDied;
    public event Action OnPlayerRespawned;

    // Public properties for read access
    public int Health => health.Value;
    public int MaxHealth => maxHealth;
    public int Points => points.Value;
    public bool IsAlive => isAlive.Value;
    public float Stamina => stamina.Value;
    public float MaxStamina => maxStamina;
    public bool IsSleeping => isSleeping.Value;
    private float _lastDamageTime;
    private float _safeSince;
    private bool _forcedWakeUsed;
    private float _nextThroneCheckTime;
    private bool _cachedOnThrone;

    void Update()
    {
        if (!IsServer) return;
        if (!isAlive.Value) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (isSleeping.Value)
        {
            RegenStaminaServer(dt);
            TryAutoWakeServer();
        }
        else
        {
            DrainStaminaServer(dt);
            if (stamina.Value <= 0f)
                EnterSleepServer();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            health.Value = maxHealth;
            points.Value = startingPoints;
            isAlive.Value = true;
            stamina.Value = maxStamina;
            isSleeping.Value = false;
            _lastDamageTime = -999f;
            _safeSince = 0f;
            _forcedWakeUsed = false;
        }

        if (IsOwner)
        {
            UpdateStaminaUI();
        }

        health.OnValueChanged += OnHealthValueChanged;
        points.OnValueChanged += OnPointsValueChanged;
        isAlive.OnValueChanged += OnAliveStatusChanged;
        stamina.OnValueChanged += OnStaminaValueChanged;
        isSleeping.OnValueChanged += OnSleepingChanged;

        if (debugMode)
            Debug.Log($"PlayerStatsManager initialized for {(IsOwner ? "Owner" : "Non-Owner")}");
    }


    public override void OnNetworkDespawn()
    {
        // Unsubscribe from events
        health.OnValueChanged -= OnHealthValueChanged;
        points.OnValueChanged -= OnPointsValueChanged;
        isAlive.OnValueChanged -= OnAliveStatusChanged;
        stamina.OnValueChanged -= OnStaminaValueChanged;
        isSleeping.OnValueChanged -= OnSleepingChanged;
    }

    #region Health Management

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage)
    {
        Debug.Log("TakeDamageServerRpc called on server");

        if (!isAlive.Value) return;
        if (isSleeping.Value) return;
        if (damage <= 0) return;

        var dmg = damage;

        var dr = GetComponent<DamageReceiver>();
        if (dr != null)
            dmg = Mathf.CeilToInt(dmg * dr.DamageMultiplier);

        int newHealth = Mathf.Max(0, health.Value - dmg);
        health.Value = newHealth;
        _lastDamageTime = Time.time;
        UpdateHealthUIClientRpc(newHealth);
        if (debugMode)
            Debug.Log($"[SERVER] Player took {dmg} damage. Health: {newHealth}/{maxHealth}");

        if (newHealth <= 0)
        {
            Die();
            if (debugMode) Debug.Log("[SERVER] Player died!");
        }
    }

    [ClientRpc(RequireOwnership = false)]
    private void UpdateHealthUIClientRpc(int newHealth)
    {
        if (!IsOwner) return;
        if (GameManager.Instance == null || GameManager.Instance.healthBar == null) return;

        Debug.Log("Updating Health UI via ClientRpc");
        GameManager.Instance.healthBar.value = (float)newHealth / maxHealth;
        TextMeshProUGUI healthText = GameManager.Instance.healthBar.GetComponentInChildren<TextMeshProUGUI>();
        if (healthText != null)
        {
            healthText.text = newHealth.ToString();
        }
    }

    public void Heal(int healAmount)
    {
        if (!IsOwner && !IsServer) return;
        if (!isAlive.Value) return;

        if (IsServer)
            ApplyHealServer(healAmount);
        else
            HealServerRpc(healAmount);
    }

    public void SetHealth(int newHealth)
    {
        if (!IsOwner && !IsServer) return;

        if (IsServer)
            ApplySetHealthServer(newHealth);
        else
            SetHealthServerRpc(newHealth);
    }

    private void Die()
    {
        if (!IsServer) return;
        isAlive.Value = false;

        if (debugMode)
            Debug.Log("Player died!");
    }

    public void Respawn()
    {
        if (!IsOwner && !IsServer) return;

        if (IsServer)
            ApplyRespawnServer();
        else
            RespawnServerRpc();
    }

    #endregion

    #region Points Management

    public void AddPoints(int pointsToAdd)
    {
        if (!IsOwner && !IsServer) return;

        if (IsServer)
            ApplyAddPointsServer(pointsToAdd);
        else
            AddPointsServerRpc(pointsToAdd);
    }

    public void RemovePoints(int pointsToRemove)
    {
        if (!IsOwner && !IsServer) return;

        pointsToRemove = Mathf.Max(0, pointsToRemove);
        if (IsServer)
            ApplyAddPointsServer(-pointsToRemove);
        else
            RemovePointsServerRpc(pointsToRemove);
    }

    public void SetPoints(int newPoints)
    {
        if (!IsOwner && !IsServer) return;

        if (IsServer)
            ApplySetPointsServer(newPoints);
        else
            SetPointsServerRpc(newPoints);
    }

    [ServerRpc]
    private void HealServerRpc(int healAmount)
    {
        ApplyHealServer(healAmount);
    }

    private void ApplyHealServer(int healAmount)
    {
        if (!IsServer) return;
        if (!isAlive.Value) return;
        if (healAmount <= 0) return;

        int newHealth = Mathf.Min(maxHealth, health.Value + healAmount);
        health.Value = newHealth;
        UpdateHealthUIClientRpc(newHealth);

        if (debugMode)
            Debug.Log($"Player healed {healAmount}. Health: {newHealth}/{maxHealth}");
    }

    [ServerRpc]
    private void SetHealthServerRpc(int newHealth)
    {
        ApplySetHealthServer(newHealth);
    }

    private void ApplySetHealthServer(int newHealth)
    {
        if (!IsServer) return;

        int clamped = Mathf.Clamp(newHealth, 0, maxHealth);
        health.Value = clamped;
        UpdateHealthUIClientRpc(clamped);
    }

    [ServerRpc]
    private void RespawnServerRpc()
    {
        ApplyRespawnServer();
    }

    private void ApplyRespawnServer()
    {
        if (!IsServer) return;

        health.Value = maxHealth;
        isAlive.Value = true;
        stamina.Value = maxStamina;
        isSleeping.Value = false;
        _lastDamageTime = -999f;
        _safeSince = 0f;
        _forcedWakeUsed = false;
        UpdateHealthUIClientRpc(health.Value);

        if (debugMode)
            Debug.Log("Player respawned!");
    }

    [ServerRpc]
    private void AddPointsServerRpc(int pointsToAdd)
    {
        pointsToAdd = Mathf.Max(0, pointsToAdd);
        ApplyAddPointsServer(pointsToAdd);
    }

    [ServerRpc]
    private void RemovePointsServerRpc(int pointsToRemove)
    {
        pointsToRemove = Mathf.Max(0, pointsToRemove);
        ApplyAddPointsServer(-pointsToRemove);
    }

    [ServerRpc]
    private void SetPointsServerRpc(int newPoints)
    {
        ApplySetPointsServer(newPoints);
    }

    private void ApplyAddPointsServer(int delta)
    {
        if (!IsServer) return;

        int newValue = Mathf.Max(0, points.Value + delta);
        points.Value = newValue;

        if (debugMode)
        {
            if (delta >= 0)
                Debug.Log($"Added {delta} points. Total: {points.Value}");
            else
                Debug.Log($"Removed {Mathf.Abs(delta)} points. Total: {points.Value}");
        }
    }

    private void ApplySetPointsServer(int newPoints)
    {
        if (!IsServer) return;
        points.Value = Mathf.Max(0, newPoints);
    }

    public bool CanSpendPoints(int cost)
    {
        return points.Value >= cost;
    }

    public bool TrySpendPoints(int cost)
    {
        if (!IsOwner || !CanSpendPoints(cost)) return false;

        RemovePoints(cost);
        return true;
    }

    [ServerRpc]
    public void RequestSleepServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (isSleeping.Value) return;

        float minRequired = maxStamina * manualRestMinPercent;
        if (stamina.Value < minRequired) return;

        EnterSleepServer();
    }

    [ServerRpc]
    public void RequestWakeServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!isSleeping.Value) return;

        float required = maxStamina * autoWakePercent;
        if (stamina.Value < required) return;
        if (_forcedWakeUsed) return;

        _forcedWakeUsed = true;
        WakeUpServer();
    }

    #endregion

    #region Stamina (GDD)

    private void DrainStaminaServer(float dt)
    {
        float drainPercent = activeDrainPercentPerSec;
        if (IsCarrying())
            drainPercent += carryExtraDrainPercentPerSec;

        float drainPerSec = (drainPercent / 100f) * maxStamina;
        stamina.Value = Mathf.Max(0f, stamina.Value - drainPerSec * dt);
    }

    private void RegenStaminaServer(float dt)
    {
        float regen = IsOnOwnThrone() ? regenOnThronePerSec : regenOnGroundPerSec;
        if (Time.time - _lastDamageTime <= underFireSeconds)
            regen *= (1f - underFirePenalty);

        stamina.Value = Mathf.Min(maxStamina, stamina.Value + regen * dt);
    }

    private void EnterSleepServer()
    {
        if (!IsServer) return;
        if (isSleeping.Value) return;

        isSleeping.Value = true;
        _safeSince = 0f;
        _forcedWakeUsed = false;
    }

    private void WakeUpServer()
    {
        if (!IsServer) return;
        if (!isSleeping.Value) return;

        isSleeping.Value = false;
        _safeSince = 0f;
    }

    private void TryAutoWakeServer()
    {
        float required = maxStamina * autoWakePercent;
        if (stamina.Value < required)
        {
            _safeSince = 0f;
            return;
        }

        if (IsEnemyNearby())
        {
            _safeSince = 0f;
            return;
        }

        if (_safeSince <= 0f)
            _safeSince = Time.time;

        if (Time.time - _safeSince >= safeEnemySeconds)
            WakeUpServer();
    }

    private bool IsCarrying()
    {
        var carrier = GetComponent<MillstoneCarrier>();
        return carrier != null && carrier.IsCarrying.Value;
    }

    private bool IsOnOwnThrone()
    {
        if (Time.time < _nextThroneCheckTime)
            return _cachedOnThrone;

        _nextThroneCheckTime = Time.time + 0.25f;
        _cachedOnThrone = false;

        var thrones = FindObjectsOfType<ThroneCapture>(true);
        foreach (var t in thrones)
        {
            if (t == null) continue;
            if (t.ownerClientId.Value != OwnerClientId) continue;

            float sqr = (t.transform.position - transform.position).sqrMagnitude;
            if (sqr <= throneRegenRadius * throneRegenRadius)
            {
                _cachedOnThrone = true;
                break;
            }
        }

        return _cachedOnThrone;
    }

    private bool IsEnemyNearby()
    {
        int mask = enemyCheckMask.value != 0 ? enemyCheckMask.value : LayerMask.GetMask("Player");
        var hits = Physics.OverlapSphere(transform.position, safeEnemyRadius, mask, QueryTriggerInteraction.Ignore);
        foreach (var col in hits)
        {
            var no = col.GetComponentInParent<NetworkObject>();
            if (no == null) continue;
            if (no.OwnerClientId == OwnerClientId) continue;
            return true;
        }

        return false;
    }

    private void UpdateStaminaUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.staminaBar == null)
            return;

        var bar = GameManager.Instance.staminaBar;
        bar.value = maxStamina <= 0f ? 0f : stamina.Value / maxStamina;

        var staminaText = bar.GetComponentInChildren<TextMeshProUGUI>();
        if (staminaText != null)
        {
            string state = isSleeping.Value ? "SLEEPING" : "STAMINA";
            staminaText.text = $"{state}: {Mathf.CeilToInt(stamina.Value)}";
        }
    }

    #endregion

    #region Network Variable Callbacks

    private void OnHealthValueChanged(int oldHealth, int newHealth)
    {
        if (!IsOwner) return;
        OnHealthChanged?.Invoke(newHealth, maxHealth);

        if (debugMode)
            Debug.Log($"Health changed: {oldHealth} -> {newHealth}");
    }

    private void OnPointsValueChanged(int oldPoints, int newPoints)
    {
        OnPointsChanged?.Invoke(newPoints);

        if (debugMode)
            Debug.Log($"Points changed: {oldPoints} -> {newPoints}");
    }

    private void OnStaminaValueChanged(float oldValue, float newValue)
    {
        if (!IsOwner) return;
        OnStaminaChanged?.Invoke(newValue, maxStamina, isSleeping.Value);
        UpdateStaminaUI();
    }

    private void OnSleepingChanged(bool oldValue, bool newValue)
    {
        if (!IsOwner) return;
        OnStaminaChanged?.Invoke(stamina.Value, maxStamina, newValue);
        UpdateStaminaUI();
    }

    private void OnAliveStatusChanged(bool wasAlive, bool nowAlive)
    {
        if (!wasAlive && nowAlive)
        {
            OnPlayerRespawned?.Invoke();
        }
        else if (wasAlive && !nowAlive)
        {
            OnPlayerDied?.Invoke();
        }
    }

    #endregion

    #region Debug Methods (Context Menu for testing)

    [ContextMenu("Take 10 Damage")]
    private void TakeDamageTest()
    {
        TakeDamageServerRpc(10);
    }

    [ContextMenu("Heal 20")]
    private void HealTest()
    {
        Heal(20);
    }

    [ContextMenu("Add 50 Points")]
    private void AddPointsTest()
    {
        AddPoints(50);
    }

    [ContextMenu("Kill Player")]
    private void KillTest()
    {
        TakeDamageServerRpc(health.Value);
    }

    [ContextMenu("Respawn Player")]
    private void RespawnTest()
    {
        Respawn();
    }

    #endregion

    public bool IsOwnedByLocalPlayer()
    {
        if (IsOwner)
        {
            Debug.Log("This PlayerStatsManager is owned by the local player.");
            return true;
        }
        else
        {
            Debug.Log("This PlayerStatsManager is NOT owned by the local player.");
            return false;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetForNewRoundServerRpc()
    {
        if (!IsServer) return;

        health.Value = maxHealth;
        points.Value = startingPoints;
        isAlive.Value = true;
        stamina.Value = maxStamina;
        isSleeping.Value = false;
        _lastDamageTime = -999f;
        _safeSince = 0f;
        _forcedWakeUsed = false;

        // also update owner UI immediately
        UpdateHealthUIClientRpc(health.Value);
    }
}
