using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AtpUI : MonoBehaviour
{
    [Header("ATP UI")]
    [SerializeField] private Slider atpBar;
    [SerializeField] private TextMeshProUGUI atpText;

    private AtpResource _atp;
    private ulong _boundObjectId = ulong.MaxValue;
    private bool _bound;

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
        if (!_bound)
            BindIfNeeded();
    }

    private void BindIfNeeded()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient) return;

        var playerObj = nm.SpawnManager.GetLocalPlayerObject();
        if (playerObj == null) return;

        var no = playerObj.GetComponent<NetworkObject>();
        ulong objectId = no != null ? no.NetworkObjectId : ulong.MaxValue;

        if (_bound && _boundObjectId == objectId) return;

        Unbind();

        _atp = playerObj.GetComponent<AtpResource>();
        _boundObjectId = objectId;
        if (_atp == null) return;

        _atp.OnAtpChanged += HandleAtpChanged;
        _bound = true;
        HandleAtpChanged(_atp.CurrentAtp);
    }

    private void Unbind()
    {
        if (_atp != null)
            _atp.OnAtpChanged -= HandleAtpChanged;

        _atp = null;
        _bound = false;
        _boundObjectId = ulong.MaxValue;
    }

    private void HandleAtpChanged(float value)
    {
        float cap = _atp != null ? _atp.AtpCap : value;

        if (atpBar != null)
        {
            atpBar.maxValue = cap;
            atpBar.value = value;
        }

        if (atpText != null)
            atpText.text = $"{Mathf.CeilToInt(value)} / {Mathf.CeilToInt(cap)}";
    }
}
