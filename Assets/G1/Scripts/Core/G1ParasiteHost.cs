using UnityEngine;

/// The thing riding the robot.
///
/// A parasitised chassis is armoured everywhere except the animal on its
/// shoulders, and that asymmetry is the entire fight. Emptying a magazine into
/// the plating barely works; four rounds into the sac drops it. The player is
/// never told this — the parasite glows, it is the only organic thing on an
/// otherwise grey machine, and it is placed where the eye already goes.
///
/// Mechanically this is a second collider on the host with its own health,
/// registered as damageable in its own right. Shots that land on it are routed
/// here rather than to the chassis, and killing it kills the host.
[RequireComponent(typeof(SphereCollider))]
public sealed class G1ParasiteHost : MonoBehaviour, IDamageable
{
    [Tooltip("The chassis this parasite is driving.")]
    public HealthSystem host;

    [Tooltip("How much punishment the parasite itself takes before it dies.")]
    public float health = 34f;

    [Tooltip("Multiplier applied to incoming damage. This is the weak point.")]
    public float damageMultiplier = 3.0f;

    public Renderer glow;
    public Light halo;

    float max;
    float pulse;
    bool dead;

    void Awake()
    {
        max = health;
        var col = GetComponent<SphereCollider>();
        col.isTrigger = false;                 // it has to stop a bullet
        if (halo == null)
        {
            var go = new GameObject("ParasiteGlow");
            go.transform.SetParent(transform, false);
            halo = go.AddComponent<Light>();
            halo.type = LightType.Point;
            halo.color = new Color(0.42f, 0.95f, 0.20f);
            halo.range = 5.5f;
            halo.intensity = 1.5f;
            halo.shadows = LightShadows.None;
        }
    }

    void Update()
    {
        if (dead || halo == null) return;
        // A slow breath rather than a steady lamp. It is the difference
        // between "a green light on a robot" and "there is something alive on
        // that robot", and it costs one sine.
        pulse += Time.deltaTime * 2.4f;
        float k = health / Mathf.Max(0.01f, max);
        halo.intensity = (1.15f + Mathf.Sin(pulse) * 0.35f) * Mathf.Lerp(0.5f, 1.4f, k);
        halo.range = Mathf.Lerp(3.2f, 6.0f, k);
    }

    public void TakeDamage(float amount, Vector3 point, Vector3 normal)
    {
        if (dead) return;
        health -= amount * damageMultiplier;

        G1Audio.Play("hit_thunk", point, 1.6f, 0.5f);
        if (glow != null)
        {
            // flash toward white on a hit, so a landed shot is unmistakable
            var m = glow.material;
            m.color = Color.Lerp(new Color(0.30f, 0.46f, 0.13f), Color.white, 0.7f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", Color.white * 2f);
                m.EnableKeyword("_EMISSION");
            }
        }

        if (health > 0f) return;

        dead = true;
        if (halo != null) halo.enabled = false;
        // The host is a machine with nobody driving it. Killing it outright,
        // rather than leaving it standing with zero health, is what makes the
        // weak point *feel* like a weak point.
        if (host != null)
            host.TakeDamage(99999f, transform.position, Vector3.up);
        G1Audio.Play("enemy_death", transform.position, 0.7f, 0.9f);
        Destroy(gameObject, 0.1f);
    }
}
