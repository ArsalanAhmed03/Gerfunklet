using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    // - RoundEnded = a single round finished, we will reset arena and start next round
    // - MatchEnded = match is fully finished (best-of), stays ended until rematch requested
    public enum MatchPhase : int
    {
        WaitingForPlayers,
        Countdown,
        Playing,
        Overtime,
        RoundEnded,
        MatchEnded
    }

    [Header("Config")]
    [SerializeField] private int requiredPlayers = 2;
    [SerializeField] private float countdownSeconds = 3f;

    [Header("Round / Match Rules")]
    [SerializeField] private int roundsToWin = 2;            // Best-of-3 => first to 2
    [SerializeField] private float endScreenSeconds = 3f;     // delay after round/match result shown
    [SerializeField] private float overtimeLabelSeconds = 2f; // optional UI use

    [Header("Match Timer")]
    [SerializeField] private float matchSeconds = 180f;

    public NetworkVariable<int> Phase = new NetworkVariable<int>(
        (int)MatchPhase.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> CountdownRemaining = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // WinnerClientId is meaningful only when Phase == MatchEnded
    // For draws we keep ulong.MaxValue
    public NetworkVariable<ulong> WinnerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> MatchRemaining = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Round state (public so UI can read later if you want)
    public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> PlayerAWins = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> PlayerBWins = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool _countdownStarted;
    private Coroutine _countdownRoutine;

    // For round-end gating (prevents double-end calls)
    private bool _roundEnding;

    // Capture race resolution
    private ulong _pendingWinner = ulong.MaxValue;
    private double _pendingWinnerTime = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Ensure clean defaults on server spawn (important when you stop playmode/rehost etc.)
        ResetMatchStateServer();
        Phase.Value = (int)MatchPhase.WaitingForPlayers;
        SetGameplayEnabledClientRpc(false);
    }

    private void Update()
    {
        if (!IsServer) return;

        // Only auto-start countdown when waiting (not during MatchEnded)
        if ((MatchPhase)Phase.Value == MatchPhase.WaitingForPlayers)
            TryStartCountdown();

        // Match timer only ticks while live
        if ((MatchPhase)Phase.Value == MatchPhase.Playing)
        {
            MatchRemaining.Value -= Time.deltaTime;
            if (MatchRemaining.Value <= 0f)
            {
                MatchRemaining.Value = 0f;
                Phase.Value = (int)MatchPhase.Overtime;
            }
        }
    }

    private IEnumerator CountdownRoutine()
    {
        if (_countdownStarted && _countdownRoutine != null)
            StopCoroutine(_countdownRoutine);

        _countdownStarted = true;

        Phase.Value = (int)MatchPhase.Countdown;

        MatchRemaining.Value = matchSeconds;
        float t = countdownSeconds;
        while (t > 0f)
        {
            CountdownRemaining.Value = t;
            yield return null;
            t -= Time.deltaTime;
        }

        CountdownRemaining.Value = 0f;
        Phase.Value = (int)MatchPhase.Playing;

        MatchRemaining.Value = matchSeconds;

        SetGameplayEnabledClientRpc(true);

        _countdownRoutine = null;
    }

    public void NotifyPlayerDied(ulong deadClientId)
    {
        if (!IsServer) return;

        var phase = (MatchPhase)Phase.Value;
        if (phase != MatchPhase.Playing && phase != MatchPhase.Overtime) return;

        ulong winner = GetOtherClient(deadClientId);
        EndRoundServer(winner);
    }

    private ulong GetOtherClient(ulong deadClientId)
    {
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
            if (c.ClientId != deadClientId) return c.ClientId;

        return ulong.MaxValue;
    }

    // Called by ObjectiveZone when a capture completes
    public void ReportCaptureServer(ulong winnerClientId)
    {
        if (!IsServer) return;

        var phase = (MatchPhase)Phase.Value;
        if (phase != MatchPhase.Playing && phase != MatchPhase.Overtime) return;

        double now = NetworkManager.ServerTime.Time;

        // If no winner yet, store it and resolve end-of-frame (race window)
        if (_pendingWinner == ulong.MaxValue)
        {
            _pendingWinner = winnerClientId;
            _pendingWinnerTime = now;
            StartCoroutine(ResolveCaptureEndOfFrame());
            return;
        }

        // Someone else also completed in same frame-ish -> draw round
        if (_pendingWinner != winnerClientId && Mathf.Abs((float)(now - _pendingWinnerTime)) < 0.05f)
        {
            EndRoundServer(ulong.MaxValue); // draw
        }
    }

    private IEnumerator ResolveCaptureEndOfFrame()
    {
        yield return null; // wait one frame to allow other zone to report too

        // If we still have a pending winner and round is not already ending, end the round
        if (_pendingWinner != ulong.MaxValue)
        {
            var phase = (MatchPhase)Phase.Value;
            if (phase != MatchPhase.RoundEnded && phase != MatchPhase.MatchEnded)
                EndRoundServer(_pendingWinner);
        }

        _pendingWinner = ulong.MaxValue;
        _pendingWinnerTime = -1;
    }

    private void EndRoundServer(ulong winnerClientId)
    {
        if (!IsServer) return;
        if (_roundEnding) return;
        _roundEnding = true;

        // Disable gameplay immediately
        SetGameplayEnabledClientRpc(false);

        // Mark round ended
        Phase.Value = (int)MatchPhase.RoundEnded;

        // Update wins unless draw
        if (winnerClientId != ulong.MaxValue)
            RegisterRoundWinServer(winnerClientId);

        // Decide if match is over
        bool matchOver =
            PlayerAWins.Value >= roundsToWin ||
            PlayerBWins.Value >= roundsToWin;

        if (matchOver)
        {
            // Match ended
            Phase.Value = (int)MatchPhase.MatchEnded;
            WinnerClientId.Value = winnerClientId;

            // Reuse your existing end UI
            ShowEndScreenClientRpc(winnerClientId, EndScreenKind.Match);

            // Do NOT auto-reset into a new match.
            // Wait for RequestRematchServerRpc().
            StartCoroutine(MatchEndCooldownRoutine());
        }
        else
        {
            // Round ended (not match ended). For now we reuse the same UI.
            // If you want separate round UI later, replace this with ShowRoundEndClientRpc().
            ShowEndScreenClientRpc(winnerClientId, EndScreenKind.Round);

            StartCoroutine(ResetRoundRoutine(startNewMatch: false));
        }
    }

    private IEnumerator MatchEndCooldownRoutine()
    {
        // let players see final result
        yield return new WaitForSeconds(endScreenSeconds);

        // keep everything ended until rematch requested
        // allow a new countdown only after RequestRematchServerRpc triggers reset
        _roundEnding = false;
    }

    private void RegisterRoundWinServer(ulong winnerClientId)
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients.Count < 2) return;

        ulong a = clients[0].ClientId;
        ulong b = clients[1].ClientId;

        if (winnerClientId == a) PlayerAWins.Value++;
        else if (winnerClientId == b) PlayerBWins.Value++;
    }

    [ClientRpc]
    private void SetGameplayEnabledClientRpc(bool enabled)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.SetGameplayEnabled(enabled);
    }

    public enum EndScreenKind : int
    {
        Round,
        Match
    }

    [ClientRpc(RequireOwnership = false)]
    private void ShowEndScreenClientRpc(ulong winnerClientId, EndScreenKind kind)
    {
        bool isDraw = winnerClientId == ulong.MaxValue;
        bool iWon = !isDraw && (NetworkManager.Singleton.LocalClientId == winnerClientId);

        if (GameManager.Instance == null) return;

        if (kind == EndScreenKind.Round)
        {
            GameManager.Instance.ShowRoundEnd(iWon, isDraw);
        }
        else
        {
            GameManager.Instance.ShowMatchEndWithDraw(iWon, isDraw);
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void RequestRematchServerRpc()
    {
        if (!IsServer) return;

        // Only allow rematch if match is ended (or if you want to allow anytime)
        // This keeps flow clean.
        ResetMatchStateServer();
        StartCoroutine(ResetRoundRoutine(startNewMatch: true));
    }

    private IEnumerator ResetRoundRoutine(bool startNewMatch)
    {
        // wait so players can see result (round or match if you call this there)
        yield return new WaitForSeconds(endScreenSeconds);

        // reset objective capture resolution state
        _pendingWinner = ulong.MaxValue;
        _pendingWinnerTime = -1;

        // reset countdown state
        _countdownStarted = false;
        _countdownRoutine = null;

        // Clear gameplay
        SetGameplayEnabledClientRpc(false);

        // If we are starting a brand new match (rematch), reset wins/round and winner
        if (startNewMatch)
        {
            ResetMatchStateServer();
        }
        else
        {
            // Starting next round (same match)
            CurrentRound.Value = Mathf.Max(1, CurrentRound.Value + 1);
            WinnerClientId.Value = ulong.MaxValue; // keep unused unless MatchEnded
        }

        // reset phase before rebuilding state so UI can show "Waiting"
        Phase.Value = (int)MatchPhase.WaitingForPlayers;

        // despawn all minions (safe even if you aren't using them much yet)
        DespawnAllMinionsServer();

        // reset tiles + zones
        ResetAllTilesServer();
        ResetAllZonesServer();

        // respawn players at spawns + reset stats
        if (LocalSpawner.Instance != null)
            LocalSpawner.Instance.RespawnAllPlayersAtSpawnsServer();

        // allow next round to end again
        _roundEnding = false;

        // start countdown again
        TryStartCountdown();
    }

    private void ResetMatchStateServer()
    {
        CurrentRound.Value = 1;
        PlayerAWins.Value = 0;
        PlayerBWins.Value = 0;
        WinnerClientId.Value = ulong.MaxValue;

        _roundEnding = false;
        _pendingWinner = ulong.MaxValue;
        _pendingWinnerTime = -1;
    }

    private void DespawnAllMinionsServer()
    {
        if (!IsServer) return;

        var minions = GameObject.FindGameObjectsWithTag("Minion");
        foreach (var m in minions)
        {
            var no = m.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned)
                no.Despawn(true);
            else
                Destroy(m);
        }
    }

    private void ResetAllZonesServer()
    {
        if (!IsServer) return;

        var zones = FindObjectsOfType<ObjectiveZone>(true);
        foreach (var z in zones)
        {
            z.progress01.Value = 0f;
            z.contested.Value = false;
            z.currentAttackerClientId.Value = ulong.MaxValue;
            // keep owner assignment as-is; it will remain correct
        }
    }

    private void ResetAllTilesServer()
    {
        if (!IsServer) return;

        var tiles = FindObjectsByType<TileBehaviour>(FindObjectsSortMode.None);
        foreach (var t in tiles)
        {
            t.ResetTileForNewRoundServer();
        }
    }

    private void TryStartCountdown()
    {
        if (_countdownStarted) return;
        if (LocalSpawner.Instance == null) return;
        if (LocalSpawner.Instance.GetSpawnedPlayerCount() < requiredPlayers) return;

        // Don't start a new countdown if match ended
        if ((MatchPhase)Phase.Value == MatchPhase.MatchEnded) return;

        _countdownRoutine = StartCoroutine(CountdownRoutine());
    }
}
