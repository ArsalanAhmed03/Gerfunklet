using UnityEngine;

[CreateAssetMenu(menuName = "Gerfunklet/Cards/Card Definition")]
public class CardDefinition : ScriptableObject
{
    public CardId id;
    public Sprite icon;
    public float atpCost = 2f;
    public GameObject spawnPrefab;
    public float spawnWarmupSeconds = 0f;
    [TextArea] public string description;
}
