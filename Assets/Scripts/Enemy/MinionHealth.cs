using UnityEngine;
using Unity.Netcode;

public class MinionHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;
    public int CurrentHealth => currentHealth;
    public float Health01 => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        int dmg = amount;

        var dr = GetComponent<DamageReceiver>();
        if (dr != null)
            dmg = Mathf.CeilToInt(dmg * dr.DamageMultiplier);

        var amp = GetComponent<DamageAmplifierReceiver>();
        if (amp != null)
            dmg = Mathf.CeilToInt(dmg * amp.DamageMultiplier);

        currentHealth -= dmg;
        Debug.Log($"{gameObject.name} took {dmg} damage! HP left: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died!");
        var burst = GetComponent<MinionVolatileBurst>();
        if (burst != null)
            burst.HandleDeath();
        var dropper = GetComponent<FoodDropper>();
        if (dropper != null)
            dropper.DropServer();

        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            no.Despawn(true);
        else
            Destroy(gameObject);
    }
}
