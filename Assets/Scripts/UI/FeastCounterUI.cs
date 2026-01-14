using Unity.Netcode;
using UnityEngine;
using TMPro;

public class FeastCounterUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private string label = "Feast";

    private FeastRing _ring;

    private void OnEnable()
    {
        BindIfNeeded();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Update()
    {
        if (_ring == null)
            BindIfNeeded();
    }

    private void BindIfNeeded()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient) return;

        var playerObj = nm.SpawnManager.GetLocalPlayerObject();
        if (playerObj == null) return;

        _ring = playerObj.GetComponent<FeastRing>();
        if (_ring != null)
            _ring.storedFood.OnListChanged += HandleFoodChanged;

        Refresh();
    }

    private void Unbind()
    {
        if (_ring != null)
            _ring.storedFood.OnListChanged -= HandleFoodChanged;
        _ring = null;
    }

    private void HandleFoodChanged(NetworkListEvent<int> changeEvent)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (countText == null) return;
        int count = _ring != null ? _ring.storedFood.Count : 0;
        countText.text = $"{label}: {count}";
    }
}
