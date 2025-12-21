using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    public enum MatchPhase : int { WaitingForPlayers, Countdown, Playing, Ended, Overtime }

    [Header("Config")]
    [SerializeField] private int requiredPlayers = 2;
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float endScreenSeconds = 3f;
    [SerializeField] private float overtimeLabelSeconds = 2f; // optional UI use


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

    public NetworkVariable<ulong> WinnerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [SerializeField] private float matchSeconds = 180f;
    public NetworkVariable<float> MatchRemaining = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    NetworkVariable<int> CurrentRound;
    NetworkVariable<int> PlayerAWins;
    NetworkVariable<int> PlayerBWins;

    private bool _countdownStarted;
    private Coroutine _countdownRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!IsServer) return;

        if ((MatchPhase)Phase.Value == MatchPhase.WaitingForPlayers)
            TryStartCountdown();

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
        if ((MatchPhase)Phase.Value != MatchPhase.Playing &&
            (MatchPhase)Phase.Value != MatchPhase.Overtime) return;

        ulong winner = GetOtherClient(deadClientId);
        EndMatchServer(winner);
    }


    private ulong GetOtherClient(ulong deadClientId)
    {
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
            if (c.ClientId != deadClientId) return c.ClientId;

        return ulong.MaxValue;
    }

    private bool _ended;
    public void EndMatchServer(ulong winnerClientId)
    {
        if (!IsServer) return;
        if (_ended) return;
        _ended = true;

        Phase.Value = (int)MatchPhase.Ended;
        WinnerClientId.Value = winnerClientId;

        SetGameplayEnabledClientRpc(false);
        ShowEndScreenClientRpc(winnerClientId);

        StartCoroutine(ResetRoundRoutine());
    }

    [ClientRpc]
    private void SetGameplayEnabledClientRpc(bool enabled)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.SetGameplayEnabled(enabled);
    }

    [ClientRpc]
    private void ShowEndScreenClientRpc(ulong winnerClientId)
    {
        bool iWon = NetworkManager.Singleton.LocalClientId == winnerClientId;
        if (GameManager.Instance != null)
            GameManager.Instance.ShowMatchEnd(iWon);
    }

    private ulong _pendingWinner = ulong.MaxValue;
    private double _pendingWinnerTime = -1;

    public void ReportCaptureServer(ulong winnerClientId)
    {
        if (!IsServer) return;
        if ((MatchPhase)Phase.Value != MatchPhase.Playing &&
            (MatchPhase)Phase.Value != MatchPhase.Overtime) return;

        double now = NetworkManager.ServerTime.Time;

        // if no winner yet, store it
        if (_pendingWinner == ulong.MaxValue)
        {
            _pendingWinner = winnerClientId;
            _pendingWinnerTime = now;
            StartCoroutine(ResolveCaptureEndOfFrame());
            return;
        }

        // someone else also completed in same frame-ish -> draw
        if (_pendingWinner != winnerClientId && Mathf.Abs((float)(now - _pendingWinnerTime)) < 0.05f)
        {
            EndMatchServer(ulong.MaxValue); // treat as draw in UI
        }
    }

    private IEnumerator ResolveCaptureEndOfFrame()
    {
        yield return null; // wait one frame to allow other zone to report too
        if (_pendingWinner != ulong.MaxValue && (MatchPhase)Phase.Value != MatchPhase.Ended)
            EndMatchServer(_pendingWinner);

        _pendingWinner = ulong.MaxValue;
        _pendingWinnerTime = -1;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestRematchServerRpc()
    {
        if (!IsServer) return;
        StartCoroutine(ResetRoundRoutine());
    }

    private IEnumerator ResetRoundRoutine()
    {
        // wait so players can see win/lose
        yield return new WaitForSeconds(endScreenSeconds);

        // hard reset internal flags
        _ended = false;
        _countdownStarted = false;
        _countdownRoutine = null;
        WinnerClientId.Value = ulong.MaxValue;

        // reset objective capture resolution state
        _pendingWinner = ulong.MaxValue;
        _pendingWinnerTime = -1;

        // reset phase before rebuilding state so UI can show "Waiting"
        Phase.Value = (int)MatchPhase.WaitingForPlayers;

        // disable gameplay during reset
        SetGameplayEnabledClientRpc(false);

        // despawn all minions
        DespawnAllMinionsServer();

        // reset tiles + zones
        ResetAllTilesServer();
        ResetAllZonesServer();

        // respawn players at spawns + reset stats
        if (LocalSpawner.Instance != null)
            LocalSpawner.Instance.RespawnAllPlayersAtSpawnsServer();

        // start countdown again
        TryStartCountdown();
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

        _countdownRoutine = StartCoroutine(CountdownRoutine());
    }
}
