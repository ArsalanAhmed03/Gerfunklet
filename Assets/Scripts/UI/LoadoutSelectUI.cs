using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadoutSelectUI : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private GameObject root;

    [Header("Cards (4 slots)")]
    [SerializeField] private LoadoutCardUI[] cards = new LoadoutCardUI[4];

    [Header("Popup")]
    [SerializeField] private AbilityPickerPopupUI pickerPopup;
    [SerializeField] private AbilityIconDatabase iconDb;

    [Header("Submit")]
    [SerializeField] private Button lockInButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Countdown UI")]
    [SerializeField] private Slider countdownSlider; // 0..1 (local loadout timer visual)

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
        bool show = phase == MatchManager.MatchPhase.LoadoutSelect;

        if (root != null) root.SetActive(show);

        // IMPORTANT: if we are not in LoadoutSelect, force-close popup and stop here
        if (!show)
        {
            if (pickerPopup != null) pickerPopup.Hide();
            return;
        }

        bool locked = MatchManager.Instance.LoadoutsLocked.Value;

        if (!_submitted && _loadoutEndsAtLocal <= 0f)
            _loadoutEndsAtLocal = Time.time + loadoutSelectSeconds;

        // If server says locked, close popup and disable interactions
        if (locked)
        {
            if (pickerPopup != null) pickerPopup.Hide();
            if (statusText != null) statusText.text = "Locked. Starting...";
            if (lockInButton != null) lockInButton.interactable = false;
            UpdateCountdownVisual(1f);
            return;
        }

        float remaining = Mathf.Max(0f, _loadoutEndsAtLocal - Time.time);
        float t01 = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.01f, loadoutSelectSeconds));
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

        pickerPopup.Show(iconDb, (pickedId) =>
        {
            _selected[slotIndex] = pickedId;
            RefreshCards();
        });
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
        var all = (AbilityId[])System.Enum.GetValues(typeof(AbilityId));

        for (int slot = 0; slot < _selected.Length; slot++)
        {
            if (_selected[slot].HasValue) continue;

            for (int i = 0; i < all.Length; i++)
            {
                AbilityId candidate = all[i];
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

        // CLOSE POPUP immediately when submitting
        if (pickerPopup != null) pickerPopup.Hide();

        MatchManager.Instance.SubmitLoadoutServerRpc(chosen);
        _submitted = true;

        if (lockInButton != null) lockInButton.interactable = false;
        if (statusText != null) statusText.text = "Locked in. Waiting for opponent...";
    }
}
