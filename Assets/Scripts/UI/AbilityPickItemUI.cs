using UnityEngine;
using UnityEngine.UI;

public class AbilityPickItemUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;
    [SerializeField] private CanvasGroup canvasGroup; // optional (for greying)

    private AbilityId _id;
    private System.Action<AbilityId> _onClick;

    public void Bind(AbilityId id, Sprite icon, bool disabled, System.Action<AbilityId> onClick)
    {
        _id = id;
        _onClick = onClick;

        if (iconImage != null) iconImage.sprite = icon;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = !disabled;
            if (!disabled)
                button.onClick.AddListener(() => _onClick?.Invoke(_id));
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = disabled ? 0.35f : 1f;
            canvasGroup.interactable = !disabled;
            canvasGroup.blocksRaycasts = !disabled;
        }
        else if (iconImage != null)
        {
            var c = iconImage.color;
            iconImage.color = disabled ? new Color(c.r, c.g, c.b, 0.35f) : new Color(c.r, c.g, c.b, 1f);
        }
    }
}
