using UnityEngine;

/// A resonance emitter anchoring the Threshold ring. These are what keep the
/// loop stable. The player can either step through the ring (the Auditor's
/// preferred ending) or turn the crowbar on the emitters and collapse it.
///
/// Each emitter is crowbar-destructible via HealthSystem/IDamageable (same path
/// as a Breakable crate). When the last emitter in the scene dies, the collapse
/// ending fires.
[RequireComponent(typeof(HealthSystem))]
public sealed class G1ResonanceEmitter : MonoBehaviour
{
    static int total;
    static int alive;
    static bool collapseFired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { total = 0; alive = 0; collapseFired = false; }

    /// True when at least one emitter exists in the scene (so the ending knows
    /// the collapse path is available at all).
    public static bool AnyPresent => total > 0;

    Renderer rend;
    Material mat;
    Color baseEmissive = new Color(0.16f, 0.75f, 0.75f);
    float pulse;

    void Awake()
    {
        total++;
        alive++;
        var health = GetComponent<HealthSystem>();
        health.OnDeath += OnEmitterDestroyed;
    }

    void Start()
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            mat = rend.material;                 // instance so pulsing is per-emitter
            if (mat.HasProperty("_EmissionColor"))
                mat.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        // Unstable teal throb telegraphs "this is load-bearing — hit it."
        if (mat == null) return;
        pulse += Time.deltaTime * 4f;
        float k = 1.6f + Mathf.Sin(pulse) * 0.8f;
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", baseEmissive * k);
    }

    void OnEmitterDestroyed(Vector3 point, Vector3 normal)
    {
        alive = Mathf.Max(0, alive - 1);

        // Discharge flash.
        G1Audio.Play("explosion", transform.position, 0.7f, 1.4f, 0.1f);
        var flash = new GameObject("EmitterDischarge");
        flash.transform.position = transform.position;
        var l = flash.AddComponent<Light>();
        l.color = baseEmissive;
        l.range = 10f;
        l.intensity = 6f;
        Destroy(flash, 0.35f);

        if (alive <= 0 && total > 0 && !collapseFired)
        {
            collapseFired = true;
            G1EndingCutscene.TriggerCollapse(point);
        }
    }
}
