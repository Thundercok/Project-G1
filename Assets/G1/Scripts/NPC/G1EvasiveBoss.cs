using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// The finale boss. It CANNOT attack the player — it only runs, jukes, and keeps
/// its distance, so it is hard to gun down. Kill it to win. Uses a NavMeshAgent
/// to flee to the farthest of several sampled points and changes direction
/// unpredictably. On death it triggers the scripted ending if present, otherwise
/// returns to the menu.
[RequireComponent(typeof(NavMeshAgent), typeof(HealthSystem))]
public class G1EvasiveBoss : MonoBehaviour
{
    [Header("Evasion")]
    public float fleeRadius = 22f;       // how far it tries to stay from the player
    public float panicRadius = 10f;      // inside this it sprints and jukes harder
    public float repathInterval = 0.6f;
    public float baseSpeed = 7.5f;
    public float panicSpeed = 11f;

    [Header("Win")]
    public string winScene = "MenuScene";

    NavMeshAgent agent;
    HealthSystem health;
    Transform player;
    float nextRepath;
    bool down;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<HealthSystem>();
        agent.angularSpeed = 720f;
        agent.acceleration = 32f;
        agent.stoppingDistance = 0f;
        agent.autoBraking = false;       // keep momentum, never settle = hard to hit
        agent.speed = baseSpeed;

        var p = GameObject.FindWithTag("Player");
        if (p) player = p.transform;

        health.OnDeath += OnBossDown;
    }

    void Update()
    {
        if (down || player == null || health.IsDead) return;
        if (agent == null || !agent.isOnNavMesh) return;

        // Always face-away flee; sprint when the player closes in.
        float dist = Vector3.Distance(transform.position, player.position);
        agent.speed = dist < panicRadius ? panicSpeed : baseSpeed;

        if (Time.time < nextRepath) return;
        nextRepath = Time.time + repathInterval;

        Vector3 away = (transform.position - player.position);
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
        away.Normalize();

        // Sample several candidates; pick the one that maximizes distance from the
        // player, with jitter so the path is unpredictable (juking).
        Vector3 best = transform.position;
        float bestScore = -1f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-120f, 120f), 0f) * away;
            Vector3 cand = transform.position + dir * Random.Range(6f, fleeRadius);
            if (NavMesh.SamplePosition(cand, out var hit, 4f, NavMesh.AllAreas))
            {
                float score = Vector3.Distance(hit.position, player.position) + Random.Range(0f, 7f);
                if (score > bestScore) { bestScore = score; best = hit.position; }
            }
        }
        agent.SetDestination(best);
    }

    void OnBossDown(Vector3 point, Vector3 normal)
    {
        if (down) return;
        down = true;
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        G1Audio.Play("explosion", transform.position, 1f, 0.7f);
        GameObject.FindWithTag("Player")?.GetComponent<PlayerHUD>()
            ?.ShowTerminalLog("THE PROXY IS DOWN. THE LOOP IS YOURS TO END.");

        // Prefer the scripted collapse ending if this scene has one.
        var ending = FindFirstObjectByType<G1EndingCutscene>();
        if (ending != null)
            G1EndingCutscene.TriggerCollapse(transform.position);
        else
            Invoke(nameof(LoadWin), 4.5f);

        G1LevelExitTrigger.ElevatorUnlocked = true;   // any exit now opens
    }

    void LoadWin() => SceneManager.LoadScene(winScene);
}
