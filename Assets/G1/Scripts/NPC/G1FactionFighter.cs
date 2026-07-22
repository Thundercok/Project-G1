using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// A combatant in the two-faction battlefield. Every fighter registers itself,
/// finds the nearest living member of the OPPOSING faction (hostiles also count
/// the player), and engages: ranged fighters hold at range and fire tracers,
/// melee fighters close in and strike. Friendly-fire-free (damage is applied
/// directly; the tracer is purely visual).
[RequireComponent(typeof(HealthSystem))]
public sealed class G1FactionFighter : MonoBehaviour
{
    public enum Faction { Allied, Hostile }
    public enum Kind { Ranged, Melee }

    public Faction faction = Faction.Allied;
    public Kind kind = Kind.Ranged;

    [Header("Combat")]
    public float detectRange = 60f;
    public float engageRange = 26f;     // ranged: hold here; melee: strike range
    public float fireInterval = 0.7f;
    public float damage = 12f;
    public float projectileSpeed = 45f;

    static readonly List<G1FactionFighter> Registry = new List<G1FactionFighter>(128);
    static Transform playerT;
    static HealthSystem playerHp;

    NavMeshAgent agent;
    HealthSystem health;
    Animator anim;
    Transform target;
    float nextScan, nextFire;
    Color tracer;

    void OnEnable() { Registry.Add(this); }
    void OnDisable() { Registry.Remove(this); }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<HealthSystem>();
        anim = GetComponentInChildren<Animator>();
        tracer = faction == Faction.Allied
            ? new Color(0.4f, 0.8f, 1f) : new Color(1f, 0.55f, 0.2f);
        if (agent) { agent.stoppingDistance = kind == Kind.Ranged ? engageRange * 0.7f : 1.4f;
                     agent.updateRotation = false; }
        if (playerT == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p) { playerT = p.transform; playerHp = p.GetComponent<HealthSystem>(); }
        }
    }

    void Update()
    {
        if (health.IsDead || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.3f;
            target = FindTarget();
        }
        if (target == null)
        {
            agent.SetDestination(transform.position);
            Animate(false);
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);
        Face(target.position);

        if (kind == Kind.Ranged)
        {
            if (dist > engageRange) { agent.SetDestination(target.position); Animate(true); }
            else { agent.SetDestination(transform.position); Animate(false); }
            if (dist <= detectRange && Time.time >= nextFire && HasLoS(target))
                FireRanged(target);
        }
        else   // melee
        {
            if (dist > engageRange) { agent.SetDestination(target.position); Animate(true); }
            else { agent.SetDestination(transform.position); Animate(false);
                   if (Time.time >= nextFire) Melee(target); }
        }
    }

    Transform FindTarget()
    {
        float best = detectRange * detectRange;
        Transform found = null;
        for (int i = 0; i < Registry.Count; i++)
        {
            var f = Registry[i];
            if (f == null || f == this || f.faction == faction || f.health.IsDead)
                continue;
            float d = (f.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; found = f.transform; }
        }
        // hostiles also hunt the player
        if (faction == Faction.Hostile && playerT != null && playerHp != null && !playerHp.IsDead)
        {
            float d = (playerT.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; found = playerT; }
        }
        return found;
    }

    bool HasLoS(Transform t)
    {
        Vector3 eye = transform.position + Vector3.up * 1.4f;
        Vector3 dir = (t.position + Vector3.up) - eye;
        if (Physics.Raycast(eye, dir.normalized, out RaycastHit hit, dir.magnitude + 0.5f))
            return hit.collider.transform.root == t.root;
        return true;
    }

    void FireRanged(Transform t)
    {
        nextFire = Time.time + fireInterval;
        Vector3 muzzle = transform.position + Vector3.up * 1.4f + transform.forward * 0.5f;
        Vector3 aim = t.position + Vector3.up;
        var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        vis.transform.position = muzzle;
        vis.transform.localScale = Vector3.one * 0.14f;
        Destroy(vis.GetComponent<Collider>());
        vis.AddComponent<G1VisibleProjectile>()
           .Launch((aim - muzzle).normalized, 0f, projectileSpeed, tracer, false);

        DamageTarget(t, aim, muzzle);
        G1Audio.Play("fire_smg", muzzle, 0.3f, faction == Faction.Allied ? 1.1f : 0.9f);
    }

    void Melee(Transform t)
    {
        nextFire = Time.time + fireInterval;
        DamageTarget(t, t.position + Vector3.up, transform.position);
        G1Audio.Play("hit_thunk", transform.position, 0.5f, 1.1f);
    }

    void DamageTarget(Transform t, Vector3 at, Vector3 from)
    {
        // the player is targeted via its transform; fighters via their component
        if (playerT != null && t == playerT)
            playerHp?.TakeDamage(damage, at, (from - at).normalized);
        else
            t.GetComponent<HealthSystem>()?.TakeDamage(damage, at, (from - at).normalized);
    }

    void Face(Vector3 pos)
    {
        Vector3 to = pos - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(to), 7f * Time.deltaTime);
    }

    void Animate(bool moving)
    {
        if (anim) anim.CrossFade(moving ? "Walk" : "Idle", 0.15f);
    }
}
