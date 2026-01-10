using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class LoadoutSelectUI : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private GameObject root;

    [Header("Cards (4 slots)")]
    [SerializeField] private LoadoutCardUI[] cards = new LoadoutCardUI[4];

    [Header("Popup")]
    [SerializeField] private AbilityPickerPopupUI pickerPopup;
    [SerializeField] private AbilityCatalog iconDb;

    [Header("Submit")]
    [SerializeField] private Button lockInButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Countdown UI")]
    [SerializeField] private Slider countdownSlider;

    [Header("Rules")]
    [SerializeField] private float loadoutSelectSeconds = 20f;

    private AbilityId?[] _selected = new AbilityId?[4];
    private float _loadoutEndsAtLocal;
    private bool _submitted;

    private void Awake()
    {
        if (iconDb != null) iconDb.Build();

        for (int i = 0; i < cards.Length; i++)
        {
            int idx = i;
            if (cards[i] != null)
                cards[i].Init(() => OnCardClicked(idx));
        }

        if (lockInButton != null)
            lockInButton.onClick.AddListener(SubmitIfReady);

        RefreshCards();
    }

    private void Update()
    {
        if (MatchManager.Instance == null) return;

        var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
        bool show = phase == MatchManager.MatchPhase.LoadoutSelect &&
                    MatchManager.Instance.EnableAbilityLoadoutUI;

        if (root != null) root.SetActive(show);

        if (!show)
        {
            if (pickerPopup != null) pickerPopup.Hide();
            _loadoutEndsAtLocal = 0f;
            return;
        }

        bool locked = MatchManager.Instance.LoadoutsLocked.Value;

        if (locked)
        {
            if (pickerPopup != null) pickerPopup.Hide();
            if (statusText != null) statusText.text = "Locked. Starting...";
            if (lockInButton != null) lockInButton.interactable = false;
            UpdateCountdownVisual(1f);
            return;
        }

        float remaining = GetRemainingLoadoutSeconds();
        float duration = GetLoadoutDurationSeconds();
        float t01 = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.01f, duration));
        UpdateCountdownVisual(t01);

        if (statusText != null)
            statusText.text = _submitted ? "Locked in. Waiting for opponent..." : "Pick 4 abilities and Lock In.";

        if (lockInButton != null)
            lockInButton.interactable = !_submitted && IsAllSlotsFilledNoDuplicates();

        if (!_submitted && remaining <= 0f)
        {
            AutoFillMissingSlots();
            SubmitIfReady();
        }
    }

    private float GetRemainingLoadoutSeconds()
    {
        if (MatchManager.Instance == null) return 0f;

        double endServerTime = MatchManager.Instance.LoadoutEndsAtServerTime.Value;
        if (endServerTime > 0d && NetworkManager.Singleton != null)
        {
            double now = NetworkManager.Singleton.ServerTime.Time;
            return Mathf.Max(0f, (float)(endServerTime - now));
        }

        if (_loadoutEndsAtLocal <= 0f)
            _loadoutEndsAtLocal = Time.time + GetLoadoutDurationSeconds();

        return Mathf.Max(0f, _loadoutEndsAtLocal - Time.time);
    }

    private float GetLoadoutDurationSeconds()
    {
        if (MatchManager.Instance != null)
            return MatchManager.Instance.LoadoutSelectSeconds;

        return loadoutSelectSeconds;
    }

    private void UpdateCountdownVisual(float t01)
    {
        if (countdownSlider != null)
            countdownSlider.value = t01;
    }

    private void OnCardClicked(int slotIndex)
    {
        if (MatchManager.Instance == null) return;

        if ((MatchManager.MatchPhase)MatchManager.Instance.Phase.Value != MatchManager.MatchPhase.LoadoutSelect)
            return;

        if (MatchManager.Instance.LoadoutsLocked.Value) return;
        if (_submitted) return;

        if (pickerPopup == null || iconDb == null) return;

        HashSet<AbilityId> disabled = BuildDisabledSet(exceptSlotIndex: slotIndex);

        pickerPopup.Show(iconDb, disabled, (pickedId) =>
        {
            _selected[slotIndex] = pickedId;
            RefreshCards();
        });
    }

    private HashSet<AbilityId> BuildDisabledSet(int exceptSlotIndex)
    {
        var set = new HashSet<AbilityId>();
        for (int i = 0; i < _selected.Length; i++)
        {
            if (i == exceptSlotIndex) continue;
            if (_selected[i].HasValue) set.Add(_selected[i].Value);
        }
        return set;
    }

    private void RefreshCards()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;

            if (_selected[i].HasValue)
                cards[i].SetFilled(iconDb != null ? iconDb.GetIcon(_selected[i].Value) : null);
            else
                cards[i].SetEmpty();
        }
    }

    private bool IsAllSlotsFilledNoDuplicates()
    {
        for (int i = 0; i < _selected.Length; i++)
            if (!_selected[i].HasValue) return false;

        for (int a = 0; a < _selected.Length; a++)
        for (int b = a + 1; b < _selected.Length; b++)
            if (_selected[a].Value == _selected[b].Value) return false;

        return true;
    }

    private void AutoFillMissingSlots()
    {
        if (iconDb == null || iconDb.entries == null || iconDb.entries.Count == 0)
            return;

        for (int slot = 0; slot < _selected.Length; slot++)
        {
            if (_selected[slot].HasValue) continue;

            for (int i = 0; i < iconDb.entries.Count; i++)
            {
                var candidate = iconDb.entries[i].id;
                if (!IsAlreadyPicked(candidate))
                {
                    _selected[slot] = candidate;
                    break;
                }
            }
        }

        RefreshCards();
    }

    private bool IsAlreadyPicked(AbilityId id)
    {
        for (int i = 0; i < _selected.Length; i++)
            if (_selected[i].HasValue && _selected[i].Value == id) return true;
        return false;
    }

    private void SubmitIfReady()
    {
        if (_submitted) return;

        if (!IsAllSlotsFilledNoDuplicates())
        {
            if (statusText != null) statusText.text = "Fill all 4 slots (no duplicates).";
            return;
        }

        AbilityId[] chosen = new AbilityId[4]
        {
            _selected[0].Value,
            _selected[1].Value,
            _selected[2].Value,
            _selected[3].Value
        };

        if (pickerPopup != null) pickerPopup.Hide();

        Debug.Log($"[LoadoutSelectUI] Submitting loadout: " +
                  $"{chosen[0]}, {chosen[1]}, {chosen[2]}, {chosen[3]}");
        MatchManager.Instance.SubmitLoadoutServerRpc(chosen);
        _submitted = true;

        if (lockInButton != null) lockInButton.interactable = false;
        if (statusText != null) statusText.text = "Locked in. Waiting for opponent...";
    }
}
