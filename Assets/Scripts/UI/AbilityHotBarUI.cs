using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class AbilityHotbarUI : MonoBehaviour
{
    [Header("Icons")]
    [SerializeField] private Image[] buttonIcons = new Image[4];
    [SerializeField] private AbilityCatalog iconDb;

    private AbilityRunner _localRunner;
    private ulong _boundPlayerObjectId = ulong.MaxValue;
    private bool _forceRefresh;

    private void Awake()
    {
        if (iconDb != null) iconDb.Build();
    }

    private void Update()
    {
        BindIfNeeded();
        RefreshIconsIfNeeded();
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
        _boundPlayerObjectId = objectId;
        _forceRefresh = true;
    }

    private AbilityId[] _lastIds = new AbilityId[4];

    private void RefreshIconsIfNeeded()
    {
        if (_localRunner == null) return;

        if (_forceRefresh)
        {
            for (int i = 0; i < _lastIds.Length; i++)
                _lastIds[i] = (AbilityId)int.MinValue;
            _forceRefresh = false;
        }

        for (int i = 0; i < 4; i++)
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
    }

}
