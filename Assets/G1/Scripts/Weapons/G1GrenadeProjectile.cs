using System.Collections.Generic;
using UnityEngine;

/// Live grenade in the world: bounces off geometry (clink SFX), counts down
/// its fuse, then explodes with a light flash, shockwave ring, physics debris
/// shards, radial falloff damage, rigidbody kick, and camera shake.
[RequireComponent(typeof(Rigidbody))]
public sealed class G1GrenadeProjectile : MonoBehaviour
{
    public float fuse = 3f;
    public float radius = 5f;
    public float maxDamage = 80f;
    public float explosionForce = 400f;

    static readonly Collider[] buf = new Collider[32];
    static readonly HashSet<IDamageable> seen = new HashSet<IDamageable>();
    static PhysicMaterial bounceMat;

    float nextBounceSound;

    void Start()
    {
        // shared bouncy physic material for all live grenades
        if (bounceMat == null)
        {
            bounceMat = new PhysicMaterial("GrenadeBounce")
            {
                bounciness = 0.65f,
                dynamicFriction = 0.3f,
                staticFriction = 0.3f,
                bounceCombine = PhysicMaterialCombine.Maximum,
            };
        }
        var col = GetComponent<Collider>();
        if (col)
            col.material = bounceMat;
    }

    void OnCollisionEnter(Collision c)
    {
        // clink on solid impacts, throttled and scaled to impact speed
        if (Time.time < nextBounceSound)
            return;
        float speed = c.relativeVelocity.magnitude;
        if (speed < 1.5f)
            return;
        nextBounceSound = Time.time + 0.08f;
        G1Audio.Play("hit_thunk", transform.position,
                     Mathf.Clamp01(speed / 10f) * 0.5f, 1.6f);
    }

    void Update()
    {
        fuse -= Time.deltaTime;
        if (fuse <= 0f)
            Explode();
    }

    void Explode()
    {
        Vector3 pos = transform.position;
        G1Audio.Play("explosion", pos, 1f);
        G1ExplosionFX.Spawn(pos);
        SpawnDebris(pos);

        seen.Clear();
        int hits = Physics.OverlapSphereNonAlloc(pos, radius, buf);
        for (int i = 0; i < hits; i++)
        {
            var dmg = buf[i].GetComponentInParent<IDamageable>();
            if (dmg != null && seen.Add(dmg))
            {
                float dist = Vector3.Distance(buf[i].ClosestPoint(pos), pos);
                dmg.TakeDamage(maxDamage * Mathf.Clamp01(1f - dist / radius),
                               pos, (buf[i].transform.position - pos).normalized);
            }
            if (buf[i].attachedRigidbody)
                buf[i].attachedRigidbody.AddExplosionForce(
                    explosionForce, pos, radius);
        }

        var cam = Camera.main;
        if (cam)
        {
            float camDist = Vector3.Distance(cam.transform.position, pos);
            if (camDist < 8f)
            {
                var fx = cam.GetComponent<CameraEffects>();
                if (fx)
                    fx.Shake(0.4f * (1f - camDist / 8f));
            }
        }
        Destroy(gameObject);
    }

    void SpawnDebris(Vector3 pos)
    {
        int n = Random.Range(10, 15);
        var smoke = new Color(0.2f, 0.2f, 0.2f);
        var fire = new Color(1f, 0.55f, 0.15f);
        for (int i = 0; i < n; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.transform.position = pos + Random.insideUnitSphere * 0.3f;
            shard.transform.localScale = Vector3.one * Random.Range(0.06f, 0.16f);
            var mat = new Material(Shader.Find("Standard"));
            mat.color = Random.value < 0.5f ? smoke : fire;
            shard.GetComponent<Renderer>().sharedMaterial = mat;
            var rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            rb.velocity = (Random.insideUnitSphere + Vector3.up) * Random.Range(3f, 7f);
            rb.angularVelocity = Random.insideUnitSphere * 14f;
            Destroy(shard, Random.Range(1.5f, 2.5f));
        }
    }
}
