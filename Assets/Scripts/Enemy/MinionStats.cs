using UnityEngine;

public class MinionStats : MonoBehaviour
{
    public enum Role
    {
        Grunt,
        Harvester,
        Brute,
        Spewer,
        Scout,
        Acolyte
    }

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
    [SerializeField] private Role role = Role.Grunt;
    [SerializeField] private int damage = 10;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackIntervalSeconds = 1f;
    [SerializeField] private bool destroyOnAttack = true;

    [Header("Targeting")]
    [SerializeField] private Targeting targeting = Targeting.PlayersFirst;

    [Header("AoE (Spewer-like)")]
    [SerializeField] private bool useAoeAttack = false;
    [SerializeField] private int aoeDamage = 40;
    [SerializeField] private float aoeRadius = 1.8f;
    [SerializeField] private int aoeThreshold = 3;

    [Header("Healer (Acolyte-like)")]
    [SerializeField] private bool canHealAllies = false;
    [SerializeField] private int healAmount = 25;
    [SerializeField] private float healIntervalSeconds = 3f;
    [SerializeField] private float healRange = 3f;
    [SerializeField] private float healBelowPercent = 0.5f;

    [Header("Devour")]
    [SerializeField] private Size size = Size.Medium;
    [SerializeField] private bool devourable = true;

    public Role RoleType => role;
    public int Damage => damage;
    public float MoveSpeed => moveSpeed;
    public float AttackRange => attackRange;
    public float AttackIntervalSeconds => attackIntervalSeconds;
    public bool DestroyOnAttack => destroyOnAttack;
    public Targeting TargetingMode => targeting;
    public bool UseAoeAttack => useAoeAttack;
    public int AoeDamage => aoeDamage;
    public float AoeRadius => aoeRadius;
    public int AoeThreshold => aoeThreshold;
    public bool CanHealAllies => canHealAllies;
    public int HealAmount => healAmount;
    public float HealIntervalSeconds => healIntervalSeconds;
    public float HealRange => healRange;
    public float HealBelowPercent => healBelowPercent;
    public Size SizeCategory => size;
    public bool Devourable => devourable;
}
