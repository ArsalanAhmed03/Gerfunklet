using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class LocalSpawner : NetworkBehaviour
{

    public static LocalSpawner Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    [Header("Spawning Configuration")]
    [SerializeField] private GameObject playerPrefab;

    [SerializeField] private GameObject minionPrefab;
    [SerializeField] private Transform[] spawnLocations;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();
    private int currentSpawnIndex = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (!HasValidSpawnPoints())
        {
            Debug.LogError("LocalSpawner missing spawn locations; cannot spawn players.");
            return;
        }

        Debug.Log("LocalSpawner initialized on Server");

        // Spawn existing connections (host + any late joiners pre-start)
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            SpawnPlayerForClient(client.ClientId);
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (debugMode)
            Debug.Log($"Client {clientId} connected. Spawning player...");

        SpawnPlayerForClient(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (debugMode)
            Debug.Log($"Client {clientId} disconnected. Cleaning up player...");

        DespawnPlayerForClient(clientId);
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (!IsServer) return;
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned!");
            return;
        }

        if (!HasValidSpawnPoints())
        {
            Debug.LogError("No spawn locations assigned!");
            return;
        }

        if (spawnedPlayers.ContainsKey(clientId))
        {
            Debug.LogWarning($"Player for client {clientId} already exists!");
            return;
        }

        // Get spawn position
        Transform spawnLocation = GetNextSpawnLocation();
        Vector3 spawnPosition = spawnLocation.position;
        Quaternion spawnRotation = spawnLocation.rotation;

        // Instantiate player prefab
        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, spawnRotation);

        // Get NetworkObject and spawn with ownership
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            // networkObject.SpawnWithOwnership(clientId);
            networkObject.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[Spawner] client={clientId} PlayerObjectNull={NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject==null}");

            spawnedPlayers[clientId] = playerInstance;

            if (debugMode)
                Debug.Log($"Player spawned for client {clientId} at {spawnPosition}");
        }
        else
        {
            Debug.LogError("Player prefab must have a NetworkObject component!");
            Destroy(playerInstance);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnMinionForClientServerRpc(ulong clientId)
    {
        Debug.Log($"SpawnMinionForClientServerRpc called by client {clientId}");
        if (minionPrefab == null)
        {
            Debug.LogError("Minion prefab is not assigned!");
            return;
        }

        if (!spawnedPlayers.TryGetValue(clientId, out var ownerPlayer) || ownerPlayer == null)
        {
            Debug.LogWarning($"Cannot spawn minion: player for client {clientId} not found.");
            return;
        }

        Transform spawnLocation = ownerPlayer.transform;
        Vector3 spawnPosition = spawnLocation.position;
        Quaternion spawnRotation = spawnLocation.rotation;

        // Instantiate minion prefab
        GameObject minionInstance = Instantiate(minionPrefab, spawnPosition, spawnRotation);

        // Get NetworkObject and spawn with ownership
        NetworkObject networkObject = minionInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            // networkObject.SpawnWithOwnership(clientId);
            networkObject.Spawn();

            var minionAI = minionInstance.GetComponent<MinionAI>();
            if (minionAI != null)
            {
                foreach (var kvp in spawnedPlayers)
                {
                    if (kvp.Key == clientId) continue;
                    minionAI.target = kvp.Value?.transform;
                    break;
                }
            }

            if (debugMode)
                Debug.Log($"Minion spawned for client {clientId} at {spawnPosition}");
        }
        else
        {
            Debug.LogError("Minion prefab must have a NetworkObject component!");
            Destroy(minionInstance);
        }
    }

    private void DespawnPlayerForClient(ulong clientId)
    {
        if (spawnedPlayers.TryGetValue(clientId, out GameObject playerInstance))
        {
            if (playerInstance != null)
            {
                NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                }
            }

            spawnedPlayers.Remove(clientId);

            if (debugMode)
                Debug.Log($"Player despawned for client {clientId}");
        }
    }

    private Transform GetNextSpawnLocation()
    {
        Transform spawnLocation = spawnLocations[currentSpawnIndex];
        currentSpawnIndex = (currentSpawnIndex + 1) % spawnLocations.Length;
        return spawnLocation;
    }

    // Public methods for external access
    public GameObject GetPlayerForClient(ulong clientId)
    {
        spawnedPlayers.TryGetValue(clientId, out GameObject player);
        return player;
    }

    public int GetSpawnedPlayerCount()
    {
        return spawnedPlayers.Count;
    }

    private bool HasValidSpawnPoints()
    {
        return spawnLocations != null && spawnLocations.Length > 0;
    }

    // Validation
    private void OnValidate()
    {
        if (playerPrefab != null && playerPrefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogWarning("Player prefab should have a NetworkObject component!");
        }
    }

    public Transform GetSpawnForIndex(int index)
    {
        if (spawnLocations == null || spawnLocations.Length == 0) return null;
        index = Mathf.Clamp(index, 0, spawnLocations.Length - 1);
        return spawnLocations[index];
    }

    public void RespawnAllPlayersAtSpawnsServer()
    {
        if (!IsServer) return;

        int idx = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (!spawnedPlayers.TryGetValue(client.ClientId, out var playerGO) || playerGO == null)
                continue;

            Transform sp = GetSpawnForIndex(idx);
            idx++;

            if (sp == null) continue;

            playerGO.transform.SetPositionAndRotation(sp.position, sp.rotation);

            // reset stats on server
            var stats = playerGO.GetComponent<PlayerStatsManager>();
            if (stats != null)
                stats.ResetForNewRoundServerRpc();

            var abilities = playerGO.GetComponent<AbilityRunner>();
            if (abilities != null) abilities.ResetForNewRoundServerRpc();

            var stun = playerGO.GetComponent<StunReceiver>();
            if (stun != null) stun.ResetForNewRoundServerRpc();

            var parry = playerGO.GetComponent<ParryReceiver>();
            if (parry != null) parry.ResetForNewRoundServerRpc();

            var buff = playerGO.GetComponent<BuffReceiver>();
            if (buff != null) buff.ResetForNewRoundServerRpc();

            var dr = playerGO.GetComponent<DamageReceiver>();
            if (dr != null) dr.ResetForNewRoundServerRpc();
            
            var tele = playerGO.GetComponent<PlayerTeleport>();
            if (tele != null)
            {
                var target = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { client.ClientId }
                    }
                };

                tele.TeleportOwnerClientRpc(sp.position, sp.rotation, target);
            }
            else
            {
                // fallback
                playerGO.transform.SetPositionAndRotation(sp.position, sp.rotation);
            }




        }
    }


}
