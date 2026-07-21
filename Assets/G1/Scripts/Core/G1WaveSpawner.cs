using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// 3-wave alien encounter system — replaces the old one-shot G1HordeTrigger
/// for the Alien Breach Zone.
///
/// Beat 1: Player crosses the trigger → a signal light turns teal → Wave 1
///         spawns FAR AWAY (at spawnFarCenter) giving the player 2–3 seconds
///         to duck behind a cover block.
/// Beat 2: When ≥ 60 % of Wave 1 is dead → Wave 2 flanks from both side walls.
/// Beat 3: When all of Wave 2 is dead → 1 Elite Alien spawns at the far end,
///         override terminal unlocks.
///
/// On Wave 3 death → calls overrideTerminal.isUnlocked = true so the player
/// can press E to open Door 4 and reach the elevator.
public class G1WaveSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject alienPrefab;
    public GameObject eliteAlienPrefab;   // can share alienPrefab; G1EliteAlien boosts stats in Awake

    [Header("Spawn Positions")]
    public Vector3 spawnFarCenter  = new Vector3(12f, 0f, 70f); // Wave 1 — far north
    public Vector3 spawnFlankLeft  = new Vector3( 7f, 0f, 67f); // Wave 2 — west wall
    public Vector3 spawnFlankRight = new Vector3(17f, 0f, 67f); // Wave 2 — east wall
    public Vector3 spawnElite      = new Vector3(12f, 0f, 72f); // Wave 3 — north end

    [Header("Signal Light (optional)")]
    public Light signalLight;          // teal point light above trigger — toggled on encounter start

    [Header("Override Terminal")]
    public G1OverrideTerminal overrideTerminal;

    [Header("Stagger")]
    public float spawnStagger = 0.4f;  // seconds between each alien in a wave

    // ------------------------------------------------------------------ state
    private bool _triggered;
    private int  _wave;                // 0 = not started, 1 2 3

    private readonly List<HealthSystem> _wave1 = new List<HealthSystem>();
    private readonly List<HealthSystem> _wave2 = new List<HealthSystem>();
    private HealthSystem               _eliteHealth;

    private int _wave1Dead;
    private int _wave2Dead;

    // ------------------------------------------------------------------ trigger
    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;
        _triggered = true;

        if (signalLight != null)
            signalLight.enabled = true;

        G1Audio.Play2D("horde_roar", 0.55f, 0.65f); // low distant rumble — pre-warning
        StartCoroutine(StartWave1());
    }

    // ------------------------------------------------------------------ Wave 1: 2 aliens far away
    private IEnumerator StartWave1()
    {
        _wave = 1;
        yield return new WaitForSeconds(0.6f); // brief pause — player hears the audio before enemies appear

        for (int i = 0; i < 2; i++)
        {
            Vector3 offset = new Vector3((i == 0 ? -1.2f : 1.2f), 0f, 0f);
            SpawnAlien(spawnFarCenter + offset, _wave1, isElite: false);
            yield return new WaitForSeconds(spawnStagger);
        }
    }

    // ------------------------------------------------------------------ Wave 2: 2 aliens flanking from walls
    private IEnumerator StartWave2()
    {
        _wave = 2;
        yield return new WaitForSeconds(0.3f);

        SpawnAlien(spawnFlankLeft,  _wave2, isElite: false);
        yield return new WaitForSeconds(spawnStagger);
        SpawnAlien(spawnFlankRight, _wave2, isElite: false);
    }

    // ------------------------------------------------------------------ Wave 3: 1 elite
    private IEnumerator StartWave3()
    {
        _wave = 3;

        // Brief dramatic pause before elite appears
        G1Audio.Play2D("horde_roar", 0.8f, 0.55f); // low boom
        yield return new WaitForSeconds(1.2f);

        SpawnAlien(spawnElite, null, isElite: true);
    }

    // ------------------------------------------------------------------ spawn helper
    private void SpawnAlien(Vector3 pos, List<HealthSystem> trackList, bool isElite)
    {
        if (alienPrefab == null) return;

        GameObject go = Instantiate(
            (isElite && eliteAlienPrefab != null) ? eliteAlienPrefab : alienPrefab,
            pos,
            Quaternion.identity
        );
        go.name = isElite ? "Alien_Elite" : "Alien_Wave";

        // Ensure NavMeshAgent is warped on to navmesh
        var warp = go.GetComponent<AgentNavMeshWarp>();
        if (warp == null) warp = go.AddComponent<AgentNavMeshWarp>();

        // Boost stats for elite via helper component
        if (isElite && go.GetComponent<G1EliteAlien>() == null)
            go.AddComponent<G1EliteAlien>();

        var health = go.GetComponent<HealthSystem>();
        if (health == null) return;

        if (trackList != null)
        {
            trackList.Add(health);
            // Subscribe to death event to count kills per wave
            health.OnDeath += (pos2, nrm) => OnEnemyDied(health, trackList, isElite);
        }
        else if (isElite)
        {
            _eliteHealth = health;
            health.OnDeath += (pos2, nrm) => OnEliteDied();
        }
    }

    // ------------------------------------------------------------------ death callbacks
    private void OnEnemyDied(HealthSystem dead, List<HealthSystem> trackList, bool isElite)
    {
        if (trackList == _wave1)
        {
            _wave1Dead++;
            // Trigger Wave 2 when ≥ 60 % of Wave 1 is dead (i.e. 1 of 2)
            if (_wave == 1 && _wave1Dead >= Mathf.CeilToInt(_wave1.Count * 0.6f))
            {
                StartCoroutine(StartWave2());
            }
        }
        else if (trackList == _wave2)
        {
            _wave2Dead++;
            // Trigger Wave 3 only when ALL of Wave 2 is dead
            if (_wave == 2 && _wave2Dead >= _wave2.Count)
            {
                StartCoroutine(StartWave3());
            }
        }
    }

    private void OnEliteDied()
    {
        _wave = 0; // encounter over

        // Signal light flickers off (encounter complete)
        if (signalLight != null)
            StartCoroutine(FlickerOff());

        // Unlock override terminal
        if (overrideTerminal != null)
        {
            overrideTerminal.isUnlocked = true;
        }

        // Let ThreatDirector know things calmed down
        if (ThreatDirector.Instance != null)
            ThreatDirector.Instance.ReportSoldierDead();

        G1Audio.Play2D("pickup", 0.6f, 0.8f); // gentle completion chime
    }

    // ------------------------------------------------------------------ util
    private IEnumerator FlickerOff()
    {
        if (signalLight == null) yield break;
        for (int i = 0; i < 5; i++)
        {
            signalLight.enabled = !signalLight.enabled;
            yield return new WaitForSeconds(0.12f);
        }
        signalLight.enabled = false;
    }
}
