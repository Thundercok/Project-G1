using UnityEngine;

/// Visible 3D physical projectile (No Hitscan) with glowing tracer trail and travel time.
/// Used by HECU Soldiers and Alien Mobs so attacks are completely readable and dodgeable!
public class G1VisibleProjectile : MonoBehaviour
{
    [Header("Projectile Properties")]
    public float speed = 25f;
    public float damage = 8f;
    public float maxLifetime = 4.0f;
    public bool isEnemyProjectile = true;
    public Color tracerColor = new Color(1f, 0.7f, 0.1f);

    private Vector3 direction;
    private float spawnTime;
    private TrailRenderer trail;

    public void Launch(Vector3 dir, float dmg, float projSpeed, Color col, bool isEnemy = true)
    {
        direction = dir.normalized;
        damage = dmg;
        speed = projSpeed;
        tracerColor = col;
        isEnemyProjectile = isEnemy;
        spawnTime = Time.time;

        transform.rotation = Quaternion.LookRotation(direction);

        // Visual tracer sphere
        var r = GetComponent<Renderer>();
        if (r != null)
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = tracerColor;
            r.sharedMaterial = mat;
        }

        // Add trail renderer for readable bullet trajectory
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.startWidth = 0.12f;
        trail.endWidth = 0.02f;
        var trailMat = new Material(Shader.Find("Unlit/Color"));
        trailMat.color = tracerColor;
        trail.material = trailMat;
    }

    private void Update()
    {
        float moveDist = speed * Time.deltaTime;
        Vector3 nextPos = transform.position + direction * moveDist;

        // Check collision during movement
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, moveDist + 0.1f))
        {
            if (isEnemyProjectile)
            {
                // Check if hit player
                var playerHp = hit.collider.GetComponentInParent<HealthSystem>();
                if (playerHp != null && hit.collider.CompareTag("Player"))
                {
                    playerHp.TakeDamage(damage, hit.point, hit.normal);
                    if (ThreatDirector.Instance != null)
                    {
                        ThreatDirector.Instance.ReportPlayerHit();
                    }
                }
            }
            else
            {
                // Player projectile hitting enemy
                var enemyHp = hit.collider.GetComponentInParent<HealthSystem>();
                if (enemyHp != null)
                {
                    enemyHp.TakeDamage(damage, hit.point, hit.normal);
                }
            }

            // Spawn impact spark effect
            SpawnImpactFX(hit.point, hit.normal);
            Destroy(gameObject);
            return;
        }

        transform.position = nextPos;

        if (Time.time - spawnTime > maxLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void SpawnImpactFX(Vector3 point, Vector3 normal)
    {
        var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(spark.GetComponent<Collider>());
        spark.transform.position = point;
        spark.transform.localScale = Vector3.one * 0.15f;

        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = tracerColor;
        spark.GetComponent<Renderer>().sharedMaterial = mat;

        Destroy(spark, 0.2f);
    }
}
