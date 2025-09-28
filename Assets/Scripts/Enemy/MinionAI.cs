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

        if (!IsOwner) return;

        GetComponent<Animator>()?.SetBool("isWalking", true);
    }

    private void Update()
    {
        // if (!IsOwner) return;

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
        target.GetComponent<PlayerStatsManager>()?.TakeDamageServerRpc(damage);
        Destroy(gameObject);
    }
}
