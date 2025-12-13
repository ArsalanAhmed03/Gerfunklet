using UnityEngine;

public abstract class AbilityDefinition : ScriptableObject
{
    public AbilityId id;
    public float cooldownSeconds = 8f;

    // Server does real gameplay (damage, buffs, CC, spawn projectile)
    public abstract void ServerExecute(AbilityRunner runner);

    // Clients do visuals only (anim, VFX, SFX)
    public virtual void ClientExecute(AbilityRunner runner) { }
}
