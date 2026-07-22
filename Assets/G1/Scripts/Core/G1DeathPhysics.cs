using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// Satisfying high-octane physical ragdoll death reaction for NPCs:
/// Disables animation/AI/NavMesh, converts model bones/rigidbody into active physics,
/// applies punchy directional force from bullet hits (Shotgun/Magnum impact),
/// and handles slow ground-sinking cleanup.
[RequireComponent(typeof(HealthSystem))]
public class G1DeathPhysics : MonoBehaviour
{
    public float forceMultiplier = 24f;
    public float torqueMultiplier = 25f;
    public float lieDuration = 5f;
    public float sinkDuration = 2f;

    void Awake()
    {
        GetComponent<HealthSystem>().OnDeath += HandleDeath;
    }

    void HandleDeath(Vector3 hitPoint, Vector3 hitNormal)
    {
        // 1. Disable AI, Navigation, and Animation
        var agent = GetComponent<NavMeshAgent>();
        if (agent) { agent.isStopped = true; agent.enabled = false; }

        var patrol = GetComponent<NPCController>();
        if (patrol) patrol.enabled = false;

        var combat = GetComponent<G1NPCCombat>();
        if (combat) combat.enabled = false;

        var soldierAI = GetComponent<G1SoldierAI>();
        if (soldierAI) soldierAI.enabled = false;

        var zombieAI = GetComponent<G1ZombieAI>();
        if (zombieAI) zombieAI.enabled = false;

        var alienAI = GetComponent<G1AlienAI>();
        if (alienAI) alienAI.enabled = false;

        var anim = GetComponent<Animator>();
        if (anim) anim.enabled = false;

        // 2. Ignore collision with Player
        var rootCol = GetComponent<Collider>();
        var player = GameObject.FindWithTag("Player");
        if (player && rootCol)
        {
            var playerCol = player.GetComponent<Collider>();
            if (playerCol) Physics.IgnoreCollision(playerCol, rootCol);
        }

        // 3. Enable ragdoll physics across child bones if present, or root Rigidbody
        var childRbs = GetComponentsInChildren<Rigidbody>();
        Vector3 pushDir = -hitNormal;
        if (pushDir.sqrMagnitude < 0.01f) pushDir = -transform.forward;
        pushDir.y = Mathf.Max(pushDir.y, 0.35f); // High-octane upward & backward launch

        if (childRbs != null && childRbs.Length > 1)
        {
            // Bone hierarchy ragdoll
            foreach (var rbBone in childRbs)
            {
                rbBone.isKinematic = false;
                rbBone.useGravity = true;
                rbBone.AddForceAtPosition(pushDir.normalized * (forceMultiplier * 0.5f), hitPoint, ForceMode.Impulse);
            }
        }
        else
        {
            // Root physics ragdoll
            var rb = GetComponent<Rigidbody>();
            if (!rb) rb = gameObject.AddComponent<Rigidbody>();

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 65f;
            rb.drag = 0.4f;
            rb.angularDrag = 0.4f;

            rb.AddForceAtPosition(pushDir.normalized * forceMultiplier, hitPoint, ForceMode.Impulse);

            Vector3 torqueDir = Vector3.Cross(Vector3.up, pushDir).normalized;
            if (torqueDir.sqrMagnitude < 0.01f) torqueDir = transform.right;
            rb.AddTorque(torqueDir * torqueMultiplier, ForceMode.Impulse);
        }

        // 4. Start sinking and cleanup coroutine
        StartCoroutine(SinkAndDestroy(rootCol));
    }

    IEnumerator SinkAndDestroy(Collider col)
    {
        yield return new WaitForSeconds(lieDuration);

        var rbs = GetComponentsInChildren<Rigidbody>();
        foreach (var r in rbs) r.isKinematic = true;

        if (col) col.enabled = false;
        var cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols) c.enabled = false;

        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.down * 2.0f;

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / sinkDuration;
            transform.position = Vector3.Lerp(startPos, targetPos, t * t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
