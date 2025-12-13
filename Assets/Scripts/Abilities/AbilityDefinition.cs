using UnityEngine;

public enum AbilityId
{
    Stomp = 0,
    Rally = 1,
    Parry = 2,
    Throw = 3
}

[CreateAssetMenu(menuName = "Gerfunklet/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    public AbilityId id;
    public float cooldownSeconds = 8f;

    [Header("Stomp example values")]
    public float radius = 2.6f;
    public float damage = 160f;
    public float stunSeconds = 0.25f;
}
