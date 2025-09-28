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
        if (IsHost)
        {
            Debug.Log("LocalSpawner initialized on Server");

            // SpawnPlayerForClient(NetworkManager.Singleton.LocalClientId);
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                SpawnPlayerForClient(client.ClientId);
            }

            // Subscribe to client connection events
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            // Spawn player for host immediately
        }
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
        // Validation checks
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned!");
            return;
        }

        if (spawnLocations == null || spawnLocations.Length == 0)
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
            networkObject.SpawnWithOwnership(clientId);
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
        // Validation checks
        if (minionPrefab == null)
        {
            Debug.LogError("Minion prefab is not assigned!");
            return;
        }

        Transform spawnLocation = spawnedPlayers[clientId]?.transform;
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
            if (NetworkManager.Singleton.LocalClientId == clientId)
            {
                foreach (var kvp in spawnedPlayers)
                {
                    if (kvp.Key != clientId)
                    {
                        minionAI.target = kvp.Value?.transform;
                        break;
                    }
                }
            }
            else
            {
                minionAI.target = spawnedPlayers[NetworkManager.Singleton.LocalClientId]?.transform;
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

    // Validation
    private void OnValidate()
    {
        if (playerPrefab != null && playerPrefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogWarning("Player prefab should have a NetworkObject component!");
        }
    }
}
