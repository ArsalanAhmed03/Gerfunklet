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
        _bound = true;
        HandleChargeChanged(0f, _charge.Charge01.Value);
    }

    private void Unbind()
    {
        if (_charge != null)
            _charge.Charge01.OnValueChanged -= HandleChargeChanged;

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
    }

    private void OnSuperClicked()
    {
        if (_controller == null) return;
        _controller.TryCastSuper();
    }
}
