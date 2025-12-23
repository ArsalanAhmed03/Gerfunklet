using UnityEngine;
using UnityEngine.UI;

public class AbilityPickItemUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;

    private AbilityId _id;
    private System.Action<AbilityId> _onClick;

    public void Bind(AbilityId id, Sprite icon, System.Action<AbilityId> onClick)
    {
        _id = id;
        _onClick = onClick;

        if (iconImage != null) iconImage.sprite = icon;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _onClick?.Invoke(_id));
        }
    }
}
