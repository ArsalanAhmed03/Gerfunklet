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
    public float attackIntervalSeconds = 1f;
    [SerializeField] private bool destroyOnAttack = true;
    private float _nextAttackTime;

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
            if (Time.time >= _nextAttackTime)
                AttackTarget();
        }
    }

    private void AttackTarget()
    {
        var parry = target.GetComponent<ParryReceiver>();
        if (parry != null && parry.IsParryActive)
        {
            var selfStun = GetComponent<StunReceiver>();
            if (selfStun != null)
                selfStun.ApplyStunServerRpc(0.4f);

            _nextAttackTime = Time.time + attackIntervalSeconds;
            return;
        }

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
        float attackSpeedMul = 1f;
        var buff = GetComponent<BuffReceiver>();
        if (buff != null)
            attackSpeedMul = Mathf.Max(0.1f, buff.AttackSpeedMultiplier);

        _nextAttackTime = Time.time + (attackIntervalSeconds / attackSpeedMul);

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
        attackIntervalSeconds = stats.AttackIntervalSeconds;
        destroyOnAttack = stats.DestroyOnAttack;
    }
}
