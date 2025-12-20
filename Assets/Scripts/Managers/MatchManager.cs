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

    private bool _countdownStarted;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!IsServer) return;

        if ((MatchPhase)Phase.Value == MatchPhase.WaitingForPlayers)
        {
            if (!_countdownStarted &&
                LocalSpawner.Instance != null &&
                LocalSpawner.Instance.GetSpawnedPlayerCount() >= requiredPlayers)
            {
                _countdownStarted = true;
                StartCoroutine(CountdownRoutine());
            }
        }

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

    public void EndMatchServer(ulong winnerClientId)
    {
        if (!IsServer) return;

        Phase.Value = (int)MatchPhase.Ended;
        WinnerClientId.Value = winnerClientId;

        SetGameplayEnabledClientRpc(false);
        ShowEndScreenClientRpc(winnerClientId);
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
}
