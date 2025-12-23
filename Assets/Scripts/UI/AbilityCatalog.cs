using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Ability Catalog")]
public class AbilityCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public AbilityId id;
        public AbilityDefinition definition;
        public Sprite icon;
    }

    [SerializeField] public List<Entry> entries = new List<Entry>();

    private Dictionary<AbilityId, Entry> _map;

    public void Build()
    {
        _map = new Dictionary<AbilityId, Entry>();

        foreach (var e in entries)
        {
            if (e == null) continue;
            if (e.definition == null) continue;
            if (_map.ContainsKey(e.id)) continue;

            _map.Add(e.id, e);
        }
    }

    public AbilityDefinition GetDefinition(AbilityId id)
    {
        if (_map == null) Build();
        return _map.TryGetValue(id, out var e) ? e.definition : null;
    }

    public Sprite GetIcon(AbilityId id)
    {
        if (_map == null) Build();
        return _map.TryGetValue(id, out var e) ? e.icon : null;
    }
}
