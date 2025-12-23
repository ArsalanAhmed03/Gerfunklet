using Unity.Netcode;
using UnityEngine;
using System;
using UnityEngine.UI;
using TMPro;

public class PlayerStatsManager : NetworkBehaviour
{
    [Header("Player Stats Configuration")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int maxStamina = 100;

    [SerializeField] private int startingPoints = 0;

    [SerializeField] private int stamina = 100;

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

    // Events for UI and other systems to subscribe to
    public event Action<int, int> OnHealthChanged; // (newHealth, maxHealth)
    public event Action<int> OnPointsChanged; // (newPoints)
    public event Action OnPlayerDied;
    public event Action OnPlayerRespawned;

    // Public properties for read access
    public int Health => health.Value;
    public int MaxHealth => maxHealth;
    public int Points => points.Value;
    public bool IsAlive => isAlive.Value;


    private float staminaTickTimer = 0f;

    void Update()
    {
        if (!IsOwner || !isAlive.Value) return;
        staminaTickTimer += Time.deltaTime;
        if (staminaTickTimer >= 1f && stamina < maxStamina)
        {
            modifyStamina(2);
            staminaTickTimer = 0f;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            health.Value = maxHealth;
            points.Value = startingPoints;
            isAlive.Value = true;
        }

        if (IsOwner)
        {
            stamina = maxStamina;
            UpdateStaminaUI();
        }

        health.OnValueChanged += OnHealthValueChanged;
        points.OnValueChanged += OnPointsValueChanged;
        isAlive.OnValueChanged += OnAliveStatusChanged;

        if (debugMode)
            Debug.Log($"PlayerStatsManager initialized for {(IsOwner ? "Owner" : "Non-Owner")}");
    }


    public override void OnNetworkDespawn()
    {
        // Unsubscribe from events
        health.OnValueChanged -= OnHealthValueChanged;
        points.OnValueChanged -= OnPointsValueChanged;
        isAlive.OnValueChanged -= OnAliveStatusChanged;
    }

    #region Health Management

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage)
    {
        Debug.Log("TakeDamageServerRpc called on server");

        if (!isAlive.Value) return;
        if (damage <= 0) return;

        var dmg = damage;

        var dr = GetComponent<DamageReceiver>();
        if (dr != null)
            dmg = Mathf.CeilToInt(dmg * dr.DamageMultiplier);

        int newHealth = Mathf.Max(0, health.Value - dmg);
        health.Value = newHealth;
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

    public void modifyStamina(int amount)
    {
        if (amount > 0)
        {
            amount = amount + stamina > maxStamina ? maxStamina - stamina : amount;
        }
        else
        {
            amount = amount + stamina < 0 ? -stamina : amount;
        }
        stamina += amount;
        UpdateStaminaUI();
    }

    public int getStamina()
    {
        return stamina;
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

        // also update owner UI immediately
        UpdateHealthUIClientRpc(health.Value);

        // stamina is local, so tell owner client to reset it
        ResetStaminaOwnerClientRpc();
    }

    [ClientRpc]
    private void ResetStaminaOwnerClientRpc()
    {
        if (!IsOwner) return;
        stamina = maxStamina;

        UpdateStaminaUI();
    }

    private void UpdateStaminaUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.staminaBar == null)
            return;

        GameManager.Instance.staminaBar.value = (float)stamina / maxStamina;
        var staminaText = GameManager.Instance.staminaBar.GetComponentInChildren<TextMeshProUGUI>();
        if (staminaText != null) staminaText.text = $"Stamina: {stamina}";
    }
}
