using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// ThreatDirector — singleton, zero-alloc spawn director with L4D2 Horde Events.
public sealed class ThreatDirector : MonoBehaviour
{
    public static ThreatDirector Instance { get; private set; }

    [Header("HECU Settings")]
    public GameObject soldierPrefab;
    public Transform[] spawnNodes;
    public int maxActiveSoldiers = 4;
    public float relaxDuration = 8f;
    public float intensityDecayRate = 0.08f;

    [Header("Horde Settings")]
    public GameObject[] mobPrefabs; // [0] = Zombie, [1] = Alien
    public int hordeSize = 10;
    public float hordeSpeedMult = 1.35f;

    public int ActiveSoldiersCount => _activeSoldiers;

    private enum DirectorState : byte { Relax, BuildUp, Peak }
    private DirectorState _dirState = DirectorState.Relax;

    private float _intensity;          // 0..1
    private float _relaxTimer;
    private int _activeSoldiers;
    private bool _hordeTriggeredThisPeak;

    private Camera _cam;
    private Plane[] _frustumPlanes = new Plane[6];

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        _cam = Camera.main;
        _relaxTimer = relaxDuration;
    }

    private void Update()
    {
        DecayIntensity();
        TickDirectorFSM();
    }

    public void ReportPlayerHit() => _intensity = Mathf.Min(_intensity + 0.18f, 1f);
    
    public void ReportSoldierDead() 
    { 
        _activeSoldiers = Mathf.Max(0, _activeSoldiers - 1);
        _intensity = Mathf.Max(_intensity - 0.05f, 0f);
    }

    private void DecayIntensity()
        => _intensity = Mathf.MoveTowards(_intensity, 0f, intensityDecayRate * Time.deltaTime);

    private void TickDirectorFSM()
    {
        switch (_dirState)
        {
            case DirectorState.Relax:
                _relaxTimer -= Time.deltaTime;
                if (_relaxTimer <= 0f) 
                {
                    _dirState = DirectorState.BuildUp;
                    _hordeTriggeredThisPeak = false;
                }
                break;

            case DirectorState.BuildUp:
                if (_intensity >= 0.45f) 
                {
                    _dirState = DirectorState.Peak;
                }
                else 
                {
                    TrySpawnSoldier();
                }
                break;

            case DirectorState.Peak:
                if (_activeSoldiers < maxActiveSoldiers) 
                {
                    TrySpawnSoldier();
                }

                // Trigger Horde event when intensity crosses 0.75 in Peak state
                if (_intensity >= 0.75f && !_hordeTriggeredThisPeak)
                {
                    _hordeTriggeredThisPeak = true;
                    TriggerHordeEvent();
                }

                if (_intensity < 0.2f)
                {
                    _dirState   = DirectorState.Relax;
                    _relaxTimer = relaxDuration;
                    if (SquadBlackboard.Instance != null)
                    {
                        SquadBlackboard.Instance.ResetAlphaStrike();
                    }
                }
                break;
        }
    }

    private void TrySpawnSoldier()
    {
        if (_activeSoldiers >= maxActiveSoldiers) return;
        if (spawnNodes == null || spawnNodes.Length == 0 || soldierPrefab == null) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        GeometryUtility.CalculateFrustumPlanes(_cam, _frustumPlanes);

        int count = spawnNodes.Length;
        // Start from a randomized index to prevent prediction bias when player stands still
        int startIdx = Random.Range(0, count);

        for (int i = 0; i < count; i++)
        {
            int idx = (startIdx + i) % count;
            var node = spawnNodes[idx];
            if (node == null) continue;

            var bounds = new Bounds(node.position, Vector3.one * 1.5f);
            if (GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)) continue;

            var spawned = Instantiate(soldierPrefab, node.position, node.rotation);
            spawned.SetActive(true);
            _activeSoldiers++;
            _intensity += 0.12f;
            return;
        }
    }

    private void TriggerHordeEvent()
    {
        StartCoroutine(SpawnHordeBurst());
    }

    private IEnumerator SpawnHordeBurst()
    {
        // Roar alert warning message
        Debug.LogWarning("⚠️ WARNING: HORDE BURST TRIGGERED! (Infected roar in distance)");

        int spawnedCount = 0;
        if (spawnNodes == null || spawnNodes.Length == 0 || mobPrefabs == null || mobPrefabs.Length == 0)
            yield break;

        // Shuffle spawn nodes to randomize spawning location
        List<Transform> nodesList = new List<Transform>(spawnNodes);
        for (int i = 0; i < nodesList.Count; i++)
        {
            Transform temp = nodesList[i];
            int randomIndex = Random.Range(i, nodesList.Count);
            nodesList[i] = nodesList[randomIndex];
            nodesList[randomIndex] = temp;
        }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) yield break;

        foreach (var node in nodesList)
        {
            if (node == null) continue;
            if (spawnedCount >= hordeSize) yield break;

            // L4D2 Rule: NEVER spawn in player frustum
            GeometryUtility.CalculateFrustumPlanes(_cam, _frustumPlanes);
            var bounds = new Bounds(node.position, Vector3.one * 1.5f);
            if (GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)) continue;

            // Pick Zombie or Alien
            var prefab = mobPrefabs[Random.Range(0, mobPrefabs.Length)];
            if (prefab == null) continue;

            var mob = Instantiate(prefab, node.position, node.rotation);
            mob.SetActive(true);

            // Apply speed boost
            if (mob.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent))
            {
                agent.speed *= hordeSpeedMult;
            }
            // For Zombie/Alien custom scripts: multiply speed as well!
            if (mob.TryGetComponent<G1ZombieAI>(out var zombie))
            {
                zombie.speed *= hordeSpeedMult;
            }
            if (mob.TryGetComponent<G1AlienAI>(out var alien))
            {
                alien.speed *= hordeSpeedMult;
            }

            spawnedCount++;
            yield return new WaitForSeconds(0.15f); // Staggered spawn rate
        }
    }

    public void ForceSpawnHorde(Vector3 pos, bool isZombie)
    {
        if (mobPrefabs == null || mobPrefabs.Length == 0) return;
        int index = isZombie ? 0 : 1;
        if (index >= mobPrefabs.Length) index = 0;
        var prefab = mobPrefabs[index];
        if (prefab == null) return;

        var mob = Instantiate(prefab, pos, Quaternion.identity);
        mob.SetActive(true);

        if (mob.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent))
        {
            agent.speed *= hordeSpeedMult;
        }
        if (mob.TryGetComponent<G1ZombieAI>(out var zombie))
        {
            zombie.speed *= hordeSpeedMult;
        }
        if (mob.TryGetComponent<G1AlienAI>(out var alien))
        {
            alien.speed *= hordeSpeedMult;
        }
    }
}
