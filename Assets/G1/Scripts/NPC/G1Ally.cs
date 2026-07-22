using UnityEngine;
using UnityEngine.AI;

/// Friendly faction NPC. Two roles:
///  - combat=true  (security): hunts the nearest Enemy-layer target, holds at
///    range, and fires visible tracers that damage only that target (no
///    friendly fire — damage is applied directly, the projectile is visual).
///  - combat=false (scientist): unarmed; flees from nearby enemies, otherwise
///    wanders near its home point.
[RequireComponent(typeof(HealthSystem))]
public class G1Ally : MonoBehaviour
{
    [Header("Role")]
    public bool combat = true;

    [Header("Combat")]
    public float detectRange = 34f;
    public float engageRange = 22f;
    public float fireInterval = 0.55f;
    public float damage = 10f;
    public float projectileSpeed = 40f;
    public Color tracerColor = new Color(0.4f, 0.8f, 1f);

    [Header("Movement")]
    public float wanderRadius = 8f;

    NavMeshAgent agent;
    HealthSystem health;
    Animator anim;
    Vector3 home;
    float nextFire, nextWander;
    int enemyMask;
    static readonly Collider[] buf = new Collider[24];

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<HealthSystem>();
        anim = GetComponentInChildren<Animator>();
        home = transform.position;
        int el = LayerMask.NameToLayer("Enemy");
        enemyMask = 1 << (el < 0 ? 10 : el);
        if (agent) agent.stoppingDistance = combat ? engageRange * 0.6f : 0.4f;
    }

    void Update()
    {
        if (health.IsDead || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        Transform enemy = NearestEnemy(out float dist);

        if (combat)
        {
            if (enemy != null)
            {
                Face(enemy.position);
                if (dist > engageRange)
                    agent.SetDestination(enemy.position);
                else
                    agent.SetDestination(transform.position);   // hold & fire
                if (dist <= detectRange && HasLineOfSight(enemy) && Time.time >= nextFire)
                    Fire(enemy);
            }
            else
            {
                Wander();
            }
        }
        else   // scientist: flee or mill about
        {
            if (enemy != null && dist < 16f)
            {
                Vector3 away = (transform.position - enemy.position).normalized;
                if (NavMesh.SamplePosition(transform.position + away * 8f,
                        out NavMeshHit h, 8f, NavMesh.AllAreas))
                    agent.SetDestination(h.position);
            }
            else
            {
                Wander();
            }
        }

        if (anim)
            anim.CrossFade(agent.velocity.sqrMagnitude > 0.2f ? "Walk" : "Idle", 0.15f);
    }

    Transform NearestEnemy(out float dist)
    {
        dist = float.MaxValue;
        Transform best = null;
        int n = Physics.OverlapSphereNonAlloc(transform.position, detectRange, buf, enemyMask);
        for (int i = 0; i < n; i++)
        {
            var hs = buf[i].GetComponentInParent<HealthSystem>();
            if (hs == null || hs.IsDead)
                continue;
            float d = Vector3.Distance(transform.position, buf[i].transform.position);
            if (d < dist) { dist = d; best = hs.transform; }
        }
        return best;
    }

    bool HasLineOfSight(Transform enemy)
    {
        Vector3 eye = transform.position + Vector3.up * 1.4f;
        Vector3 dir = (enemy.position + Vector3.up * 1f) - eye;
        if (Physics.Raycast(eye, dir.normalized, out RaycastHit hit, dir.magnitude + 0.5f))
            return hit.collider.GetComponentInParent<HealthSystem>() != null
                && hit.collider.transform.root == enemy.root;
        return true;
    }

    void Fire(Transform enemy)
    {
        nextFire = Time.time + fireInterval;
        Vector3 muzzle = transform.position + Vector3.up * 1.4f + transform.forward * 0.5f;
        Vector3 target = enemy.position + Vector3.up * 1f;

        // visible tracer (no damage — purely readable), real damage applied direct
        var tracer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tracer.transform.position = muzzle;
        tracer.transform.localScale = Vector3.one * 0.14f;
        Destroy(tracer.GetComponent<Collider>());
        tracer.AddComponent<G1VisibleProjectile>()
            .Launch((target - muzzle).normalized, 0f, projectileSpeed, tracerColor, false);

        enemy.GetComponentInParent<HealthSystem>()
            ?.TakeDamage(damage, target, (muzzle - target).normalized);
        G1Audio.Play("fire_smg", muzzle, 0.35f, 1.1f);
    }

    void Face(Vector3 pos)
    {
        Vector3 to = pos - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(to), 6f * Time.deltaTime);
    }

    void Wander()
    {
        if (Time.time < nextWander)
            return;
        nextWander = Time.time + Random.Range(2.5f, 5f);
        Vector2 r = Random.insideUnitCircle * wanderRadius;
        if (NavMesh.SamplePosition(home + new Vector3(r.x, 0f, r.y),
                out NavMeshHit h, wanderRadius, NavMesh.AllAreas))
            agent.SetDestination(h.position);
    }
}
