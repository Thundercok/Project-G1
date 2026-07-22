using System.Collections.Generic;
using UnityEngine;

/// Level 2 airborne boss: a HECU gunship that strafes over the courtyard,
/// rakes the player with its machine gun, and fires 3-rocket salvos. Its rotor
/// is the weak point (HealthSystem); on death it trails smoke and crashes.
[RequireComponent(typeof(HealthSystem))]
public sealed class G1HelicopterBoss : MonoBehaviour
{
    [Header("Flight")]
    public float altitude = 12f;
    public float strafeWidth = 18f;
    public float strafeSpeed = 0.4f;
    public Vector3 arenaCenter = new Vector3(6f, 0f, 0f);

    [Header("Machine gun")]
    public float mgInterval = 0.14f;
    public float mgDamage = 4f;
    public float mgRange = 60f;

    [Header("Rockets")]
    public float salvoInterval = 6f;
    public float rocketSpeed = 18f;

    HealthSystem health;
    Transform player;
    float mgTimer, salvoTimer, phase;
    bool dead;
    static readonly RaycastHit[] hitBuf = new RaycastHit[8];

    void Start()
    {
        health = GetComponent<HealthSystem>();
        health.OnDeath += (p, n) => Crash();
        var pgo = GameObject.FindWithTag("Player");
        if (pgo) player = pgo.transform;
        salvoTimer = 3f;
    }

    void Update()
    {
        if (dead)
        {
            transform.position += Vector3.down * 6f * Time.deltaTime;
            transform.Rotate(0f, 120f * Time.deltaTime, 8f * Time.deltaTime);
            return;
        }

        // strafe across the courtyard, always facing the player
        phase += Time.deltaTime * strafeSpeed;
        Vector3 pos = arenaCenter
            + new Vector3(Mathf.Sin(phase) * strafeWidth, altitude,
                          Mathf.Cos(phase * 0.6f) * 4f);
        transform.position = pos;
        if (player)
        {
            Vector3 look = player.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(look), 2f * Time.deltaTime);
        }

        if (player == null)
            return;

        mgTimer -= Time.deltaTime;
        if (mgTimer <= 0f)
        {
            mgTimer = mgInterval;
            MachineGun();
        }

        salvoTimer -= Time.deltaTime;
        if (salvoTimer <= 0f)
        {
            salvoTimer = salvoInterval;
            StartCoroutine(RocketSalvo());
        }
    }

    void MachineGun()
    {
        // spread fire toward the player; damages on a near-direct line
        Vector3 origin = transform.position + Vector3.down * 0.5f;
        Vector3 dir = (player.position + Vector3.up * 1f - origin).normalized
            + Random.insideUnitSphere * 0.05f;
        G1Audio.Play("fire_smg", origin, 0.5f, 1.2f);
        if (Physics.Raycast(origin, dir, out RaycastHit hit, mgRange))
        {
            if (hit.collider.CompareTag("Player"))
                hit.collider.GetComponent<HealthSystem>()
                    ?.TakeDamage(mgDamage, hit.point, hit.normal);
        }
    }

    System.Collections.IEnumerator RocketSalvo()
    {
        G1Audio.Play("radio_bark_a", transform.position, 0.8f, 0.7f);
        for (int i = 0; i < 3; i++)
        {
            FireRocket();
            yield return new WaitForSeconds(0.25f);
        }
    }

    void FireRocket()
    {
        var rocket = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rocket.name = "Rocket";
        rocket.transform.position = transform.position + Vector3.down * 0.6f;
        rocket.transform.localScale = new Vector3(0.2f, 0.4f, 0.2f);
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.2f, 0.2f, 0.2f);
        rocket.GetComponent<Renderer>().sharedMaterial = mat;
        Vector3 dir = player
            ? (player.position + Vector3.up * 0.5f - rocket.transform.position).normalized
            : Vector3.down;
        var rb = rocket.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.velocity = dir * rocketSpeed;
        rocket.AddComponent<G1Rocket>();
        G1Audio.Play("fire_shotgun", transform.position, 0.6f, 0.7f);
    }

    void Crash()
    {
        dead = true;
        G1Audio.Play("explosion", transform.position, 1f);
        G1ExplosionFX.Spawn(transform.position);
        Destroy(gameObject, 3f);
    }
}

/// Simple boss rocket: flies straight, explodes on any contact with radial
/// damage. Separate from the frag grenade so tuning stays independent.
public sealed class G1Rocket : MonoBehaviour
{
    public float radius = 3.5f;
    public float damage = 22f;
    static readonly Collider[] buf = new Collider[16];

    void Start() => Destroy(gameObject, 6f);   // fail-safe cleanup

    void OnCollisionEnter(Collision c) => Explode();
    void OnTriggerEnter(Collider c) => Explode();

    bool done;
    void Explode()
    {
        if (done) return;
        done = true;
        Vector3 pos = transform.position;
        G1Audio.Play("explosion", pos, 0.9f);
        G1ExplosionFX.Spawn(pos);
        int n = Physics.OverlapSphereNonAlloc(pos, radius, buf);
        var seen = new HashSet<IDamageable>();
        for (int i = 0; i < n; i++)
        {
            var d = buf[i].GetComponentInParent<IDamageable>();
            if (d != null && seen.Add(d))
            {
                float dist = Vector3.Distance(buf[i].ClosestPoint(pos), pos);
                d.TakeDamage(damage * Mathf.Clamp01(1f - dist / radius), pos, Vector3.up);
            }
        }
        Destroy(gameObject);
    }
}
