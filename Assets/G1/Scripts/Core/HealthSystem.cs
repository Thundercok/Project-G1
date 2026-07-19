using System;
using UnityEngine;

/// Generic health container. Other components react through the events:
/// Breakable shatters on OnDeath, WorldSpaceHealthBar redraws on OnHealthChanged.
public class HealthSystem : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    public bool godMode = false;

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
        if (IsDead || (CompareTag("Player") && godMode))
            return;
        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0f);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        var camFx = GetComponentInChildren<CameraEffects>();
        if (camFx)
        {
            camFx.ShowDamageFlash();
            G1Audio.Play2D("player_hurt", 0.6f);
        }
        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            if (!camFx)
                G1Audio.Play(GetComponent<Breakable>() ? "crate_break" : "enemy_death",
                             transform.position, 0.8f);
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

    void Update()
    {
        if (CompareTag("Player"))
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                godMode = !godMode;
                // Play confirmation beep sound
                G1Audio.Play2D("pickup", 0.8f, godMode ? 1.6f : 0.9f);
                Debug.Log($"[GOD MODE] {(godMode ? "ENABLED" : "DISABLED")}");
                OnHealthChanged?.Invoke(CurrentHealth, maxHealth); // Refresh HUD
            }
            if (Input.GetKeyDown(KeyCode.H))
            {
                Heal(100f);
                var switcher = GetComponentInChildren<WeaponSwitcher>();
                if (switcher != null && switcher.weapons != null)
                {
                    // Unlock all weapons
                    if (switcher.unlocked != null)
                    {
                        for (int i = 0; i < switcher.unlocked.Length; i++)
                            switcher.unlocked[i] = true;
                    }
                    // Refill all ammunition
                    foreach (var wObj in switcher.weapons)
                    {
                        var w = wObj.GetComponent<WeaponBase>();
                        if (w == null) continue;
                        if (w is G1Pistol p) { p.clip = p.clipSize; p.reserve = 68; }
                        else if (w is G1Smg s) { s.clip = s.clipSize; s.reserve = 150; }
                        else if (w is G1Shotgun sh) { sh.clip = sh.clipSize; sh.reserve = 32; }
                        else if (w is G1Magnum m) { m.clip = m.clipSize; m.reserve = 24; }
                    }
                }
                // Play tech servo refill sound
                G1Audio.Play2D("door_servo", 0.6f, 1.8f);
                Debug.Log("[CHEAT] Restored Health, Unlocked and Refilled All Weapons!");
            }
        }
    }
}
