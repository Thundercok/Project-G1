using System.Collections.Generic;
using UnityEngine;

// Split out of G1HelicopterBoss.cs so the class name matches the file name.
//
// Unity only creates a MonoScript for the type whose name matches its file. Any
// other MonoBehaviour in that file can still be added by AddComponent while the
// editor session lasts, and then serialises into the scene as `m_Script:
// {fileID: 0}` — a component that silently is not there the next time the
// scene is opened. Twenty-eight of them had accumulated, including every quest
// zone, every objective-on-death and the extraction gate, which is why walking
// into a quest trigger did nothing and why killing the boss never ticked the
// objective. It fails without an error at any point, so the only defence is the
// naming rule.

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
