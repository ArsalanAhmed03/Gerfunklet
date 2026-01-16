using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Cards/Card Catalog")]
public class CardCatalog : ScriptableObject
{
    [SerializeField] private List<CardDefinition> cards = new List<CardDefinition>();

    private Dictionary<CardId, CardDefinition> _map;

    public void Build()
    {
        _map = new Dictionary<CardId, CardDefinition>();
        foreach (var card in cards)
        {
            if (card == null) continue;
            if (_map.ContainsKey(card.id)) continue;
            _map.Add(card.id, card);
        }
    }

    public CardDefinition Get(CardId id)
    {
        if (_map == null) Build();
        return _map.TryGetValue(id, out var def) ? def : null;
    }

    public List<CardId> GetAllIds()
    {
        if (_map == null) Build();
        return new List<CardId>(_map.Keys);
    }
}
