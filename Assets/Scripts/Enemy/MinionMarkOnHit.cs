using Unity.Netcode;
using UnityEngine;

public class MinionMarkOnHit : NetworkBehaviour
{
    [SerializeField] private float chance = 0.25f;
    [SerializeField] private float durationSeconds = 3f;
    [SerializeField] private float damageMultiplier = 1.1f;

    public void NotifyHit(Transform target)
    {
        if (!IsServer) return;
        if (target == null) return;

        if (Random.value > chance)
            return;

        var amp = target.GetComponentInParent<DamageAmplifierReceiver>();
        if (amp != null)
            amp.ApplyDamageAmplifierServerRpc(damageMultiplier, durationSeconds);
    }
}
