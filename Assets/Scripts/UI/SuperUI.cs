using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SuperUI : MonoBehaviour
{
    [Header("Super UI")]
    [SerializeField] private Slider chargeBar;
    [SerializeField] private TextMeshProUGUI chargeText;
    [SerializeField] private Button superButton;
    [SerializeField] private Image superIcon;
    [SerializeField] private TextMeshProUGUI superNameText;
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private PulseUI readyPulse;

    private SuperCharge _charge;
    private SuperController _controller;
    private ulong _boundObjectId = ulong.MaxValue;
    private bool _bound;
    private bool _lastGameplay;

    private void Awake()
    {
        if (superButton != null)
            superButton.onClick.AddListener(OnSuperClicked);
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

        UpdateButtonState();
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

        _charge = playerObj.GetComponent<SuperCharge>();
        _controller = playerObj.GetComponent<SuperController>();
        _boundObjectId = objectId;
        if (_charge == null) return;

        _charge.Charge01.OnValueChanged += HandleChargeChanged;
        if (_controller != null)
            _controller.Choice.OnValueChanged += HandleChoiceChanged;
        _bound = true;
        HandleChargeChanged(0f, _charge.Charge01.Value);
        UpdateChoiceVisuals();
    }

    private void Unbind()
    {
        if (_charge != null)
            _charge.Charge01.OnValueChanged -= HandleChargeChanged;
        if (_controller != null)
            _controller.Choice.OnValueChanged -= HandleChoiceChanged;

        _charge = null;
        _controller = null;
        _bound = false;
        _boundObjectId = ulong.MaxValue;
    }

    private void HandleChargeChanged(float oldValue, float newValue)
    {
        if (chargeBar != null)
        {
            chargeBar.maxValue = 1f;
            chargeBar.value = Mathf.Clamp01(newValue);
        }

        if (chargeText != null)
        {
            int pct = Mathf.RoundToInt(Mathf.Clamp01(newValue) * 100f);
            chargeText.text = pct >= 100 ? "SUPER READY" : $"{pct}%";
        }

        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        if (superButton == null) return;

        bool gameplay = GameManager.Instance != null && GameManager.Instance.GameplayEnabled;
        if (gameplay != _lastGameplay)
            _lastGameplay = gameplay;

        bool ready = _charge != null && _charge.IsFull;
        superButton.interactable = gameplay && ready;

        if (readyIndicator != null)
            readyIndicator.SetActive(gameplay && ready);

        if (readyPulse != null)
            readyPulse.gameObject.SetActive(gameplay && ready);
    }

    private void OnSuperClicked()
    {
        if (_controller == null) return;
        _controller.TryCastSuper();
    }

    private void HandleChoiceChanged(SuperChoice oldChoice, SuperChoice newChoice)
    {
        UpdateChoiceVisuals();
    }

    private void UpdateChoiceVisuals()
    {
        if (_controller == null) return;

        var choice = _controller.Choice.Value;
        if (superNameText != null)
            superNameText.text = FormatChoice(choice);

        if (superIcon != null)
        {
            var icon = _controller.GetIcon(choice);
            superIcon.sprite = icon;
            superIcon.enabled = icon != null;
        }
    }

    private string FormatChoice(SuperChoice choice)
    {
        switch (choice)
        {
            case SuperChoice.SeismicQuake:
                return "Seismic Quake";
            case SuperChoice.BoulderPitch:
                return "Boulder Pitch";
            case SuperChoice.Gorge:
                return "Gorge";
            default:
                return choice.ToString();
        }
    }
}
