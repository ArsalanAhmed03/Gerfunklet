using Unity.Netcode;
using UnityEngine;

public class MinionAI : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public Transform target; // usually enemy base or nearest enemy

    [Header("Combat")]
    public int damage = 10;
    public float attackRange = 1.5f;
    [SerializeField] private bool destroyOnAttack = true;

    private void Start()
    {
        // if (!IsOwner) return;

        // if (target == null)
        // {
        //     foreach (Transform child in GameManager.Instance.playerSpawns)
        //     {
        //         var stats = child.GetComponent<PlayerStatsManager>();
        //         if (stats != null && !stats.IsOwnedByLocalPlayer())
        //         {
        //             target = child;
        //             break;
        //         }
        //     }
        // }

        ApplyStatsOverrides();

        if (!IsOwner) return;

        GetComponent<Animator>()?.SetBool("isWalking", true);
    }

    private void Update()
    {
        if (!IsServer) return;

        if (target == null) return;

        // Move towards target
        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        // Face target
        transform.forward = direction;

        // Check attack range
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= attackRange)
        {
            AttackTarget();
        }
    }

    private void AttackTarget()
    {
        Debug.Log($"{gameObject.name} attacks {target.name} for {damage} damage!");
        var citadel = target.GetComponent<CitadelHealth>();
        if (citadel != null)
        {
            citadel.ApplyDamageServer(damage);
        }
        else
        {
            target.GetComponent<PlayerStatsManager>()?.TakeDamageServerRpc(damage);
        }
        if (destroyOnAttack)
            Destroy(gameObject);
    }

    private void ApplyStatsOverrides()
    {
        var stats = GetComponent<MinionStats>();
        if (stats == null) return;

        damage = stats.Damage;
        moveSpeed = stats.MoveSpeed;
        attackRange = stats.AttackRange;
        destroyOnAttack = stats.DestroyOnAttack;
    }
}
