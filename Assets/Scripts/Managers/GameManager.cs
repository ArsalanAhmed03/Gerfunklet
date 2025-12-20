using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    public Slider healthBar;
    public Slider staminaBar;

    [Header("Optional Match UI")]
    [SerializeField] private TextMeshProUGUI statusText;  // "Waiting / Countdown / Playing / Ended"
    [SerializeField] private TextMeshProUGUI endText;     // "You Win / You Lose"

    [Header("Spawns")]
    public Transform playerSpawns;

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

    public void SetGameplayEnabled(bool enabled)
    {
        GameplayEnabled = enabled;

        if (statusText != null)
            statusText.text = enabled ? "Playing" : "Not Playing";
    }

    public void ShowMatchEnd(bool iWon)
    {
        if (endText != null)
            endText.text = iWon ? "YOU WIN" : "YOU LOSE";
    }
}
