using System.Collections;
using UnityEngine;

/// NPC combat AI: scans for player in range & line of sight,
/// pauses patrol, faces the player, shoots red tracer bursts,
/// and damages the player.
public class G1NPCCombat : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 18f;
    public float detectionAngle = 120f;
    public LayerMask obstacleMask = ~0;

    [Header("Combat")]
    public float damage = 8f;
    public float fireInterval = 1.4f;
    public float turnSpeed = 260f;

    GameObject player;
    HealthSystem playerHealth;
    NPCController patrol;
    Animator anim;
    float nextFire;
    bool aggroed;

    void Start()
    {
        player = GameObject.FindWithTag("Player");
        if (player)
            playerHealth = player.GetComponent<HealthSystem>();
        patrol = GetComponent<NPCController>();
        anim = GetComponent<Animator>();

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer == -1) playerLayer = 8;
        obstacleMask = (1 << 0) | (1 << playerLayer);
    }

    void Update()
    {
        if (player == null || playerHealth == null || playerHealth.IsDead)
        {
            ResetPatrol();
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);

        if (!aggroed)
        {
            // Scan for player
            if (dist <= detectionRange && CheckLineOfSight())
            {
                aggroed = true;
                if (patrol) patrol.enabled = false; // pause patrol path
                if (anim) anim.CrossFade("Idle", 0.1f);
            }
        }

        if (aggroed)
        {
            // Rotate to face player
            Vector3 lookDir = player.transform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            // Shoot if ready
            if (Time.time >= nextFire && CheckLineOfSight())
            {
                ShootPlayer();
            }

            // Lose aggro if player runs too far away
            if (dist > detectionRange * 1.5f)
            {
                ResetPatrol();
            }
        }
    }

    void ResetPatrol()
    {
        if (aggroed)
        {
            aggroed = false;
            if (patrol)
            {
                patrol.enabled = true;
                // Force patrol animation state reset
                var isWalking = patrol.waypoints != null && patrol.waypoints.Length > 1;
                if (anim) anim.CrossFade(isWalking ? "Walk" : "Idle", 0.1f);
            }
        }
    }

    bool CheckLineOfSight()
    {
        Vector3 eyePos = transform.position + Vector3.up * 1.62f + transform.forward * 0.45f;
        Vector3 targetPos = player.transform.position + Vector3.up * 1.62f;
        Vector3 dir = targetPos - eyePos;
        float dist = dir.magnitude;

        // Angle check
        float angle = Vector3.Angle(transform.forward, dir.normalized);
        if (angle > detectionAngle / 2f)
            return false;

        // Raycast check for wall obstacles
        if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dist, obstacleMask))
        {
            if (hit.collider.gameObject != player)
                return false; // hit wall/crate first
        }

        return true;
    }

    void ShootPlayer()
    {
        nextFire = Time.time + fireInterval;

        // Play visual red tracer flash at hand
        PlayMuzzleFlash();

        // Check damage hit
        Vector3 eyePos = transform.position + Vector3.up * 1.62f + transform.forward * 0.45f;
        Vector3 targetPos = player.transform.position + Vector3.up * 1.62f;
        Vector3 dir = targetPos - eyePos;

        if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, detectionRange * 1.5f, obstacleMask))
        {
            var hp = hit.collider.GetComponentInParent<HealthSystem>();
            if (hp != null && hit.collider.gameObject == player)
            {
                hp.TakeDamage(damage, hit.point, hit.normal);
            }
        }
    }

    void PlayMuzzleFlash()
    {
        Transform hand = FindHand(transform);
        if (hand == null) hand = transform;

        // Spawn a temporary bright red sphere representing gunfire
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(flash.GetComponent<Collider>());
        flash.transform.position = hand.position + transform.forward * 0.18f;
        flash.transform.localScale = Vector3.one * 0.12f;

        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = new Color(1f, 0.1f, 0.1f); // bright warning red
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
}
