using UnityEngine;

/// Easy-mode quality of life: after 4 s without taking damage, slowly
/// regenerate up to the difficulty's regen ceiling (60 HP on Easy).
[RequireComponent(typeof(HealthSystem))]
public sealed class G1PlayerRegen : MonoBehaviour
{
    public float delayAfterHit = 4f;
    public float regenPerSecond = 3f;

    HealthSystem health;
    float lastHitAt;

    void Awake()
    {
        health = GetComponent<HealthSystem>();
        health.OnHealthChanged += (cur, max) => lastHitAt = Time.time;
    }

    void Update()
    {
        float ceiling = G1Difficulty.RegenCeiling;
        if (ceiling <= 0f || health.IsDead)
            return;
        if (Time.time - lastHitAt < delayAfterHit)
            return;
        if (health.CurrentHealth < ceiling)
            health.Heal(regenPerSecond * Time.deltaTime);
    }
}
