using Unity.Netcode;
using UnityEngine;

public class AbilityRunner : NetworkBehaviour
{
    [Header("Assign in Inspector")]
    public AbilityDefinition slot1; // Stomp for now
    private float _serverSlot1ReadyAt;

    public AbilityDefinition slot2;
    private float _serverSlot2ReadyAt;

    public AbilityDefinition slot3; // Parry
    private float _serverSlot3ReadyAt;

    public AbilityDefinition slot4; // Throw
    public NetworkProjectile projectilePrefab; // assign in Inspector
    private float _serverSlot4ReadyAt;

    // Called by input on the owning client
    public void TryCastSlot1()
    {
        if (!IsOwner) return;
        if (slot1 == null) { Debug.LogError("AbilityRunner: slot1 not assigned"); return; }

        // Ask server to execute
        CastStompServerRpc();
    }

    [ServerRpc]
    private void CastStompServerRpc(ServerRpcParams rpcParams = default)
    {

        if (slot1 == null) return;

        if (Time.time < _serverSlot1ReadyAt)
            return;

        _serverSlot1ReadyAt = Time.time + slot1.cooldownSeconds;

        // Simple AoE around caster
        float radius = slot1.radius;
        int damage = Mathf.RoundToInt(slot1.damage);

        Vector3 center = transform.position;

        // Only hit players (for now). Put players on a "Player" layer.
        int playerMask = LayerMask.GetMask("Player");
        Collider[] hits = Physics.OverlapSphere(center, radius, playerMask, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            if (col.transform == transform) continue;

            var targetStats = col.GetComponentInParent<PlayerStatsManager>();
            if (targetStats == null) continue;

            var targetParry = col.GetComponentInParent<ParryReceiver>();

            // Parry SUCCESS
            if (targetParry != null && targetParry.IsParryActive)
            {
                // Stun the attacker instead
                var attackerStun = GetComponent<StunReceiver>();
                if (attackerStun != null)
                    attackerStun.ApplyStunServerRpc(4f);

                continue; // no damage to defender
            }

            // Normal stomp hit
            targetStats.TakeDamageServerRpc(damage);

            var targetStun = col.GetComponentInParent<StunReceiver>();
            if (targetStun != null)
                targetStun.ApplyStunServerRpc(slot1.stunSeconds);
        }


        // Tell everyone to play FX (cosmetic)
        PlayStompFxClientRpc(center, radius);
    }

    [ClientRpc]
    private void PlayStompFxClientRpc(Vector3 center, float radius)
    {
        // For now just log. Next step we add VFX + animation trigger.
        Debug.Log($"STOMP FX at {center} r={radius:0.0}");
        gameObject.GetComponent<PlayerMovement>()?.playerAnimator?.Stomp();
    }

    // Optional: draw the stomp radius in editor
    private void OnDrawGizmosSelected()
    {
        if (slot1 == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, slot1.radius);
    }

    public void TryCastSlot2()
    {
        if (!IsOwner) return;
        if (slot2 == null) { Debug.LogError("AbilityRunner: slot2 not assigned"); return; }

        // Ask server to execute
        CastRallyServerRpc();
    }

    [ServerRpc]
    private void CastRallyServerRpc()
    {

        if (Time.time < _serverSlot2ReadyAt)
            return;

        _serverSlot2ReadyAt = Time.time + slot2.cooldownSeconds;

        float radius = slot2.radius;
        float duration = 10f;
        float speedBoost = 2f;

        int playerMask = LayerMask.GetMask("Player");
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, playerMask);

        foreach (var col in hits)
        {
            var buff = col.GetComponentInParent<BuffReceiver>();
            if (buff != null)
            {
                buff.ApplyMoveSpeedBuffServerRpc(speedBoost, duration);
            }
        }

        PlayRallyFxClientRpc(transform.position, radius);
    }

    [ClientRpc]
    private void PlayRallyFxClientRpc(Vector3 center, float radius)
    {
        Debug.Log("RALLY FX");
    }

    [ServerRpc]
    public void CastParryServerRpc()
    {
        if (slot3 == null) return;

        if (Time.time < _serverSlot3ReadyAt)
            return;

        _serverSlot3ReadyAt = Time.time + slot3.cooldownSeconds;

        var parry = GetComponent<ParryReceiver>();
        if (parry != null)
            parry.ActivateParryServerRpc(3f);

        PlayParryFxClientRpc();
    }

    [ClientRpc]
    private void PlayParryFxClientRpc()
    {
        Debug.Log("PARRY FX");
    }

    [ServerRpc]
    public void CastThrowServerRpc()
    {
        if (slot4 == null) return;
        if (projectilePrefab == null) { Debug.LogError("AbilityRunner: projectilePrefab not assigned"); return; }

        if (Time.time < _serverSlot4ReadyAt)
            return;

        _serverSlot4ReadyAt = Time.time + slot4.cooldownSeconds;

        // Spawn slightly in front of player
        Vector3 spawnPos = transform.position + transform.forward * 1.2f + Vector3.up * 0.8f;
        Quaternion rot = Quaternion.LookRotation(transform.forward, Vector3.up);

        var proj = Instantiate(projectilePrefab, spawnPos, rot);
        proj.GetComponent<NetworkObject>().Spawn(true);

        // Initialize movement on server
        proj.InitServer(transform.forward, OwnerClientId);

        PlayThrowFxClientRpc(spawnPos);
    }

    [ClientRpc]
    private void PlayThrowFxClientRpc(Vector3 spawnPos)
    {
        Debug.Log("THROW FX");
    }



}
