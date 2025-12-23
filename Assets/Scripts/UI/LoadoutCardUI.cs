using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadoutCardUI : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private Button button;
    [SerializeField] private Image abilityIcon;
    [SerializeField] private GameObject addGroup;          // contains plus + "Add" text
    [SerializeField] private TextMeshProUGUI addText;      // optional

    public void Init(System.Action onClick)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }

    public void SetEmpty()
    {
        if (abilityIcon != null)
        {
            abilityIcon.enabled = false;
            abilityIcon.sprite = null;
        }

        if (addGroup != null) addGroup.SetActive(true);
        if (addText != null) addText.text = "Add";
    }

    public void SetFilled(Sprite icon)
    {
        if (addGroup != null) addGroup.SetActive(false);

        if (abilityIcon != null)
        {
            abilityIcon.sprite = icon;
            abilityIcon.enabled = icon != null;
        }
    }
}
