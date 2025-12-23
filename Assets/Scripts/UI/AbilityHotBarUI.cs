using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class AbilityHotbarUI : MonoBehaviour
{
    [Header("Icons")]
    [SerializeField] private Image[] buttonIcons = new Image[4];
    [SerializeField] private AbilityCatalog iconDb;

    private AbilityRunner _localRunner;
    private AbilityDefinition[] _last = new AbilityDefinition[4];

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
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsClient) return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                NetworkManager.Singleton.LocalClientId,
                out var cc))
            return;

        if (cc.PlayerObject == null) return;

        _localRunner = cc.PlayerObject.GetComponent<AbilityRunner>();
    }

    private AbilityId[] _lastIds = new AbilityId[4];

    private void RefreshIconsIfNeeded()
    {
        if (_localRunner == null) return;

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

            buttonIcons[i].sprite = iconDb.GetIcon(id);
            buttonIcons[i].enabled = true;
        }
    }

}
