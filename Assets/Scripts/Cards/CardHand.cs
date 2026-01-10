using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CardHand : NetworkBehaviour
{
    [Header("Deck/Hand Rules (GDD defaults)")]
    [SerializeField] private int deckSize = 8;
    [SerializeField] private int handSize = 4;
    [SerializeField] private int maxMulliganSwaps = 2;
    [SerializeField] private CardCatalog catalog;
    [SerializeField] private List<CardId> defaultDeck = new List<CardId>();

    public NetworkList<int> Deck = new NetworkList<int>(
        new List<int>(),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkList<int> Hand = new NetworkList<int>(
        new List<int>(),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public event Action OnHandChanged;

    private int _mulliganRemaining;

    public CardCatalog Catalog => catalog;

    public override void OnNetworkSpawn()
    {
        Hand.OnListChanged += HandleHandChanged;

        if (IsServer)
            ResetForNewMatchServer();
    }

    public override void OnNetworkDespawn()
    {
        Hand.OnListChanged -= HandleHandChanged;
    }

    public void ResetForNewMatchServer()
    {
        if (!IsServer) return;

        _mulliganRemaining = maxMulliganSwaps;
        BuildDeckServer();
        DrawInitialHandServer();
    }

    [ServerRpc]
    public void PlayCardServerRpc(int handIndex, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        var fallbackPosition = GetFallbackPlacementPositionServer();
        TryPlayCardServer(handIndex, fallbackPosition);
    }

    [ServerRpc]
    public void PlayCardAtServerRpc(int handIndex, Vector3 worldPosition, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        TryPlayCardServer(handIndex, worldPosition);
    }

    [ServerRpc]
    public void MulliganServerRpc(int[] handIndices, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (handIndices == null || handIndices.Length == 0) return;
        if (handIndices.Length > maxMulliganSwaps) return;
        if (handIndices.Length > _mulliganRemaining) return;

        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (MatchManager.Instance != null)
        {
            var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
            if (phase != MatchManager.MatchPhase.LoadoutSelect)
                return;
        }

        var seen = new HashSet<int>();
        foreach (var idx in handIndices)
        {
            if (!seen.Add(idx)) return;
            if (idx < 0 || idx >= Hand.Count) return;
        }

        foreach (var idx in handIndices)
        {
            var oldId = (CardId)Hand[idx];
            Hand[idx] = (int)DrawFromDeckServer();
            ReturnCardToDeckBottomServer(oldId);
        }

        _mulliganRemaining -= handIndices.Length;
    }

    private void TryPlayCardServer(int handIndex, Vector3 worldPosition)
    {
        if (handIndex < 0 || handIndex >= Hand.Count) return;

        var id = (CardId)Hand[handIndex];
        if (id == CardId.None) return;

        if (MatchManager.Instance != null)
        {
            var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
            if (phase != MatchManager.MatchPhase.Playing && phase != MatchManager.MatchPhase.Overtime)
                return;
        }

        CardDefinition def = null;
        float cost = 0f;
        if (catalog != null)
        {
            def = catalog.Get(id);
            if (def != null) cost = def.atpCost;
        }

        if (def == null || def.spawnPrefab == null)
        {
            Debug.LogWarning($"[CardHand][SERVER] Card {id} has no spawn prefab assigned.");
            return;
        }

        var rules = GetComponent<DeploymentRules>();
        if (rules == null)
        {
            Debug.LogWarning("[CardHand][SERVER] DeploymentRules missing on player; cannot validate placement.");
            return;
        }

        if (!rules.IsPlacementValid(worldPosition, out var reason))
        {
            if (!string.IsNullOrEmpty(reason))
                Debug.Log($"[CardHand][SERVER] Placement rejected: {reason}");
            return;
        }

        var atp = GetComponent<AtpResource>();
        if (atp != null && cost > 0f)
        {
            if (!atp.TrySpendServer(cost))
                return;
        }

        Debug.Log($"[CardHand][SERVER] Play card {id} by {OwnerClientId} cost={cost} at {worldPosition}");

        StartCoroutine(SpawnCardAfterWarmupServer(def, worldPosition));

        Hand[handIndex] = (int)DrawFromDeckServer();
        ReturnCardToDeckBottomServer(id);
    }

    private IEnumerator SpawnCardAfterWarmupServer(CardDefinition def, Vector3 position)
    {
        float warmup = Mathf.Max(0f, def.spawnWarmupSeconds);
        if (warmup > 0f)
            yield return new WaitForSeconds(warmup);

        var instance = Instantiate(def.spawnPrefab, position, Quaternion.identity);
        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();
        else
            Debug.LogWarning($"[CardHand][SERVER] Spawn prefab {def.spawnPrefab.name} has no NetworkObject; it will not replicate.");

        AssignMinionTargetIfPresent(instance);
    }

    private void AssignMinionTargetIfPresent(GameObject instance)
    {
        var minion = instance.GetComponent<MinionAI>();
        if (minion == null) return;

        var ownerTag = instance.GetComponent<MinionOwner>();
        if (ownerTag != null)
            ownerTag.SetOwnerServer(OwnerClientId);

        var enemyCitadel = FindEnemyCitadel(OwnerClientId);
        if (enemyCitadel != null && !enemyCitadel.destroyed.Value)
        {
            minion.target = enemyCitadel.transform;
            return;
        }

        if (NetworkManager.Singleton == null) return;
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            if (kvp.Key == OwnerClientId) continue;
            minion.target = kvp.Value.PlayerObject != null ? kvp.Value.PlayerObject.transform : null;
            if (minion.target != null) break;
        }
    }

    private CitadelHealth FindEnemyCitadel(ulong ownerClientId)
    {
        var citadels = FindObjectsOfType<CitadelHealth>(true);
        foreach (var c in citadels)
        {
            if (c == null) continue;
            if (c.ownerClientId.Value == ulong.MaxValue) continue;
            if (c.ownerClientId.Value == ownerClientId) continue;
            return c;
        }
        return null;
    }

    private void BuildDeckServer()
    {
        Deck.Clear();

        List<CardId> ids = new List<CardId>();

        if (defaultDeck != null && defaultDeck.Count > 0)
        {
            ids.AddRange(defaultDeck);
        }
        else if (catalog != null)
        {
            ids.AddRange(catalog.GetAllIds());
        }

        if (ids.Count == 0)
        {
            for (int i = 0; i < deckSize; i++)
                ids.Add(CardId.None);
        }
        else
        {
            var seed = new List<CardId>(ids);
            int idx = 0;
            while (ids.Count < deckSize)
            {
                ids.Add(seed[idx % seed.Count]);
                idx++;
            }
        }

        if (ids.Count > deckSize)
            ids.RemoveRange(deckSize, ids.Count - deckSize);

        Shuffle(ids);
        foreach (var id in ids)
            Deck.Add((int)id);
    }

    private void DrawInitialHandServer()
    {
        Hand.Clear();

        for (int i = 0; i < handSize; i++)
            Hand.Add((int)DrawFromDeckServer());
    }

    private CardId DrawFromDeckServer()
    {
        if (Deck.Count == 0) return CardId.None;
        var card = (CardId)Deck[0];
        Deck.RemoveAt(0);
        return card;
    }

    private void ReturnCardToDeckBottomServer(CardId id)
    {
        if (id == CardId.None) return;
        Deck.Add((int)id);
    }

    private Vector3 GetFallbackPlacementPositionServer()
    {
        var rules = GetComponent<DeploymentRules>();
        if (rules != null)
            return rules.GetAnchorPosition(out _);

        return transform.position;
    }

    private void Shuffle(List<CardId> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void HandleHandChanged(NetworkListEvent<int> changeEvent)
    {
        OnHandChanged?.Invoke();
    }

    public CardId GetHandCardId(int index)
    {
        if (index < 0 || index >= Hand.Count) return CardId.None;
        return (CardId)Hand[index];
    }
}
