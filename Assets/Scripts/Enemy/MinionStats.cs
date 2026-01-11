using UnityEngine;

public class MinionStats : MonoBehaviour
{
    public enum Targeting
    {
        PlayersFirst,
        StructuresFirst
    }

    [Header("Stats")]
    [SerializeField] private int damage = 10;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private bool destroyOnAttack = true;

    [Header("Targeting")]
    [SerializeField] private Targeting targeting = Targeting.PlayersFirst;

    public int Damage => damage;
    public float MoveSpeed => moveSpeed;
    public float AttackRange => attackRange;
    public bool DestroyOnAttack => destroyOnAttack;
    public Targeting TargetingMode => targeting;
}
