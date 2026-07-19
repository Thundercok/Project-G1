using System.Collections;
using UnityEngine;

/// Zombie AI: detects the player, chases them down directly,
/// overrides arm bones in LateUpdate to walk with hands raised forward,
/// and performs high-damage double claw swipes when close.
[RequireComponent(typeof(Animator), typeof(HealthSystem))]
public class G1ZombieAI : MonoBehaviour
{
    [Header("Zombie Stats")]
    public float detectionRange = 16f;
    public float speed = 1.6f;            // slower but constant chase speed
    public float turnSpeed = 240f;
    public float attackRange = 1.8f;
    public float attackInterval = 1.5f;
    public float damage = 20f;            // high warning damage

    GameObject player;
    HealthSystem playerHealth;
    HealthSystem myHealth;
    NPCController patrol;
    Animator anim;

    Transform leftArm;
    Transform rightArm;

    bool isChasing;
    float nextAttack;

    void Start()
    {
        player = GameObject.FindWithTag("Player");
        if (player)
            playerHealth = player.GetComponent<HealthSystem>();
        myHealth = GetComponent<HealthSystem>();
        patrol = GetComponent<NPCController>();
        anim = GetComponent<Animator>();

        // Recursively locate arm bones for procedural arm-raise posing
        leftArm = FindBone(transform, "upper_arm.L");
        rightArm = FindBone(transform, "upper_arm.R");
    }

    void Update()
    {
        if (myHealth.IsDead || player == null || playerHealth == null || playerHealth.IsDead)
        {
            isChasing = false;
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);

        if (!isChasing)
        {
            // Scan for player or aggro if damaged
            if (dist <= detectionRange || myHealth.CurrentHealth < myHealth.maxHealth)
            {
                isChasing = true;
                if (patrol) patrol.enabled = false;
                if (anim) anim.CrossFade("Walk", 0.1f);
            }
        }
        else
        {
            // Rotate towards player
            Vector3 lookDir = player.transform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            if (dist > attackRange)
            {
                // Chase player
                transform.position += transform.forward * speed * Time.deltaTime;
                if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                    anim.CrossFade("Walk", 0.1f);
            }
            else
            {
                // Stand and attack player
                if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                    anim.CrossFade("Idle", 0.15f);

                if (Time.time >= nextAttack)
                {
                    PerformClawAttack();
                }
            }
        }
    }

    void LateUpdate()
    {
        // Override animations programmatically to raise zombie arms forward
        if (isChasing && !myHealth.IsDead)
        {
            // Raise upper arms forward and slightly inward
            if (leftArm) leftArm.localRotation = Quaternion.Euler(-80f, 0f, 40f);
            if (rightArm) rightArm.localRotation = Quaternion.Euler(-80f, 0f, -40f);
        }
    }

    void PerformClawAttack()
    {
        nextAttack = Time.time + attackInterval;

        // Visual slash effect (short pop of arms forward-down)
        StartCoroutine(AttackVisualSwipe());

        // Check if player is still in range to take damage
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= attackRange + 0.5f)
        {
            playerHealth.TakeDamage(damage, player.transform.position + Vector3.up * 1f, -transform.forward);
        }
    }

    IEnumerator AttackVisualSwipe()
    {
        float elapsed = 0f;
        float duration = 0.25f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float angleOffset = Mathf.Sin(t * Mathf.PI) * 35f; // dip arms down

            if (leftArm) leftArm.localRotation = Quaternion.Euler(-80f + angleOffset, 0f, 40f);
            if (rightArm) rightArm.localRotation = Quaternion.Euler(-80f + angleOffset, 0f, -40f);
            yield return null;
        }
    }

    Transform FindBone(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var found = FindBone(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
