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

    [Header("Super Choice (Mulligan)")]
    [SerializeField] private GameObject superChoiceRoot;
    [SerializeField] private Button[] superChoiceButtons = new Button[3];
    [SerializeField] private GameObject[] superChoiceHighlights = new GameObject[3];

    [Header("Catalog (optional override)")]
    [SerializeField] private CardCatalog catalogOverride;

    [Header("Placement (optional override)")]
    [SerializeField] private CardPlacementController placementControllerOverride;

    private CardHand _hand;
    private CardCatalog _catalog;
    private CardPlacementController _placement;
    private SuperController _super;
    private MatchManager _match;
    private ulong _boundObjectId = ulong.MaxValue;
    private bool _bound;
    private bool _mulliganMode;
    private readonly bool[] _selected = new bool[4];
    private readonly List<int> _selectionOrder = new List<int>(4);

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

        for (int i = 0; i < superChoiceButtons.Length; i++)
        {
            int idx = i;
            if (superChoiceButtons[i] != null)
                superChoiceButtons[i].onClick.AddListener(() => OnSuperChoiceClicked(idx));
        }
    }

    private void OnEnable()
    {
        BindIfNeeded();
    }

    private void OnDisable()
    {
        Unbind();
        UnbindMatch();
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
        _placement = placementControllerOverride != null ? placementControllerOverride : playerObj.GetComponent<CardPlacementController>();
        _super = playerObj.GetComponent<SuperController>();
        _boundObjectId = objectId;

        if (_hand == null) return;

        _hand.OnHandChanged += RefreshHand;
        _hand.OnMulliganRemainingChanged += HandleMulliganRemainingChanged;
        if (_super != null)
            _super.Choice.OnValueChanged += HandleSuperChoiceChanged;
        _bound = true;
        RefreshHand();
        UpdateMulliganButton();
        BindMatch();
        UpdateMulliganVisibility();
        UpdateSuperChoiceVisuals();
    }

    private void Unbind()
    {
        if (_hand != null)
        {
            _hand.OnHandChanged -= RefreshHand;
            _hand.OnMulliganRemainingChanged -= HandleMulliganRemainingChanged;
        }
        if (_super != null)
            _super.Choice.OnValueChanged -= HandleSuperChoiceChanged;

        _hand = null;
        _catalog = null;
        _placement = null;
        _super = null;
        _bound = false;
        _boundObjectId = ulong.MaxValue;
        SetMulliganMode(false);
    }

    private void BindMatch()
    {
        if (_match != null) return;
        if (MatchManager.Instance == null) return;

        _match = MatchManager.Instance;
        _match.OnPhaseChanged += HandlePhaseChanged;
    }

    private void UnbindMatch()
    {
        if (_match == null) return;
        _match.OnPhaseChanged -= HandlePhaseChanged;
        _match = null;
    }

    private void HandlePhaseChanged(MatchManager.MatchPhase phase)
    {
        UpdateMulliganVisibility();
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
            int remaining = GetMulliganRemaining();
            if (remaining <= 0)
                return;

            if (_selected[index])
            {
                SetSelected(index, false);
            }
            else
            {
                int selectedCount = _selectionOrder.Count;
                if (selectedCount >= remaining && selectedCount > 0)
                {
                    SetSelected(_selectionOrder[0], false);
                }

                SetSelected(index, true);
            }

            UpdateSelectionVisuals();
            return;
        }

        if (!IsPhasePlayable())
            return;

        var id = _hand.GetHandCardId(index);
        if (id == CardId.None)
            return;

        var def = _catalog != null ? _catalog.Get(id) : null;
        if (_placement != null)
        {
            _placement.BeginPlacement(_hand, index, def);
            return;
        }

        _hand.PlayCardServerRpc(index);
    }

    private void OnMulliganButtonClicked()
    {
        if (!IsPhaseLoadoutSelect())
            return;

        if (!_mulliganMode)
        {
            if (GetMulliganRemaining() <= 0)
                return;
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

    private void OnSuperChoiceClicked(int index)
    {
        if (!IsPhaseLoadoutSelect()) return;
        if (_super == null) return;
        if (index < 0 || index > 2) return;

        _super.SetChoiceServerRpc((SuperChoice)index);
    }

    private void SetMulliganMode(bool enabled)
    {
        _mulliganMode = enabled;
        for (int i = 0; i < _selected.Length; i++)
            _selected[i] = false;
        _selectionOrder.Clear();
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
        int remaining = GetMulliganRemaining();
        if (_mulliganMode && remaining <= 0)
            SetMulliganMode(false);

        if (mulliganButtonText != null)
            mulliganButtonText.text = _mulliganMode ? "Confirm Mulligan" : "Mulligan";

        if (mulliganButton != null)
            mulliganButton.interactable = remaining > 0;
    }

    private void UpdateMulliganVisibility()
    {
        bool show = IsPhaseLoadoutSelect();
        if (!show)
            SetMulliganMode(false);

        if (mulliganButton != null)
            mulliganButton.gameObject.SetActive(show);

        if (superChoiceRoot != null)
            superChoiceRoot.SetActive(show);
    }

    private int GetMulliganRemaining()
    {
        return _hand != null ? _hand.MulliganRemaining.Value : 0;
    }

    private void SetSelected(int index, bool selected)
    {
        if (index < 0 || index >= _selected.Length) return;

        _selected[index] = selected;

        if (selected)
        {
            _selectionOrder.Remove(index);
            _selectionOrder.Add(index);
        }
        else
        {
            _selectionOrder.Remove(index);
        }
    }

    private void HandleMulliganRemainingChanged(int remaining)
    {
        ClampSelectionToRemaining(remaining);
        UpdateMulliganButton();
    }

    private void HandleSuperChoiceChanged(SuperChoice oldChoice, SuperChoice newChoice)
    {
        UpdateSuperChoiceVisuals();
    }

    private void UpdateSuperChoiceVisuals()
    {
        if (_super == null) return;
        if (superChoiceHighlights == null || superChoiceHighlights.Length == 0) return;

        int idx = (int)_super.Choice.Value;
        for (int i = 0; i < superChoiceHighlights.Length; i++)
        {
            if (superChoiceHighlights[i] != null)
                superChoiceHighlights[i].SetActive(i == idx);
        }
    }

    private void ClampSelectionToRemaining(int remaining)
    {
        if (remaining < 0) remaining = 0;

        while (_selectionOrder.Count > remaining)
        {
            SetSelected(_selectionOrder[0], false);
        }

        UpdateSelectionVisuals();
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
