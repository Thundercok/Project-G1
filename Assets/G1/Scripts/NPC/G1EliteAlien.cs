using UnityEngine;

/// Applied to an alien spawn to boost it to Elite stats.
/// Works as an additive patch on top of G1AlienAI and HealthSystem
/// already on the same GameObject — no separate prefab required.
[RequireComponent(typeof(G1AlienAI), typeof(HealthSystem))]
public class G1EliteAlien : MonoBehaviour
{
    [Header("Elite Stats")]
    public float eliteMaxHealth = 160f;
    public float eliteDamage    = 20f;
    public float eliteSpeed     = 2.8f;

    // Emissive teal material created at runtime — not modifying shared assets
    private Material[] _instanceMats;

    private void Awake()
    {
        // --- Boost HealthSystem
        var health = GetComponent<HealthSystem>();
        if (health != null)
        {
            // HealthSystem sets CurrentHealth = maxHealth in its own Awake.
            // If our Awake runs after HealthSystem.Awake we need to re-init.
            health.maxHealth = eliteMaxHealth;
            // Force re-init current health to new max via reflection-free approach:
            // TakeDamage(0) will call OnHealthChanged but not kill; we use Heal instead.
            health.Heal(eliteMaxHealth);
        }

        // --- Boost G1AlienAI
        var ai = GetComponent<G1AlienAI>();
        if (ai != null)
        {
            ai.damage = eliteDamage;
            ai.speed  = eliteSpeed;
            ai.detectionRange = 22f; // spot player sooner
        }

        // --- Boost NavMeshAgent
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.speed        = eliteSpeed;
            agent.acceleration = 20f;
        }

        // --- Apply intense neon teal emission to distinguish visually
        var renderers = GetComponentsInChildren<Renderer>();
        _instanceMats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var mat = new Material(renderers[i].sharedMaterial);
            // Saturated teal body
            mat.color = new Color(0.05f, 0.85f, 0.80f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0f, 0.40f, 0.38f));
            mat.SetFloat("_Glossiness", 0.05f);
            renderers[i].sharedMaterial = mat;
            _instanceMats[i] = mat;
        }
    }

    private void Update()
    {
        // Subtle pulse on emission intensity so elite reads as different at a glance
        float pulse = 0.38f + Mathf.Sin(Time.time * 3.5f) * 0.12f;
        if (_instanceMats != null)
        {
            foreach (var mat in _instanceMats)
            {
                if (mat != null)
                    mat.SetColor("_EmissionColor", new Color(0f, pulse, pulse * 0.95f));
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up runtime materials
        if (_instanceMats != null)
            foreach (var mat in _instanceMats)
                if (mat != null) Destroy(mat);
    }
}
