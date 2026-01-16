using Unity.Netcode;
using UnityEngine;

public class CitadelHealth : NetworkBehaviour
{
    [Header("Citadel")]
    [SerializeField] private int maxHealth = 2000;
    [SerializeField] private int contactDamagePerSecond = 40;
    [SerializeField] private bool contactDamageEnabled = false;

    public NetworkVariable<int> health = new NetworkVariable<int>(
        2000,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> destroyed = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Owner")]
    public NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Tier Visuals (optional)")]
    [SerializeField] private GameObject tier75;
    [SerializeField] private GameObject tier50;
    [SerializeField] private GameObject tier25;

    private bool _ownerAssigned;

    public int MaxHealth => maxHealth;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        health.Value = maxHealth;
        destroyed.Value = false;
        TryAutoAssignOwner();
    }

    private void Start()
    {
        health.OnValueChanged += HandleHealthChanged;
        destroyed.OnValueChanged += HandleDestroyedChanged;
        UpdateTierVisuals();
    }

    private void OnDestroy()
    {
        health.OnValueChanged -= HandleHealthChanged;
        destroyed.OnValueChanged -= HandleDestroyedChanged;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!_ownerAssigned) TryAutoAssignOwner();
    }

    public void ApplyDamageServer(int damage)
    {
        if (!IsServer) return;
        if (destroyed.Value) return;
        if (damage <= 0) return;

        int applied = damage;
        if (MatchManager.Instance != null &&
            MatchManager.Instance.Phase.Value == (int)MatchManager.MatchPhase.Overtime)
        {
            applied = Mathf.CeilToInt(applied * 1.1f);
        }

        health.Value = Mathf.Max(0, health.Value - applied);
        if (health.Value <= 0)
            destroyed.Value = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;
        if (destroyed.Value) return;
        if (!contactDamageEnabled || contactDamagePerSecond <= 0) return;

        if (MatchManager.Instance != null)
        {
            var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
            if (phase != MatchManager.MatchPhase.Playing && phase != MatchManager.MatchPhase.Overtime)
                return;
        }

        var minion = other.GetComponentInParent<MinionAI>();
        if (minion == null) return;

        ApplyDamageServer(Mathf.CeilToInt(contactDamagePerSecond * Time.deltaTime));
    }

    private void TryAutoAssignOwner()
    {
        if (_ownerAssigned) return;
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.TryGetTeamClientIds(out var a, out var b)) return;

        ownerClientId.Value = transform.position.x <= 0f ? a : b;
        _ownerAssigned = true;
    }

    private void HandleHealthChanged(int oldValue, int newValue)
    {
        UpdateTierVisuals();
    }

    private void HandleDestroyedChanged(bool oldValue, bool newValue)
    {
        UpdateTierVisuals();
    }

    private void UpdateTierVisuals()
    {
        if (destroyed.Value)
        {
            SetTierActive(tier75, false);
            SetTierActive(tier50, false);
            SetTierActive(tier25, false);
            return;
        }

        float ratio = maxHealth > 0 ? (float)health.Value / maxHealth : 0f;
        bool show75 = ratio <= 0.75f && ratio > 0.5f;
        bool show50 = ratio <= 0.5f && ratio > 0.25f;
        bool show25 = ratio <= 0.25f;

        SetTierActive(tier75, show75);
        SetTierActive(tier50, show50);
        SetTierActive(tier25, show25);
    }

    private void SetTierActive(GameObject obj, bool active)
    {
        if (obj == null) return;
        obj.SetActive(active);
    }
}
