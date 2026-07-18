using System;
using UnityEngine;

/// Generic health container. Other components react through the events:
/// Breakable shatters on OnDeath, WorldSpaceHealthBar redraws on OnHealthChanged.
public class HealthSystem : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    /// (currentHealth, maxHealth)
    public event Action<float, float> OnHealthChanged;
    /// (hitPoint, hitNormal) of the killing blow
    public event Action<Vector3, Vector3> OnDeath;

    void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead)
            return;
        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0f);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        GetComponentInChildren<CameraEffects>()?.ShowDamageFlash();
        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            OnDeath?.Invoke(hitPoint, hitNormal);
        }
    }

    public void Heal(float amount)
    {
        if (IsDead)
            return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}
