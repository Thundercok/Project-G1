using UnityEngine;

/// Minimal death reaction: remove the object when its HealthSystem dies.
/// Placeholder until real death animations/ragdolls exist.
[RequireComponent(typeof(HealthSystem))]
public class DeathDespawn : MonoBehaviour
{
    public float delay = 0.05f;

    void Awake()
    {
        GetComponent<HealthSystem>().OnDeath += (p, n) => Destroy(gameObject, delay);
    }
}
