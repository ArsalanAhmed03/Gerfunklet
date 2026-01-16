using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ForageModeUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button protectButton;
    [SerializeField] private Button balancedButton;
    [SerializeField] private Button maxButton;
    [SerializeField] private GameObject[] highlights = new GameObject[3];

    private ForageModeController _controller;
    private PlayerStatsManager _stats;

    private void Awake()
    {
        if (protectButton != null)
            protectButton.onClick.AddListener(() => SetMode(ForageModeController.ForageMode.ProtectOnly));
        if (balancedButton != null)
            balancedButton.onClick.AddListener(() => SetMode(ForageModeController.ForageMode.Balanced));
        if (maxButton != null)
            maxButton.onClick.AddListener(() => SetMode(ForageModeController.ForageMode.MaxForage));
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
        if (_controller == null)
            BindIfNeeded();

        RefreshVisibility();
    }

    private void BindIfNeeded()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient) return;

        var playerObj = nm.SpawnManager.GetLocalPlayerObject();
        if (playerObj == null) return;

        _controller = playerObj.GetComponent<ForageModeController>();
        _stats = playerObj.GetComponent<PlayerStatsManager>();

        if (_controller != null)
            _controller.Mode.OnValueChanged += HandleModeChanged;

        RefreshHighlights();
    }

    private void Unbind()
    {
        if (_controller != null)
            _controller.Mode.OnValueChanged -= HandleModeChanged;

        _controller = null;
        _stats = null;
    }

    private void SetMode(ForageModeController.ForageMode mode)
    {
        if (_controller == null) return;
        _controller.SetModeServerRpc(mode);
    }

    private void HandleModeChanged(int oldValue, int newValue)
    {
        RefreshHighlights();
    }

    private void RefreshHighlights()
    {
        if (highlights == null || highlights.Length == 0) return;
        int idx = _controller != null ? _controller.Mode.Value : 1;
        for (int i = 0; i < highlights.Length; i++)
        {
            if (highlights[i] != null)
                highlights[i].SetActive(i == idx);
        }
    }

    private void RefreshVisibility()
    {
        if (root == null) return;
        bool sleeping = _stats != null && _stats.IsSleeping;
        if (root.activeSelf != sleeping)
            root.SetActive(sleeping);
    }
}
