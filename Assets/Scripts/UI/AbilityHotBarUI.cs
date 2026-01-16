using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class AbilityHotbarUI : MonoBehaviour
{
    [Header("Icons")]
    [SerializeField] private Image[] buttonIcons = new Image[5];
    [SerializeField] private AbilityCatalog iconDb;
    [SerializeField] private Color enabledTint = Color.white;
    [SerializeField] private Color disabledTint = new Color(1f, 1f, 1f, 0.35f);

    private AbilityRunner _localRunner;
    private PlayerStatsManager _localStats;
    private ulong _boundPlayerObjectId = ulong.MaxValue;
    private bool _forceRefresh;
    private bool _lastSleeping;

    private void Awake()
    {
        if (iconDb != null) iconDb.Build();
    }

    private void Update()
    {
        BindIfNeeded();
        RefreshIconsIfNeeded();
        RefreshSleepTint();
    }

    private void BindIfNeeded()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient) return;

        var playerObj = nm.SpawnManager.GetLocalPlayerObject();
        if (playerObj == null) return;

        var no = playerObj.GetComponent<NetworkObject>();
        ulong objectId = no != null ? no.NetworkObjectId : ulong.MaxValue;

        if (_localRunner != null && _boundPlayerObjectId == objectId) return;

        _localRunner = playerObj.GetComponent<AbilityRunner>();
        _localStats = playerObj.GetComponent<PlayerStatsManager>();
        _boundPlayerObjectId = objectId;
        _forceRefresh = true;
    }

    private AbilityId[] _lastIds = new AbilityId[5];

    private void RefreshIconsIfNeeded()
    {
        if (_localRunner == null) return;

        if (_forceRefresh)
        {
            for (int i = 0; i < _lastIds.Length; i++)
                _lastIds[i] = (AbilityId)int.MinValue;
            _forceRefresh = false;
        }

        int count = Mathf.Min(buttonIcons.Length, _lastIds.Length);
        for (int i = 0; i < count; i++)
        {
            var id = _localRunner.GetSlotId(i);
            if (_lastIds[i].Equals(id)) continue;

            _lastIds[i] = id;

            if (buttonIcons[i] == null) continue;

            if (id == AbilityId.None)
            {
                buttonIcons[i].enabled = false;
                continue;
            }

            if (iconDb == null)
            {
                buttonIcons[i].enabled = false;
                continue;
            }

            buttonIcons[i].sprite = iconDb.GetIcon(id);
            buttonIcons[i].enabled = true;
        }

        RefreshSleepTint();
    }

    private void RefreshSleepTint()
    {
        bool sleeping = _localStats != null && _localStats.IsSleeping;
        if (sleeping == _lastSleeping) return;
        _lastSleeping = sleeping;

        Color tint = sleeping ? disabledTint : enabledTint;
        for (int i = 0; i < buttonIcons.Length; i++)
        {
            if (buttonIcons[i] != null && buttonIcons[i].enabled)
                buttonIcons[i].color = tint;
        }
    }
}
