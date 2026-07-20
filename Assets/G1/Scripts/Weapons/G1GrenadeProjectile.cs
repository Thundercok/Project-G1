using System.Collections.Generic;
using UnityEngine;

/// Live grenade in the world: counts down its fuse, then deals radial
/// falloff damage, kicks rigidbodies, shakes the camera, and cleans up.
public sealed class G1GrenadeProjectile : MonoBehaviour
{
    public float fuse = 3f;
    public float radius = 5f;
    public float maxDamage = 80f;
    public float explosionForce = 400f;

    static readonly Collider[] buf = new Collider[32];
    static readonly HashSet<IDamageable> seen = new HashSet<IDamageable>();

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

        seen.Clear();
        int hits = Physics.OverlapSphereNonAlloc(pos, radius, buf);
        for (int i = 0; i < hits; i++)
        {
            var dmg = buf[i].GetComponentInParent<IDamageable>();
            if (dmg != null && seen.Add(dmg))
            {
                float dist = Vector3.Distance(
                    buf[i].ClosestPoint(pos), pos);
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
                    fx.Shake(0.35f * (1f - camDist / 8f));
            }
        }
        Destroy(gameObject);
    }
}
