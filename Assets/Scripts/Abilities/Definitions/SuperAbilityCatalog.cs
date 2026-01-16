using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Abilities/Super Ability Catalog")]
public class SuperAbilityCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SuperChoice choice;
        public SuperAbilityDefinition definition;
        public Sprite icon;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<SuperChoice, Entry> _map;

    public void Build()
    {
        _map = new Dictionary<SuperChoice, Entry>();

        foreach (var e in entries)
        {
            if (e == null) continue;
            if (e.definition == null) continue;
            if (_map.ContainsKey(e.choice)) continue;

            _map.Add(e.choice, e);
        }
    }

    public SuperAbilityDefinition GetDefinition(SuperChoice choice)
    {
        if (_map == null) Build();
        return _map.TryGetValue(choice, out var e) ? e.definition : null;
    }

    public Sprite GetIcon(SuperChoice choice)
    {
        if (_map == null) Build();
        return _map.TryGetValue(choice, out var e) ? e.icon : null;
    }
}
