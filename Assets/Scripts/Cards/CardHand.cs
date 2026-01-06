using System;
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

        if (handIndex < 0 || handIndex >= Hand.Count) return;

        var id = (CardId)Hand[handIndex];
        if (id == CardId.None) return;

        if (MatchManager.Instance != null)
        {
            var phase = (MatchManager.MatchPhase)MatchManager.Instance.Phase.Value;
            if (phase != MatchManager.MatchPhase.Playing && phase != MatchManager.MatchPhase.Overtime)
                return;
        }

        float cost = 0f;
        if (catalog != null)
        {
            var def = catalog.Get(id);
            if (def != null) cost = def.atpCost;
        }

        var atp = GetComponent<AtpResource>();
        if (atp != null && cost > 0f)
        {
            if (!atp.TrySpendServer(cost))
                return;
        }

        Debug.Log($"[CardHand][SERVER] Play card {id} by {OwnerClientId} cost={cost}");

        Hand[handIndex] = (int)DrawFromDeckServer();
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
            Hand[idx] = (int)DrawFromDeckServer();
        }

        _mulliganRemaining -= handIndices.Length;
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
