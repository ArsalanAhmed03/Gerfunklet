using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Ability Definition Database")]
public class AbilityDefinitionDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public AbilityId id;
        public AbilityDefinition def;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    private Dictionary<AbilityId, AbilityDefinition> _map;

    public void Build()
    {
        _map = new Dictionary<AbilityId, AbilityDefinition>();
        foreach (var e in entries)
        {
            if (e == null || e.def == null) continue;
            if (_map.ContainsKey(e.id)) continue;
            _map.Add(e.id, e.def);
        }
    }

    public AbilityDefinition Get(AbilityId id)
    {
        if (_map == null) Build();
        return _map.TryGetValue(id, out var def) ? def : null;
    }
}
