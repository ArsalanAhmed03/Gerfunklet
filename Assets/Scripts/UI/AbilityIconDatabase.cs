using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/UI/Ability Icon Database")]
public class AbilityIconDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public AbilityId id;
        public Sprite icon;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<AbilityId, Sprite> _map;

    public void Build()
    {
        _map = new Dictionary<AbilityId, Sprite>();
        foreach (var e in entries)
        {
            if (e == null) continue;
            if (_map.ContainsKey(e.id)) continue;
            _map.Add(e.id, e.icon);
        }
    }

    public Sprite GetIcon(AbilityId id)
    {
        if (_map == null) Build();
        return _map.TryGetValue(id, out var s) ? s : null;
    }

    public List<Entry> Entries => entries;
}
