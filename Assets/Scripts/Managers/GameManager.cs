using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    public Slider healthBar;
    public Slider staminaBar;
    public GameObject sleepingIndicator;

    [Header("Gameplay UI Roots")]
    [SerializeField] private GameObject controlsRoot;

    [Header("Citadel/Throne UI")]
    public Slider enemyCitadelBar;
    public TextMeshProUGUI enemyCitadelText;
    public Slider throneCaptureBar;
    public TextMeshProUGUI throneCaptureText;

    [Header("Citadel/Throne Objects")]
    public CitadelHealth citadelA;
    public CitadelHealth citadelB;
    public ThroneCapture throneA;
    public ThroneCapture throneB;

    [Header("Optional Match UI")]
    [SerializeField] private TextMeshProUGUI statusText;      // "Waiting / Countdown / Playing / Overtime / Ended"
    [SerializeField] private TextMeshProUGUI endText;         // Final match result (YOU WIN / YOU LOSE / DRAW)

    [Header("Round UI (Optional)")]
    [SerializeField] private TextMeshProUGUI roundText;       // "Round 1"
    [SerializeField] private TextMeshProUGUI scoreText;       // "Score: 1 - 0"
    [SerializeField] private TextMeshProUGUI roundResultText; // "ROUND WON / ROUND LOST / ROUND DRAW"

    [Header("Match UI")]
    public TextMeshProUGUI matchTimerText;

    public bool GameplayEnabled { get; private set; } = false;

    private MatchManager _match;
    private bool _boundToMatch;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        UnbindMatchManager();

        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        TryBindMatchManager();
    }

    private void OnDisable()
    {
        UnbindMatchManager();
    }

    private void Update()
    {
        if (!_boundToMatch)
            TryBindMatchManager();
    }

    public void SetGameplayEnabled(bool enabled)
    {
        GameplayEnabled = enabled;
    }

    // Called by MatchManager for FINAL match end (current behaviour)
    public void ShowMatchEnd(bool iWon)
    {
        if (endText != null)
            endText.text = iWon ? "YOU WIN" : "YOU LOSE";

        // Optional: clear round result so UI doesn’t conflict
        if (roundResultText != null)
            roundResultText.text = "";
    }

    // New: use this for round-end UI (recommended)
    public void ShowRoundEnd(bool iWon, bool isDraw)
    {
        if (roundResultText == null) return;

        if (isDraw) roundResultText.text = "ROUND DRAW";
        else roundResultText.text = iWon ? "ROUND WON" : "ROUND LOST";
    }

    // New: use this for match-end UI with draw support
    public void ShowMatchEndWithDraw(bool iWon, bool isDraw)
    {
        if (endText == null) return;

        if (isDraw) endText.text = "DRAW";
        else endText.text = iWon ? "YOU WIN" : "YOU LOSE";

        if (roundResultText != null)
            roundResultText.text = "";
    }

    private void TryBindMatchManager()
    {
        if (_boundToMatch) return;
        if (MatchManager.Instance == null) return;

        _match = MatchManager.Instance;
        _match.OnPhaseChanged += HandlePhaseChanged;
        _match.OnRoundChanged += HandleRoundChanged;
        _match.OnScoreChanged += HandleScoreChanged;
        _boundToMatch = true;

        HandlePhaseChanged((MatchManager.MatchPhase)_match.Phase.Value);
        HandleRoundChanged(_match.CurrentRound.Value);
        HandleScoreChanged(_match.PlayerAWins.Value, _match.PlayerBWins.Value);
    }

    private void UnbindMatchManager()
    {
        if (!_boundToMatch || _match == null) return;

        _match.OnPhaseChanged -= HandlePhaseChanged;
        _match.OnRoundChanged -= HandleRoundChanged;
        _match.OnScoreChanged -= HandleScoreChanged;
        _boundToMatch = false;
        _match = null;
    }

    private void HandlePhaseChanged(MatchManager.MatchPhase phase)
    {
        if (statusText != null)
        {
            statusText.text = phase switch
            {
                MatchManager.MatchPhase.LoadoutSelect => "Ready/Mulligan",
                _ => phase.ToString()
            };
        }

        UpdateGameplayUiForPhase(phase);
    }

    private void UpdateGameplayUiForPhase(MatchManager.MatchPhase phase)
    {
        bool gameplay = phase == MatchManager.MatchPhase.Playing || phase == MatchManager.MatchPhase.Overtime;

        SetActive(controlsRoot, gameplay);
        SetActive(healthBar != null ? healthBar.gameObject : null, gameplay);
        SetActive(staminaBar != null ? staminaBar.gameObject : null, gameplay);
        SetActive(sleepingIndicator, gameplay);

        SetActive(enemyCitadelBar != null ? enemyCitadelBar.gameObject : null, gameplay);
        SetActive(enemyCitadelText != null ? enemyCitadelText.gameObject : null, gameplay);
        SetActive(throneCaptureBar != null ? throneCaptureBar.gameObject : null, gameplay);
        SetActive(throneCaptureText != null ? throneCaptureText.gameObject : null, gameplay);
    }

    private void SetActive(GameObject obj, bool active)
    {
        if (obj == null) return;
        obj.SetActive(active);
    }

    private void HandleRoundChanged(int round)
    {
        if (_match != null && !_match.EnableRounds)
        {
            SetActive(roundText != null ? roundText.gameObject : null, false);
            SetActive(scoreText != null ? scoreText.gameObject : null, false);
            SetActive(roundResultText != null ? roundResultText.gameObject : null, false);
            return;
        }

        if (roundText != null)
            roundText.text = $"Round {round}";
    }

    private void HandleScoreChanged(int playerAWins, int playerBWins)
    {
        if (_match != null && !_match.EnableRounds)
        {
            SetActive(roundText != null ? roundText.gameObject : null, false);
            SetActive(scoreText != null ? scoreText.gameObject : null, false);
            SetActive(roundResultText != null ? roundResultText.gameObject : null, false);
            return;
        }

        if (scoreText != null)
        {
            // Score uses PlayerAWins/PlayerBWins based on join order (clients[0] vs clients[1] on server).
            // For UI, we just display A-B consistently.
            scoreText.text = $"Score: {playerAWins} - {playerBWins}";
        }
    }
}
