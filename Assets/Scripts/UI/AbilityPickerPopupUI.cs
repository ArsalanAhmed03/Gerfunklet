using System;
using UnityEngine;
using UnityEngine.UI;

public class AbilityPickerPopupUI : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private GameObject root;
    [SerializeField] private Transform gridRoot;
    [SerializeField] private AbilityPickItemUI itemPrefab;
    [SerializeField] private Button closeButton;

    private AbilityIconDatabase _db;
    private Action<AbilityId> _onPicked;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    public void Show(AbilityIconDatabase db, Action<AbilityId> onPicked)
    {
        _db = db;
        _onPicked = onPicked;

        if (_db != null) _db.Build();

        BuildGrid();
        if (root != null) root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        _onPicked = null;
    }

    private void BuildGrid()
    {
        if (gridRoot == null || itemPrefab == null || _db == null) return;

        for (int i = gridRoot.childCount - 1; i >= 0; i--)
            Destroy(gridRoot.GetChild(i).gameObject);

        foreach (var e in _db.Entries)
        {
            if (e == null) continue;

            var item = Instantiate(itemPrefab, gridRoot);
            item.Bind(e.id, e.icon, OnPickClicked);
        }
    }

    private void OnPickClicked(AbilityId id)
    {
        _onPicked?.Invoke(id);
        Hide();
    }
}
