using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardHandUI : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private Image[] slotIcons = new Image[4];
    [SerializeField] private Button[] slotButtons = new Button[4];
    [SerializeField] private GameObject[] mulliganHighlights = new GameObject[4];

    [Header("Mulligan")]
    [SerializeField] private Button mulliganButton;
    [SerializeField] private TextMeshProUGUI mulliganButtonText;

    [Header("Catalog (optional override)")]
    [SerializeField] private CardCatalog catalogOverride;

    private CardHand _hand;
    private CardCatalog _catalog;
    private ulong _boundObjectId = ulong.MaxValue;
    private bool _bound;
    private bool _mulliganMode;
    private readonly bool[] _selected = new bool[4];

    private void Awake()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int idx = i;
            if (slotButtons[i] != null)
                slotButtons[i].onClick.AddListener(() => OnSlotClicked(idx));
        }

        if (mulliganButton != null)
            mulliganButton.onClick.AddListener(OnMulliganButtonClicked);
    }

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

        _hand = playerObj.GetComponent<CardHand>();
        _catalog = catalogOverride != null ? catalogOverride : _hand != null ? _hand.Catalog : null;
        _boundObjectId = objectId;

        if (_hand == null) return;

        _hand.OnHandChanged += RefreshHand;
        _bound = true;
        RefreshHand();
        UpdateMulliganButton();
    }

    private void Unbind()
    {
        if (_hand != null)
            _hand.OnHandChanged -= RefreshHand;

        _hand = null;
        _catalog = null;
        _bound = false;
        _boundObjectId = ulong.MaxValue;
        SetMulliganMode(false);
    }

    private void RefreshHand()
    {
        if (_hand == null) return;

        for (int i = 0; i < slotIcons.Length; i++)
        {
            var id = _hand.GetHandCardId(i);
            var icon = _catalog != null ? _catalog.Get(id)?.icon : null;

            if (slotIcons[i] != null)
            {
                if (id == CardId.None || icon == null)
                {
                    slotIcons[i].enabled = false;
                    slotIcons[i].sprite = null;
                }
                else
                {
                    slotIcons[i].enabled = true;
                    slotIcons[i].sprite = icon;
                }
            }

            if (slotButtons[i] != null)
                slotButtons[i].interactable = id != CardId.None;
        }

        UpdateSelectionVisuals();
    }

    private void OnSlotClicked(int index)
    {
        if (_hand == null) return;

        if (_mulliganMode)
        {
            _selected[index] = !_selected[index];
            UpdateSelectionVisuals();
            return;
        }

        if (!IsPhasePlayable())
            return;

        _hand.PlayCardServerRpc(index);
    }

    private void OnMulliganButtonClicked()
    {
        if (!IsPhaseLoadoutSelect())
            return;

        if (!_mulliganMode)
        {
            SetMulliganMode(true);
            return;
        }

        List<int> indices = new List<int>();
        for (int i = 0; i < _selected.Length; i++)
            if (_selected[i]) indices.Add(i);

        if (indices.Count > 0 && _hand != null)
            _hand.MulliganServerRpc(indices.ToArray());

        SetMulliganMode(false);
    }

    private void SetMulliganMode(bool enabled)
    {
        _mulliganMode = enabled;
        for (int i = 0; i < _selected.Length; i++)
            _selected[i] = false;
        UpdateSelectionVisuals();
        UpdateMulliganButton();
    }

    private void UpdateSelectionVisuals()
    {
        if (mulliganHighlights == null) return;
        for (int i = 0; i < mulliganHighlights.Length; i++)
        {
            if (mulliganHighlights[i] != null)
                mulliganHighlights[i].SetActive(_mulliganMode && _selected[i]);
        }
    }

    private void UpdateMulliganButton()
    {
        if (mulliganButtonText != null)
            mulliganButtonText.text = _mulliganMode ? "Confirm Mulligan" : "Mulligan";
    }

    private bool IsPhaseLoadoutSelect()
    {
        if (MatchManager.Instance == null) return false;
        return (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value == MatchManager.MatchPhase.LoadoutSelect;
    }

    private bool IsPhasePlayable()
    {
        if (MatchManager.Instance == null) return false;
        var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
        return phase == MatchManager.MatchPhase.Playing || phase == MatchManager.MatchPhase.Overtime;
    }
}
