using System.Collections.Generic;
using UnityEngine;

/// ThreatDirector — singleton, zero-alloc spawn director
public sealed class ThreatDirector : MonoBehaviour
{
    public static ThreatDirector Instance { get; private set; }

    public GameObject soldierPrefab;
    public Transform[] spawnNodes;
    public int maxActiveSoldiers = 4;
    public float relaxDuration = 8f;
    public float intensityDecayRate = 0.08f;

    public int ActiveSoldiersCount => _activeSoldiers;

    private enum DirectorState : byte { Relax, BuildUp, Peak }
    private DirectorState _dirState = DirectorState.Relax;

    private float _intensity;          // 0..1
    private float _relaxTimer;
    private int _activeSoldiers;

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
                if (_relaxTimer <= 0f) _dirState = DirectorState.BuildUp;
                break;

            case DirectorState.BuildUp:
                if (_intensity >= 0.45f) _dirState = DirectorState.Peak;
                else TrySpawnSoldier();
                break;

            case DirectorState.Peak:
                if (_activeSoldiers < maxActiveSoldiers) TrySpawnSoldier();
                if (_intensity < 0.2f)
                {
                    _dirState   = DirectorState.Relax;
                    _relaxTimer = relaxDuration;
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

        foreach (var node in spawnNodes)
        {
            if (node == null) continue;
            var bounds = new Bounds(node.position, Vector3.one * 1.5f);
            if (GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)) continue;

            var spawned = Instantiate(soldierPrefab, node.position, node.rotation);
            spawned.SetActive(true); // Ensure the copy is enabled/active
            _activeSoldiers++;
            _intensity += 0.12f;
            return;
        }
    }
}
