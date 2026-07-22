using System.Collections;
using UnityEngine;

/// Zombie AI: detects the player, chases them down using NavMeshAgent & separation steering,
/// overrides arm bones in LateUpdate to walk with hands raised forward,
/// and performs high-damage double claw swipes when close.
[RequireComponent(typeof(Animator), typeof(HealthSystem), typeof(UnityEngine.AI.NavMeshAgent))]
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
    UnityEngine.AI.NavMeshAgent agent;

    Transform leftArm;
    Transform rightArm;

    bool isChasing;
    float nextAttack;

    private readonly Collider[] _neighborBuf = new Collider[6];
    private int _mobMask;

    void Start()
    {
        player = GameObject.FindWithTag("Player");
        if (player)
            playerHealth = player.GetComponent<HealthSystem>();
        myHealth = GetComponent<HealthSystem>();
        patrol = GetComponent<NPCController>();
        anim = GetComponent<Animator>();

        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        agent.updateRotation = false; // Manually rotate for retro aesthetic

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer == -1) enemyLayer = 10;
        _mobMask = 1 << enemyLayer;

        // Recursively locate arm bones for procedural arm-raise posing
        leftArm = FindBone(transform, "upper_arm.L");
        rightArm = FindBone(transform, "upper_arm.R");
    }

    void Update()
    {
        if (agent && agent.enabled && !agent.isOnNavMesh)
            return;                     // not placed on a NavMesh (yet)
        if (myHealth.IsDead || player == null || playerHealth == null || playerHealth.IsDead)
        {
            isChasing = false;
            if (agent && agent.enabled) agent.ResetPath();
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
                // Chase player with separation offset (prevent conga-lining)
                if (agent && agent.enabled)
                {
                    agent.speed = speed;
                    agent.SetDestination(player.transform.position + GetSeparationOffset());
                }

                if (anim && !anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                    anim.CrossFade("Walk", 0.1f);
            }
            else
            {
                // Stand and attack player
                if (agent && agent.enabled) agent.ResetPath();

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
        nextAttack = Time.time + attackInterval + 0.6f;
        StartCoroutine(TelegraphedAttackSequence());
    }

    IEnumerator TelegraphedAttackSequence()
    {
        // 1. Telegraph Wind-Up Phase (0.6s) with bright red glowing aura
        var aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(aura.GetComponent<Collider>());
        aura.transform.position = transform.position + Vector3.up * 1.6f + transform.forward * 0.4f;
        aura.transform.localScale = Vector3.one * 0.4f;

        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = new Color(1f, 0.15f, 0.1f, 0.9f);
        aura.GetComponent<Renderer>().sharedMaterial = mat;

        float windUpElapsed = 0f;
        while (windUpElapsed < 0.6f)
        {
            windUpElapsed += Time.deltaTime;
            float scale = 0.4f + Mathf.Sin((windUpElapsed / 0.6f) * Mathf.PI) * 0.3f;
            if (aura != null) aura.transform.localScale = Vector3.one * scale;

            if (leftArm) leftArm.localRotation = Quaternion.Euler(-120f, 0f, 40f);
            if (rightArm) rightArm.localRotation = Quaternion.Euler(-120f, 0f, -40f);
            yield return null;
        }

        if (aura != null) Destroy(aura);

        // 2. Perform Attack & Damage (Casual friendly damage = 6 HP)
        StartCoroutine(AttackVisualSwipe());

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= attackRange + 0.8f && playerHealth != null)
        {
            Vector3 eyePos = transform.position + Vector3.up * 1.5f;
            Vector3 targetPos = player.transform.position + Vector3.up * 1.5f;
            Vector3 dir = targetPos - eyePos;
            bool hitWall = Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dir.magnitude, 1 << 0);
            if (!hitWall)
            {
                playerHealth.TakeDamage(6f, player.transform.position + Vector3.up * 1f, -transform.forward);
            }
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

    private Vector3 GetSeparationOffset()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, 1.2f, _neighborBuf, _mobMask);
        if (n <= 1) return Vector3.zero;

        Vector3 push = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            if (_neighborBuf[i] == null || _neighborBuf[i].gameObject == gameObject) continue;
            Vector3 away = transform.position - _neighborBuf[i].transform.position;
            push += away.normalized / Mathf.Max(away.magnitude, 0.1f);
        }
        return push * 0.8f;
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
