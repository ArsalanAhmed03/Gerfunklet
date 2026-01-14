using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }
    public event Action<MatchPhase> OnPhaseChanged;
    public event Action<bool> OnLoadoutsLockedChanged;
    public event Action<int> OnRoundChanged;
    public event Action<int, int> OnScoreChanged;

    public float LoadoutSelectSeconds => loadoutSelectSeconds;
    public bool IsTeamAssignmentReady => PlayerAClientId.Value != ulong.MaxValue && PlayerBClientId.Value != ulong.MaxValue;
    public bool EnableObjectiveZones => enableObjectiveZones;
    public bool EnableAbilityLoadoutUI => enableAbilityLoadoutUI;
    public bool EnableRounds => enableRounds;

    // - RoundEnded = a single round finished, we reset arena and start next round
    // - MatchEnded = match is fully finished (best-of), stays ended until rematch requested
    public enum MatchPhase : int
    {
        WaitingForPlayers,
        LoadoutSelect,
        Countdown,
        Playing,
        Overtime,
        RoundEnded,
        MatchEnded
    }

    [Header("Config")]
    [SerializeField] private int requiredPlayers = 2;
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float loadoutSelectSeconds = 20f;

    [Header("Round / Match Rules")]
    [SerializeField] private bool enableRounds = false;
    [SerializeField] private int roundsToWin = 1;
    [SerializeField] private float endScreenSeconds = 3f;      // delay after round/match result shown
    [SerializeField] private float overtimeLabelSeconds = 2f;  // optional UI use
    [SerializeField] private float overtimeSeconds = 60f;
    [SerializeField] private bool enableDeathEndsRound = false;
    [SerializeField] private bool enableObjectiveZones = false;
    [SerializeField] private bool enableAbilityLoadoutUI = false;
    [SerializeField] private AbilityId[] defaultAbilityLoadout = new AbilityId[5]
    {
        AbilityId.Stomp,
        AbilityId.Devour,
        AbilityId.Rally,
        AbilityId.Parry,
        AbilityId.Throw
    };

    [Header("Match Timer")]
    [SerializeField] private float matchSeconds = 210f;

    [Header("Ability Database (assign all defs here)")]
    [SerializeField] private AbilityDefinition[] allAbilityDefs;

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

    public NetworkVariable<float> OvertimeRemaining = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

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

    public NetworkVariable<bool> LoadoutsLocked = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<ulong> PlayerAClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<ulong> PlayerBClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<double> LoadoutEndsAtServerTime = new NetworkVariable<double>(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Dictionary<ulong, AbilityId[]> _playerLoadouts;
    private Dictionary<AbilityId, AbilityDefinition> _defById;

    private bool _countdownStarted;
    private Coroutine _countdownRoutine;

    private bool _roundEnding;

    private ulong _pendingWinner = ulong.MaxValue;
    private double _pendingWinnerTime = -1;
    private bool _nvEventsBound;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        BindNetworkVariableEvents();

        if (!IsServer)
        {
            NotifyInitialState();
            return;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        BuildAbilityDatabaseServer();

        _playerLoadouts = new Dictionary<ulong, AbilityId[]>();

        ResetMatchStateServer();
        Phase.Value = (int)MatchPhase.WaitingForPlayers;
        SetGameplayEnabledClientRpc(false);

        NotifyInitialState();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        UnbindNetworkVariableEvents();
    }

    private void BuildAbilityDatabaseServer()
    {
        _defById = new Dictionary<AbilityId, AbilityDefinition>();

        if (allAbilityDefs == null) return;

        for (int i = 0; i < allAbilityDefs.Length; i++)
        {
            var def = allAbilityDefs[i];
            if (def == null) continue;

            if (_defById.ContainsKey(def.id))
            {
                Debug.LogWarning($"MatchManager: duplicate AbilityDefinition id {def.id}. Keeping first, ignoring later.");
                continue;
            }

            _defById.Add(def.id, def);
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        var phase = (MatchPhase)Phase.Value;

        if (phase == MatchPhase.WaitingForPlayers)
        {
            // When both players are present/spawned, move to LoadoutSelect
            AssignTeamsIfNeededServer();
            if (IsReadyPlayerCountMet())
                EnterLoadoutSelectServer();
        }
        else if (phase == MatchPhase.LoadoutSelect)
        {
            TryAutoLockLoadoutsServer();
        }

        // Match timer only ticks while live
        if (phase == MatchPhase.Playing)
        {
            MatchRemaining.Value -= Time.deltaTime;
            if (MatchRemaining.Value <= 0f)
            {
                MatchRemaining.Value = 0f;
                Phase.Value = (int)MatchPhase.Overtime;
                OvertimeRemaining.Value = overtimeSeconds;
            }
        }

        phase = (MatchPhase)Phase.Value;
        if (phase == MatchPhase.Overtime)
        {
            if (OvertimeRemaining.Value > 0f)
            {
                OvertimeRemaining.Value -= Time.deltaTime;
                if (OvertimeRemaining.Value <= 0f)
                {
                    OvertimeRemaining.Value = 0f;
                    EndRoundServer(ulong.MaxValue);
                }
            }
        }
    }

    private void BindNetworkVariableEvents()
    {
        if (_nvEventsBound) return;

        Phase.OnValueChanged += HandlePhaseChanged;
        LoadoutsLocked.OnValueChanged += HandleLoadoutsLockedChanged;
        CurrentRound.OnValueChanged += HandleRoundChanged;
        PlayerAWins.OnValueChanged += HandleScoreChanged;
        PlayerBWins.OnValueChanged += HandleScoreChanged;

        _nvEventsBound = true;
    }

    private void UnbindNetworkVariableEvents()
    {
        if (!_nvEventsBound) return;

        Phase.OnValueChanged -= HandlePhaseChanged;
        LoadoutsLocked.OnValueChanged -= HandleLoadoutsLockedChanged;
        CurrentRound.OnValueChanged -= HandleRoundChanged;
        PlayerAWins.OnValueChanged -= HandleScoreChanged;
        PlayerBWins.OnValueChanged -= HandleScoreChanged;

        _nvEventsBound = false;
    }

    private void NotifyInitialState()
    {
        OnPhaseChanged?.Invoke((MatchPhase)Phase.Value);
        OnLoadoutsLockedChanged?.Invoke(LoadoutsLocked.Value);
        OnRoundChanged?.Invoke(CurrentRound.Value);
        OnScoreChanged?.Invoke(PlayerAWins.Value, PlayerBWins.Value);
    }

    private void HandlePhaseChanged(int oldValue, int newValue)
    {
        OnPhaseChanged?.Invoke((MatchPhase)newValue);
    }

    private void HandleLoadoutsLockedChanged(bool oldValue, bool newValue)
    {
        OnLoadoutsLockedChanged?.Invoke(newValue);
    }

    private void HandleRoundChanged(int oldValue, int newValue)
    {
        OnRoundChanged?.Invoke(newValue);
    }

    private void HandleScoreChanged(int oldValue, int newValue)
    {
        OnScoreChanged?.Invoke(PlayerAWins.Value, PlayerBWins.Value);
    }

    private bool IsReadyPlayerCountMet()
    {
        if (LocalSpawner.Instance == null) return false;
        return LocalSpawner.Instance.GetSpawnedPlayerCount() >= requiredPlayers;
    }

    private void EnterLoadoutSelectServer()
    {
        if (!IsServer) return;

        // Don’t re-enter if already in a later phase
        var phase = (MatchPhase)Phase.Value;
        if (phase != MatchPhase.WaitingForPlayers) return;

        // Reset loadout state for a fresh match start
        LoadoutsLocked.Value = false;
        _playerLoadouts?.Clear();
        AssignTeamsIfNeededServer();
        SetLoadoutEndTimeServer();
        ResetAllHandsForMatchServer();

        // Ensure gameplay is disabled
        SetGameplayEnabledClientRpc(false);

        Phase.Value = (int)MatchPhase.LoadoutSelect;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        AssignTeamsIfNeededServer();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (PlayerAClientId.Value == clientId)
            PlayerAClientId.Value = ulong.MaxValue;
        if (PlayerBClientId.Value == clientId)
            PlayerBClientId.Value = ulong.MaxValue;
    }

    private void AssignTeamsIfNeededServer()
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton == null) return;
        if (LocalSpawner.Instance == null) return;
        if (LocalSpawner.Instance.GetSpawnedPlayerCount() < requiredPlayers) return;

        if (PlayerAClientId.Value != ulong.MaxValue && PlayerBClientId.Value != ulong.MaxValue)
            return;

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients.Count < requiredPlayers) return;

        PlayerAClientId.Value = clients[0].ClientId;
        PlayerBClientId.Value = clients[1].ClientId;
    }

    private void SetLoadoutEndTimeServer()
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton == null) return;
        LoadoutEndsAtServerTime.Value = NetworkManager.Singleton.ServerTime.Time + loadoutSelectSeconds;
    }

    private IEnumerator CountdownRoutine()
    {
        if (_countdownStarted && _countdownRoutine != null)
            StopCoroutine(_countdownRoutine);

        _countdownStarted = true;

        LoadoutEndsAtServerTime.Value = 0d;
        OvertimeRemaining.Value = 0f;
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
        if (!enableDeathEndsRound) return;

        ulong winner = GetOtherClient(deadClientId);
        EndRoundServer(winner);
    }

    private ulong GetOtherClient(ulong deadClientId)
    {
        if (TryGetOpponentClientId(deadClientId, out var opponent))
            return opponent;

        return ulong.MaxValue;
    }

    public bool TryGetOpponentClientId(ulong clientId, out ulong opponentId)
    {
        opponentId = ulong.MaxValue;

        if (PlayerAClientId.Value != ulong.MaxValue && PlayerBClientId.Value != ulong.MaxValue)
        {
            if (clientId == PlayerAClientId.Value) { opponentId = PlayerBClientId.Value; return true; }
            if (clientId == PlayerBClientId.Value) { opponentId = PlayerAClientId.Value; return true; }
        }

        if (NetworkManager.Singleton == null) return false;

        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (c.ClientId != clientId)
            {
                opponentId = c.ClientId;
                return true;
            }
        }

        return false;
    }

    public bool TryGetTeamClientIds(out ulong playerAClientId, out ulong playerBClientId)
    {
        playerAClientId = PlayerAClientId.Value;
        playerBClientId = PlayerBClientId.Value;
        return playerAClientId != ulong.MaxValue && playerBClientId != ulong.MaxValue;
    }

    public void ReportCaptureServer(ulong winnerClientId)
    {
        if (!IsServer) return;

        var phase = (MatchPhase)Phase.Value;
        if (phase != MatchPhase.Playing && phase != MatchPhase.Overtime) return;

        double now = NetworkManager.ServerTime.Time;

        if (_pendingWinner == ulong.MaxValue)
        {
            _pendingWinner = winnerClientId;
            _pendingWinnerTime = now;
            StartCoroutine(ResolveCaptureEndOfFrame());
            return;
        }

        if (_pendingWinner != winnerClientId && Mathf.Abs((float)(now - _pendingWinnerTime)) < 0.05f)
        {
            EndRoundServer(ulong.MaxValue); // draw
        }
    }

    private IEnumerator ResolveCaptureEndOfFrame()
    {
        yield return null;

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

        if (!enableRounds)
        {
            EndMatchImmediateServer(winnerClientId);
            return;
        }

        _roundEnding = true;

        SetGameplayEnabledClientRpc(false);
        Phase.Value = (int)MatchPhase.RoundEnded;

        if (winnerClientId != ulong.MaxValue)
            RegisterRoundWinServer(winnerClientId);

        bool matchOver =
            PlayerAWins.Value >= roundsToWin ||
            PlayerBWins.Value >= roundsToWin;

        if (matchOver)
        {
            Phase.Value = (int)MatchPhase.MatchEnded;
            WinnerClientId.Value = winnerClientId;

            ShowEndScreenClientRpc(winnerClientId, EndScreenKind.Match);
            StartCoroutine(MatchEndCooldownRoutine());
        }
        else
        {
            ShowEndScreenClientRpc(winnerClientId, EndScreenKind.Round);
            StartCoroutine(ResetRoundRoutine(startNewMatch: false));
        }
    }

    private IEnumerator MatchEndCooldownRoutine()
    {
        yield return new WaitForSeconds(endScreenSeconds);
        _roundEnding = false;
    }

    private void RegisterRoundWinServer(ulong winnerClientId)
    {
        ulong a = PlayerAClientId.Value;
        ulong b = PlayerBClientId.Value;

        if (a == ulong.MaxValue || b == ulong.MaxValue)
        {
            var clients = NetworkManager.Singleton.ConnectedClientsList;
            if (clients.Count < 2) return;

            a = clients[0].ClientId;
            b = clients[1].ClientId;
        }

        if (winnerClientId == a) PlayerAWins.Value++;
        else if (winnerClientId == b) PlayerBWins.Value++;
    }

    private void TryAutoLockLoadoutsServer()
    {
        if (!IsServer) return;
        if (LoadoutsLocked.Value) return;
        if (LoadoutEndsAtServerTime.Value <= 0d) return;

        double now = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ServerTime.Time
            : Time.timeAsDouble;

        if (now < LoadoutEndsAtServerTime.Value) return;

        LoadoutsLocked.Value = true;

        _countdownStarted = false;
        if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
        _countdownRoutine = StartCoroutine(CountdownRoutine());
    }

    public void ApplyDefaultLoadoutServer(GameObject playerGO)
    {
        if (!IsServer) return;
        if (playerGO == null) return;

        var runner = playerGO.GetComponent<AbilityRunner>();
        if (runner == null) return;

        if (defaultAbilityLoadout == null || defaultAbilityLoadout.Length != 5)
            return;

        runner.ApplyLoadoutServer(defaultAbilityLoadout);
        runner.ResetForNewRoundServerRpc();

        var super = playerGO.GetComponent<SuperCharge>();
        if (super != null)
            super.ResetForNewRoundServerRpc();
    }

    public void EndMatchImmediateServer(ulong winnerClientId)
    {
        if (!IsServer) return;
        if (_roundEnding) return;

        _roundEnding = true;
        SetGameplayEnabledClientRpc(false);

        PlayerAWins.Value = 0;
        PlayerBWins.Value = 0;
        if (winnerClientId == PlayerAClientId.Value) PlayerAWins.Value = roundsToWin;
        else if (winnerClientId == PlayerBClientId.Value) PlayerBWins.Value = roundsToWin;

        Phase.Value = (int)MatchPhase.MatchEnded;
        WinnerClientId.Value = winnerClientId;

        ShowEndScreenClientRpc(winnerClientId, EndScreenKind.Match);
        StartCoroutine(MatchEndCooldownRoutine());
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
            GameManager.Instance.ShowRoundEnd(iWon, isDraw);
        else
            GameManager.Instance.ShowMatchEndWithDraw(iWon, isDraw);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestRematchServerRpc()
    {
        if (!IsServer) return;

        ResetMatchStateServer();
        StartCoroutine(ResetRoundRoutine(startNewMatch: true));
    }

    private IEnumerator ResetRoundRoutine(bool startNewMatch)
    {
        yield return new WaitForSeconds(endScreenSeconds);

        _pendingWinner = ulong.MaxValue;
        _pendingWinnerTime = -1;

        _countdownStarted = false;
        _countdownRoutine = null;

        SetGameplayEnabledClientRpc(false);

        if (startNewMatch)
        {
            ResetMatchStateServer();

            // New match should go to LoadoutSelect again (fresh choices)
            LoadoutsLocked.Value = false;
            _playerLoadouts?.Clear();
            SetLoadoutEndTimeServer();
            ResetAllHandsForMatchServer();

            Phase.Value = (int)MatchPhase.LoadoutSelect;
        }
        else
        {
            CurrentRound.Value = Mathf.Max(1, CurrentRound.Value + 1);
            WinnerClientId.Value = ulong.MaxValue;

            // Between rounds, keep loadouts locked and immediately start countdown
            Phase.Value = (int)MatchPhase.Countdown;
        }

        DespawnAllMinionsServer();
        DespawnAllBuildablesServer();
        ResetAllTilesServer();
        ResetAllZonesServer();
        ResetAllMillstonesServer();

        if (LocalSpawner.Instance != null)
            LocalSpawner.Instance.RespawnAllPlayersAtSpawnsServer();

        _roundEnding = false;

        if (!startNewMatch)
        {
            // Start next round countdown
            TryStartCountdown();
        }
    }

    private void ResetMatchStateServer()
    {
        CurrentRound.Value = 1;
        PlayerAWins.Value = 0;
        PlayerBWins.Value = 0;
        WinnerClientId.Value = ulong.MaxValue;
        OvertimeRemaining.Value = 0f;

        _roundEnding = false;
        _pendingWinner = ulong.MaxValue;
        _pendingWinnerTime = -1;
    }

    private void ResetAllHandsForMatchServer()
    {
        if (!IsServer) return;
        if (LocalSpawner.Instance == null) return;
        if (NetworkManager.Singleton == null) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = LocalSpawner.Instance.GetPlayerForClient(client.ClientId);
            if (playerObj == null) continue;

            var hand = playerObj.GetComponent<CardHand>();
            
            if (hand != null)
                hand.ResetForNewMatchServer();
        }
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

    private void DespawnAllBuildablesServer()
    {
        if (!IsServer) return;

        var buildables = FindObjectsOfType<BuildableInstance>(true);
        foreach (var b in buildables)
        {
            if (b == null) continue;
            var no = b.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned)
                no.Despawn(true);
            else
                Destroy(b.gameObject);
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
        }
    }

    private void ResetAllTilesServer()
    {
        if (!IsServer) return;

        var tiles = FindObjectsOfType<TileBehaviour>(true);
        foreach (var t in tiles)
            t.ResetTileForNewRoundServer();
    }

    private void ResetAllMillstonesServer()
    {
        if (!IsServer) return;

        var heads = FindObjectsOfType<MillstoneHead>(true);
        foreach (var h in heads)
            h.ResetToHomeServer();
    }

    private void TryStartCountdown()
    {
        if (_countdownStarted) return;

        var phase = (MatchPhase)Phase.Value;
        if (phase != MatchPhase.Countdown) return;

        if (LocalSpawner.Instance == null) return;
        if (LocalSpawner.Instance.GetSpawnedPlayerCount() < requiredPlayers) return;

        // Do not start countdown until loadouts are locked for the match
        if (!LoadoutsLocked.Value) return;

        _countdownRoutine = StartCoroutine(CountdownRoutine());
    }

    private bool IsAllowedAbility(AbilityId id)
    {
        bool gddAbility = id == AbilityId.Stomp ||
                          id == AbilityId.Rally ||
                          id == AbilityId.Parry ||
                          id == AbilityId.Throw ||
                          id == AbilityId.Devour;

        if (!gddAbility) return false;
        return _defById != null && _defById.ContainsKey(id);
    }

    private AbilityDefinition GetDef(AbilityId id)
    {
        if (_defById != null && _defById.TryGetValue(id, out var def))
            return def;
        return null;
    }

    // private bool TryGetPlayerAbilityRunner(ulong clientId, out AbilityRunner runner)
    // {
    //     runner = null;

    //     if (NetworkManager.Singleton == null) return false;
    //     if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var cc)) return false;

    //     var playerObj = cc.PlayerObject;
    //     if (playerObj == null) return false;

    //     runner = playerObj.GetComponent<AbilityRunner>();
    //     return runner != null;
    // }

    private bool TryGetPlayerAbilityRunner(ulong clientId, out AbilityRunner runner)
    {
        runner = null;

        if (LocalSpawner.Instance != null)
        {
            var go = LocalSpawner.Instance.GetPlayerForClient(clientId);
            if (go != null)
            {
                runner = go.GetComponent<AbilityRunner>();
                return runner != null;
            }
        }

        if (NetworkManager.Singleton == null) return false;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var cc)) return false;
        if (cc.PlayerObject == null) return false;

        runner = cc.PlayerObject.GetComponent<AbilityRunner>();
        return runner != null;
    }


    [ServerRpc(RequireOwnership = false)]
    public void SubmitLoadoutServerRpc(AbilityId[] chosenAbilities, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong sender = rpcParams.Receive.SenderClientId;

        var phase = (MatchPhase)Phase.Value;
        Debug.Log($"[Loadout][SERVER] Submit from {sender} phase={phase} locked={LoadoutsLocked.Value} len={(chosenAbilities == null ? -1 : chosenAbilities.Length)}");

        if (phase != MatchPhase.LoadoutSelect) return;
        if (LoadoutsLocked.Value) return;
        if (chosenAbilities == null || chosenAbilities.Length != 5) return;

        var seen = new HashSet<AbilityId>();
        for (int i = 0; i < chosenAbilities.Length; i++)
        {
            if (!seen.Add(chosenAbilities[i])) return;
            if (!IsAllowedAbility(chosenAbilities[i])) return;
        }

        if (_playerLoadouts == null)
            _playerLoadouts = new Dictionary<ulong, AbilityId[]>();

        _playerLoadouts[sender] = chosenAbilities;
        Debug.Log($"[Loadout][SERVER] chosen for {sender}: {chosenAbilities[0]},{chosenAbilities[1]},{chosenAbilities[2]},{chosenAbilities[3]},{chosenAbilities[4]}");


        Debug.Log($"[Loadout][SERVER] Stored loadout for {sender}. totalSubmitted={_playerLoadouts.Count}/{requiredPlayers}");

        Debug.Log($"[Loadout][SERVER] ConnectedClients has sender? {NetworkManager.Singleton.ConnectedClients.ContainsKey(sender)} " +
          $"playerObjNull={(NetworkManager.Singleton.ConnectedClients.ContainsKey(sender) ? (NetworkManager.Singleton.ConnectedClients[sender].PlayerObject == null) : true)}");


        if (TryGetPlayerAbilityRunner(sender, out var runner))
        {
            Debug.Log($"[Loadout][SERVER] Found runner for {sender}. Before apply: {runner.Slot0.Value},{runner.Slot1.Value},{runner.Slot2.Value},{runner.Slot3.Value},{runner.Slot4.Value}");
            runner.ApplyLoadoutServer(chosenAbilities);
            Debug.Log($"[Loadout][SERVER] After apply: {runner.Slot0.Value},{runner.Slot1.Value},{runner.Slot2.Value},{runner.Slot3.Value},{runner.Slot4.Value}");
            runner.ResetForNewRoundServerRpc();

        }

        if (_playerLoadouts.Count >= requiredPlayers)
        {
            LoadoutsLocked.Value = true;

            Debug.Log("[Loadout][SERVER] Both submitted. Locking and starting countdown.");

            // Start countdown immediately (don’t depend on Update gates)
            _countdownStarted = false;
            if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
            _countdownRoutine = StartCoroutine(CountdownRoutine());
        }
    }

}
