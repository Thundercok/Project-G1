using System.Collections;
using UnityEngine;

/// Burst-fire Patrol Soldier AI.
/// Scans for player, patrols waypoints, chases player when alerted,
/// shoots in 3-shot bursts, and drops procedurally generated health/ammo packs on death.
[RequireComponent(typeof(Animator), typeof(HealthSystem))]
public class G1SoldierAI : MonoBehaviour
{
    private enum SoldierState : byte { Patrol, Alert, BurstFire, Dead }

    [Header("Patrol & Movement")]
    public Transform[] waypoints;
    public float patrolSpeed = 1.0f;
    public float chaseSpeed = 3.0f;
    public float turnSpeed = 220f;

    [Header("Combat & Detection")]
    public float detectRadius = 20f;
    public float attackRadius = 13f;
    public float damage = 8f;
    public float fireRate = 1.6f;        // cooldown between bursts
    public float burstGap = 0.11f;       // gap between shots in burst

    [Header("Drop Settings")]
    [Range(0f, 1f)] public float dropChance = 0.72f;

    private SoldierState state = SoldierState.Patrol;
    private HealthSystem myHealth;
    private Animator anim;
    private GameObject player;
    private HealthSystem playerHealth;

    private int waypointIdx;
    private float nextBurstTime;
    private float nextShotTime;
    private int shotsLeftInBurst;
    private bool playerSpotted;

    // Zero-alloc buffers
    private readonly Collider[] detectBuf = new Collider[4];
    private static readonly int playerMask = 1 << 6; // Layer 6: Player (aligned with builder)
    private static readonly int obstacleMask = (1 << 0) | (1 << 6); // Default + Player layers

    void Start()
    {
        myHealth = GetComponent<HealthSystem>();
        myHealth.OnDeath += HandleDeath;
        anim = GetComponent<Animator>();

        player = GameObject.FindWithTag("Player");
        if (player)
            playerHealth = player.GetComponent<HealthSystem>();

        state = SoldierState.Patrol;
        waypointIdx = 0;
        
        // Start patrol animation
        bool isWalking = waypoints != null && waypoints.Length > 1;
        if (anim) anim.CrossFade(isWalking ? "Walk" : "Idle", 0.05f);
    }

    void Update()
    {
        if (state == SoldierState.Dead || player == null || playerHealth == null || playerHealth.IsDead)
            return;

        // Perform periodic player detection check
        PollDetection();

        switch (state)
        {
            case SoldierState.Patrol:
                TickPatrol();
                break;
            case SoldierState.Alert:
                TickAlert();
                break;
            case SoldierState.BurstFire:
                TickBurstFire();
                break;
        }
    }

    void PollDetection()
    {
        playerSpotted = false;
        int count = Physics.OverlapSphereNonAlloc(transform.position, detectRadius, detectBuf, playerMask);
        
        // Fix for micro-quiz bug: iterate through all overlaps to check for line-of-sight
        for (int i = 0; i < count; i++)
        {
            if (detectBuf[i].CompareTag("Player"))
            {
                if (CheckLineOfSight())
                {
                    playerSpotted = true;
                    break;
                }
            }
        }

        // Transition states
        if (playerSpotted)
        {
            if (state == SoldierState.Patrol)
            {
                state = SoldierState.Alert;
                if (anim) anim.CrossFade("Walk", 0.1f);
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist <= attackRadius && Time.time >= nextBurstTime && state != SoldierState.BurstFire)
            {
                BeginBurst();
            }
        }
        else if (state == SoldierState.Alert)
        {
            // Lose alert status if player runs too far and is unseen
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > detectRadius * 1.5f)
            {
                ResetPatrol();
            }
        }
    }

    bool CheckLineOfSight()
    {
        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = player.transform.position + Vector3.up * 1.5f;
        Vector3 dir = targetPos - eyePos;
        float dist = dir.magnitude;

        // Angle check (120 degrees full angle)
        float angle = Vector3.Angle(transform.forward, dir.normalized);
        if (angle > 60f)
            return false;

        // Raycast check for wall obstacles
        if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dist, obstacleMask))
        {
            if (hit.collider.gameObject != player)
                return false; // hit wall/crate first
        }
        return true;
    }

    void TickPatrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Vector3 target = waypoints[waypointIdx].position;
        target.y = transform.position.y;
        Vector3 to = target - transform.position;

        if (to.magnitude < 0.3f)
        {
            waypointIdx = (waypointIdx + 1) % waypoints.Length;
        }
        else
        {
            Quaternion look = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            transform.position += transform.forward * patrolSpeed * Time.deltaTime;
        }
    }

    void TickAlert()
    {
        if (player == null) return;

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        // Keep moving towards the player
        if (dist > attackRadius - 1f)
        {
            Quaternion look = Quaternion.LookRotation(toPlayer);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            transform.position += transform.forward * chaseSpeed * Time.deltaTime;

            if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                anim.CrossFade("Walk", 0.1f);
        }
        else
        {
            // Face the player when close
            Quaternion look = Quaternion.LookRotation(toPlayer);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);

            if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                anim.CrossFade("Idle", 0.1f);
        }
    }

    void BeginBurst()
    {
        state = SoldierState.BurstFire;
        shotsLeftInBurst = 3;
        nextShotTime = Time.time;
        if (anim) anim.CrossFade("Idle", 0.05f);
    }

    void TickBurstFire()
    {
        // Face player while firing
        Vector3 lookDir = player.transform.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        if (Time.time >= nextShotTime)
        {
            if (shotsLeftInBurst > 0)
            {
                ShootRound();
                shotsLeftInBurst--;
                nextShotTime = Time.time + burstGap;
            }
            else
            {
                // Burst finished, transition back to Alert and start cooldown
                state = SoldierState.Alert;
                nextBurstTime = Time.time + fireRate;
            }
        }
    }

    void ShootRound()
    {
        PlayMuzzleFlash();

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = player.transform.position + Vector3.up * 1.5f;
        Vector3 dir = (targetPos - eyePos).normalized;

        // Apply inaccuracy spread
        dir = Quaternion.Euler(
            Random.Range(-2.5f, 2.5f),
            Random.Range(-2.5f, 2.5f),
            0f
        ) * dir;

        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, detectRadius * 1.5f, obstacleMask))
        {
            if (hit.collider.gameObject == player)
            {
                playerHealth.TakeDamage(damage, hit.point, hit.normal);
            }
        }
    }

    void PlayMuzzleFlash()
    {
        Transform hand = FindHand(transform);
        if (hand == null) hand = transform;

        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(flash.GetComponent<Collider>());
        flash.transform.position = hand.position + transform.forward * 0.18f;
        flash.transform.localScale = Vector3.one * 0.12f;

        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = new Color(1f, 0.75f, 0.15f); // warning yellow-orange flash
        flash.GetComponent<MeshRenderer>().sharedMaterial = mat;

        Destroy(flash, 0.05f);
    }

    Transform FindHand(Transform parent)
    {
        if (parent.name == "hand.R") return parent;
        foreach (Transform child in parent)
        {
            var found = FindHand(child);
            if (found != null) return found;
        }
        return null;
    }

    void ResetPatrol()
    {
        state = SoldierState.Patrol;
        bool isWalking = waypoints != null && waypoints.Length > 1;
        if (anim) anim.CrossFade(isWalking ? "Walk" : "Idle", 0.1f);
    }

    void HandleDeath(Vector3 hitPoint, Vector3 hitNormal)
    {
        state = SoldierState.Dead;
        TryDropPack();
    }

    void TryDropPack()
    {
        if (Random.value > dropChance) return;

        Vector3 dropPos = transform.position + Vector3.up * 0.12f;
        
        // 50/50 chance for Health Kit vs Ammo Pack
        if (Random.value < 0.5f)
        {
            G1HealthPack.Create(dropPos);
        }
        else
        {
            G1AmmoPack.Create(dropPos);
        }
    }

    void OnDestroy()
    {
        if (myHealth)
            myHealth.OnDeath -= HandleDeath;
    }
}
