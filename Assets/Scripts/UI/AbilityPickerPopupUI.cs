using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AbilityPickerPopupUI : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private GameObject root;
    [SerializeField] private Transform gridRoot;
    [SerializeField] private AbilityPickItemUI itemPrefab;
    [SerializeField] private Button closeButton;

    private AbilityCatalog _db;
    private Action<AbilityId> _onPicked;

    private HashSet<AbilityId> _disabledIds;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    public void Show(AbilityCatalog db, HashSet<AbilityId> disabledIds, Action<AbilityId> onPicked)
    {
        _db = db;
        _disabledIds = disabledIds;
        _onPicked = onPicked;

        if (_db != null) _db.Build();

        BuildGrid();
        if (root != null) root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        _onPicked = null;
        _disabledIds = null;
    }

    private void BuildGrid()
    {
        if (gridRoot == null || itemPrefab == null || _db == null) return;

        for (int i = gridRoot.childCount - 1; i >= 0; i--)
            Destroy(gridRoot.GetChild(i).gameObject);

        foreach (var e in _db.entries)
        {
            if (e == null) continue;

            bool disabled = _disabledIds != null && _disabledIds.Contains(e.id);

            var item = Instantiate(itemPrefab, gridRoot);
            item.Bind(
                e.id,
                e.icon,
                disabled,
                OnPickClicked
            );
        }
    }

    private void OnPickClicked(AbilityId id)
    {
        _onPicked?.Invoke(id);
        Hide();
    }
}
