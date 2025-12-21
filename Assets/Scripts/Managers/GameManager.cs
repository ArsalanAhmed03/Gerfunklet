using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    public Slider healthBar;
    public Slider staminaBar;

    [Header("Objective UI")]
    public Slider dangerCaptureBar;      // enemy capturing my zone
    public Slider myCaptureBar;          // me capturing enemy zone
    public TextMeshProUGUI captureStateText;

    [Header("Objective Zones")]
    public ObjectiveZone zoneA;
    public ObjectiveZone zoneB;

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
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // Optional live UI refresh (simple + safe)
        if (MatchManager.Instance == null) return;

        // Phase/status text
        if (statusText != null)
        {
            var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
            statusText.text = phase.ToString();
        }

        // Round + score UI (only if you assigned the text fields)
        if (roundText != null)
            roundText.text = $"Round {MatchManager.Instance.CurrentRound.Value}";

        if (scoreText != null)
        {
            // Score uses PlayerAWins/PlayerBWins based on join order (clients[0] vs clients[1] on server).
            // For UI, we just display A-B consistently.
            scoreText.text = $"Score: {MatchManager.Instance.PlayerAWins.Value} - {MatchManager.Instance.PlayerBWins.Value}";
        }
    }

    public void SetGameplayEnabled(bool enabled)
    {
        GameplayEnabled = enabled;

        if (statusText != null)
            statusText.text = enabled ? "Playing" : "Not Playing";
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
}
