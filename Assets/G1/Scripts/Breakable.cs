using UnityEngine;

/// HL crate energy: reacts to its HealthSystem — shrinks a touch per hit as
/// damage feedback, shatters into physics shards on death.
[RequireComponent(typeof(HealthSystem))]
public class Breakable : MonoBehaviour
{
    public int shardCount = 8;
    public float shardLife = 5f;

    void Awake()
    {
        var health = GetComponent<HealthSystem>();
        health.OnHealthChanged += (current, max) => transform.localScale *= 0.985f;
        health.OnDeath += Shatter;
    }

    void Shatter(Vector3 point, Vector3 normal)
    {
        Vector3 dir = -normal;                      // push shards away from the hit
        Renderer rend = GetComponentInChildren<Renderer>();
        Material mat = rend ? rend.sharedMaterial : null;
        Vector3 size = transform.localScale;
        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.transform.position = transform.position
                + Random.insideUnitSphere * size.magnitude * 0.25f;
            shard.transform.rotation = Random.rotation;
            shard.transform.localScale = size * Random.Range(0.15f, 0.3f);
            if (mat)
                shard.GetComponent<Renderer>().sharedMaterial = mat;
            Rigidbody rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.velocity = dir * 2.5f + Random.insideUnitSphere * 2f + Vector3.up * 1.5f;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            Destroy(shard, shardLife);
        }
        Destroy(gameObject);
    }
}
