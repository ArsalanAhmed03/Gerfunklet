using UnityEngine;

public class MinionStats : MonoBehaviour
{
    public enum Targeting
    {
        PlayersFirst,
        StructuresFirst
    }

    public enum Size
    {
        Small,
        Medium,
        Large
    }

    [Header("Stats")]
    [SerializeField] private int damage = 10;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackIntervalSeconds = 1f;
    [SerializeField] private bool destroyOnAttack = true;

    [Header("Targeting")]
    [SerializeField] private Targeting targeting = Targeting.PlayersFirst;

    [Header("Devour")]
    [SerializeField] private Size size = Size.Medium;
    [SerializeField] private bool devourable = true;

    public int Damage => damage;
    public float MoveSpeed => moveSpeed;
    public float AttackRange => attackRange;
    public float AttackIntervalSeconds => attackIntervalSeconds;
    public bool DestroyOnAttack => destroyOnAttack;
    public Targeting TargetingMode => targeting;
    public Size SizeCategory => size;
    public bool Devourable => devourable;
}
