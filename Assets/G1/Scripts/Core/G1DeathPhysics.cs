using System.Collections;
using UnityEngine;

/// Satisfying physical death reaction for NPCs:
/// Disables animation/AI, adds a Rigidbody, applies a physics kick
/// based on the fatal hit's normal, ignores player collision,
/// and handles slow ground-sinking cleanup.
[RequireComponent(typeof(HealthSystem))]
public class G1DeathPhysics : MonoBehaviour
{
    public float forceMultiplier = 12f;
    public float torqueMultiplier = 15f;
    public float lieDuration = 5f;
    public float sinkDuration = 2f;

    void Awake()
    {
        GetComponent<HealthSystem>().OnDeath += HandleDeath;
    }

    void HandleDeath(Vector3 hitPoint, Vector3 hitNormal)
    {
        // 1. Disable AI and Animation
        var patrol = GetComponent<NPCController>();
        if (patrol) patrol.enabled = false;

        var combat = GetComponent<G1NPCCombat>();
        if (combat) combat.enabled = false;

        var anim = GetComponent<Animator>();
        if (anim) anim.enabled = false;

        // 2. Ignore collision with the Player
        var myCollider = GetComponent<Collider>();
        var player = GameObject.FindWithTag("Player");
        if (player && myCollider)
        {
            var playerCollider = player.GetComponent<Collider>();
            if (playerCollider)
            {
                Physics.IgnoreCollision(playerCollider, myCollider);
            }
        }

        // 3. Add physical Rigidbody for tipping fall
        var rb = gameObject.GetComponent<Rigidbody>();
        if (!rb)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.mass = 80f;
        rb.drag = 0.5f;
        rb.angularDrag = 0.5f;

        // Push relative to bullet vector (-hitNormal)
        Vector3 pushDir = -hitNormal;
        pushDir.y = Mathf.Max(pushDir.y, 0.2f); // ensure slightly upward pop
        rb.AddForceAtPosition(pushDir.normalized * forceMultiplier, hitPoint, ForceMode.Impulse);

        // Add twist torque to tip over naturally
        Vector3 torqueDir = Vector3.Cross(Vector3.up, pushDir).normalized;
        if (torqueDir.sqrMagnitude < 0.01f) torqueDir = transform.right;
        rb.AddTorque(torqueDir * torqueMultiplier, ForceMode.Impulse);

        // 4. Start sinking and cleanup coroutine
        StartCoroutine(SinkAndDestroy(rb, myCollider));
    }

    IEnumerator SinkAndDestroy(Rigidbody rb, Collider col)
    {
        yield return new WaitForSeconds(lieDuration);

        // Turn off physics collision so body can sink through floor
        if (rb) rb.isKinematic = true;
        if (col) col.enabled = false;

        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.down * 2.0f; // sink 2m down

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / sinkDuration;
            // Smooth ease-in for sinking
            transform.position = Vector3.Lerp(startPos, targetPos, t * t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
