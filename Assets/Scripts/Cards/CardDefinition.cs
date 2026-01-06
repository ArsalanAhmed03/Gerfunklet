using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Cards/Card Definition")]
public class CardDefinition : ScriptableObject
{
    public CardId id;
    public Sprite icon;
    public float atpCost = 2f;
    [TextArea] public string description;
}
