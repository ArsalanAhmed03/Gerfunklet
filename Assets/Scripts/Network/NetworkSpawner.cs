using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro; // Add this for TextMeshPro support
using System.Collections;


public enum GameMode
{
    None,
    OneVOne,
}
public class NetworkSpawner : NetworkBehaviour
{
    [SerializeField] private int minPlayers = 2;
    [SerializeField] private string gameSceneName = "Random"; // Set this to your actual game scene name
    [SerializeField] private string timeoutSceneName = "MainMenu"; // Scene to load when timeout occurs
    [SerializeField] private float matchmakingTimeout = 50f; // Timeout in seconds
    private bool gameStarted = false;
    private float timeElapsed = 0f;
    private bool timerActive = false;
    [SerializeField] private GameObject TextBox;
    [SerializeField] private GameObject playersJoinedText; // Reference to the players joined text gameobject

    [SerializeField] private GameObject TimerText; // Reference to the timer text gameobject

    private TextMeshProUGUI timerTextComponent;
    private GameMode currentGameMode = GameMode.None;
    private int requiredPlayers = 0;

    private string targetSceneName = "";
    private bool isInitialized = false;
    private void Awake()
    {
        // Get the TextMeshProUGUI component from TimerText GameObject
        if (TimerText != null)
        {
            timerTextComponent = TimerText.GetComponent<TextMeshProUGUI>();
            if (timerTextComponent == null)
            {
                Debug.LogWarning("TimerText GameObject does not have a TextMeshProUGUI component");
            }
        }
    }


    bool initRan = false;
    // Update the init method to be more flexible
    public void init()
    {
        if (initRan) return;
        initRan = true;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public void OnJoinPressed()
    {
        // Start the matchmaking timer
        timerActive = true;
        timeElapsed = 0f; // Reset timer when joining

        // Show the text box
        if (TextBox != null)
        {
            TextBox.SetActive(true);
        }

        // Show the timer text
        if (TimerText != null)
        {
            TimerText.SetActive(true);
        }
    }

    public void OnClosePressed()
    {
        // Stop the matchmaking timer
        timerActive = false;

        // Hide the text box
        if (TextBox != null)
        {
            TextBox.SetActive(false);
        }

        // Hide the timer text
        if (TimerText != null)
        {
            TimerText.SetActive(false);
        }

        // If we're the host, shut down the network
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer || IsOwner))
        {
            NetworkManager.Singleton.Shutdown();
        }

    }

    private void Update()
    {
        // Only run the timer if it's active and game hasn't started
        if (timerActive && !gameStarted)
        {
            timeElapsed += Time.deltaTime;
            UpdateTimerDisplay();
            int connectedPlayers = GetConnectedPlayers();
            if (playersJoinedText != null)
            {
                playersJoinedText.GetComponent<TextMeshProUGUI>().text = $"Finding players...";
            }

            // Check if timeout period has elapsed
            if (timeElapsed >= matchmakingTimeout)
            {
                timerActive = false;
                HandleMatchmakingTimeout();
            }
        }
    }

    private void UpdateTimerDisplay()
    {
        // Update the timer text if the component exists
        if (timerTextComponent != null)
        {
            float remainingTime = Mathf.Max(0, matchmakingTimeout - timeElapsed);
            int seconds = Mathf.FloorToInt(remainingTime);
            timerTextComponent.text = $"Finding Match: {seconds}s";
        }
    }

    private void HandleMatchmakingTimeout()
    {
        Debug.Log("Matchmaking timed out after " + matchmakingTimeout + " seconds");

        // If we're the host, shut down the network
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Hide UI elements
        if (TextBox != null)
        {
            TextBox.SetActive(false);
        }

        if (TimerText != null)
        {
            TimerText.SetActive(false);
        }

        // Load the timeout scene (e.g., main menu)
        SceneManager.LoadScene(timeoutSceneName);
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("Client connected: " + clientId);
        if(GetConnectedPlayers() >= minPlayers)
        {
            StartGame();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log("Client disconnected: " + clientId);
    }
    private void StartGame()
    {
        if (gameStarted) return;

        gameStarted = true;
        timerActive = false; // Stop the timer when game starts
        Debug.Log("Starting game with enough players!");

        // Hide UI elements
        if (TextBox != null)
        {
            TextBox.SetActive(false);
        }

        if (TimerText != null)
        {
            TimerText.SetActive(false);
        }

        // Load the game scene for all clients
        Debug.Log("Loading game scene: " + gameSceneName);
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    public GameMode GetCurrentGameMode()
    {
        return currentGameMode;
    }

    public int GetRequiredPlayers()
    {
        return requiredPlayers;
    }

    public int GetConnectedPlayers()
    {
        return NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClients.Count : 0;
    }

    public bool IsGameStarted()
    {
        return gameStarted;
    }

    public bool IsInitialized()
    {
        return isInitialized;
    }

}

