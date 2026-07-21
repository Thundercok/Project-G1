using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// Sandbox Mob Spawner Toolbox.
///
/// AUTO-BOOTSTRAPS: attaches itself to the Player in any scene whose name
/// contains "WeaponTest" or "TestScene" — no scene rebuild needed.
///
/// Press TAB (default) to open/close the panel.
/// While open, G1MobSpawnerToolbox.IsOpen == true, which WeaponBase reads
/// via InputLocked to hard-block all weapon fire.
///
/// Spawns coloured capsule mobs via G1SandboxMob (no Animator/prefab needed).
[DisallowMultipleComponent]
public class G1MobSpawnerToolbox : MonoBehaviour
{
    // ── Auto-bootstrap ────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!sceneName.Contains("WeaponTest") && !sceneName.Contains("TestScene"))
            return;

        // Find the Player and attach if not already present
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        if (player.GetComponent<G1MobSpawnerToolbox>() != null) return;

        player.AddComponent<G1MobSpawnerToolbox>();
        Debug.Log("[MOB SPAWNER] Auto-bootstrapped on Player.");
    }

    // ── Static weapon-fire gate (WeaponBase.InputLocked reads this) ───────────
    public static bool IsOpen { get; private set; }

    // ── Settings ──────────────────────────────────────────────────────────────
    public KeyCode toggleKey   = KeyCode.Tab;
    public float   spawnDist   = 8f;
    public float   spawnSpread = 2.5f;

    // ── Mob catalogue ─────────────────────────────────────────────────────────
    struct MobDef
    {
        public string label, tip;
        public Color  col;
        public float  hp, spd, dmg, range, interval;
        public int    count;
        public float  scale;
    }

    readonly MobDef[] _mobs =
    {
        new MobDef { label="Zombie",   tip="Slow · 20 dmg",     col=new Color(.25f,.70f,.25f), hp=100, spd=1.8f, dmg=20, range=1.8f, interval=1.5f, count=1, scale=1.0f },
        new MobDef { label="Soldier",  tip="Agile · 8 dmg",     col=new Color(.30f,.50f,.80f), hp= 80, spd=3.2f, dmg= 8, range=1.4f, interval=0.8f, count=1, scale=0.9f },
        new MobDef { label="Alien",    tip="Fast · 15 dmg",     col=new Color(.65f,.25f,.85f), hp= 90, spd=4.0f, dmg=15, range=2.0f, interval=1.0f, count=1, scale=1.0f },
        new MobDef { label="Horde ×5", tip="5 zombies",         col=new Color(.20f,.60f,.20f), hp=100, spd=1.8f, dmg=20, range=1.8f, interval=1.5f, count=5, scale=1.0f },
        new MobDef { label="Squad ×3", tip="Soldier fire-team", col=new Color(.25f,.40f,.75f), hp= 80, spd=3.2f, dmg= 8, range=1.4f, interval=0.8f, count=3, scale=0.9f },
        new MobDef { label="BOSS",     tip="Tank · 500 HP",     col=new Color(.85f,.18f,.10f), hp=500, spd=1.6f, dmg=35, range=2.2f, interval=1.8f, count=1, scale=1.6f },
    };

    readonly List<GameObject> _alive = new List<GameObject>();

    // ── GUI styles ────────────────────────────────────────────────────────────
    bool     _ready;
    GUIStyle _stPanel, _stTitle, _stLabel, _stBtn, _stKill;
    Texture2D _txPanel, _txBtn, _txKill;

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetOpen(!IsOpen);
    }

    void OnDestroy()
    {
        if (IsOpen) SetOpen(false);
    }

    void SetOpen(bool open)
    {
        IsOpen           = open;
        Cursor.lockState = open ? CursorLockMode.Confined : CursorLockMode.Locked;
        Cursor.visible   = open;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (!_ready) BuildStyles();

        // ── TAB hint ─────────────────────────────────────────────────────────
        GUI.color = IsOpen ? new Color(.5f,.9f,.5f,.9f) : new Color(1f,.9f,.3f,.85f);
        GUI.Label(new Rect(Screen.width - 252f, 14f, 238f, 22f),
                  IsOpen ? "[ TAB ] Close Spawner" : "[ TAB ] Mob Spawner", _stLabel);
        GUI.color = Color.white;

        if (!IsOpen) return;

        // ── Panel bounds ─────────────────────────────────────────────────────────────
        const float PW = 292f;
        float ph = 52f + _mobs.Length * 64f + 52f;
        float px = 16f;
        float py = Mathf.Max(10f, (Screen.height - ph) * 0.5f);
        var   panelRect = new Rect(px, py, PW, ph);

        GUI.Box(panelRect, GUIContent.none, _stPanel);

        float x = px + 14f;
        float y = py + 12f;

        GUI.Label(new Rect(x, y, PW - 28f, 26f), "⚙  MOB SPAWNER TOOLBOX", _stTitle);
        y += 32f;

        // ── Mob rows ──────────────────────────────────────────────────────────
        foreach (var mob in _mobs)
        {
            GUI.color = mob.col;
            GUI.DrawTexture(new Rect(x, y + 5f, 4f, 48f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 10f, y + 3f,  PW - 116f, 22f), mob.label, _stBtn);
            GUI.Label(new Rect(x + 10f, y + 24f, PW - 116f, 18f), mob.tip,   _stLabel);

            if (GUI.Button(new Rect(x + PW - 108f, y + 10f, 90f, 36f), "SPAWN", _stBtn))
                SpawnMob(mob);

            y += 64f;
        }

        // ── Kill-all ──────────────────────────────────────────────────────────
        y += 6f;
        int n = CountAlive();
        GUI.color = n > 0 ? new Color(1f, .35f, .35f) : new Color(.5f,.5f,.5f,.6f);
        if (GUI.Button(new Rect(x, y, PW - 28f, 38f), $"✕  KILL ALL  ({n} alive)", _stKill))
            KillAll();
        GUI.color = Color.white;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void SpawnMob(MobDef mob)
    {
        for (int i = 0; i < mob.count; i++)
            StartCoroutine(SpawnDelayed(mob, i * 0.08f));
    }

    IEnumerator SpawnDelayed(MobDef mob, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        SpawnOne(mob);
    }

    void SpawnOne(MobDef mob)
    {
        // Resolve NavMesh spawn point
        Transform cam   = Camera.main ? Camera.main.transform : transform;
        Vector3   fwd   = new Vector3(cam.forward.x, 0, cam.forward.z).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
        Vector3   scat  = new Vector3(Random.Range(-spawnSpread, spawnSpread), 0,
                                       Random.Range(-spawnSpread, spawnSpread));
        Vector3   want  = transform.position + fwd * spawnDist + scat;

        if (!NavMesh.SamplePosition(want, out NavMeshHit navHit, 6f, NavMesh.AllAreas))
        {
            // Expand search radius and try again
            if (!NavMesh.SamplePosition(transform.position + fwd * 4f, out navHit, 10f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[MOB SPAWNER] No NavMesh near spawn point. Run G1 > Build Weapon Testing Range first.");
                return;
            }
        }

        // Build capsule
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"{mob.label.Replace(" ", "")}_{_alive.Count}";
        go.transform.position   = navHit.position + Vector3.up * 0.02f;
        go.transform.localScale = Vector3.one * mob.scale;
        go.tag = "Enemy";

        // Visual material
        var rend     = go.GetComponent<Renderer>();
        var mat      = new Material(Shader.Find("Standard")) { color = mob.col };
        mat.SetFloat("_Metallic",   0.1f);
        mat.SetFloat("_Smoothness", 0.3f);
        rend.sharedMaterial = mat;

        // Head sphere
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0, 0.85f, 0);
        head.transform.localScale    = Vector3.one * 0.55f;
        head.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(head.GetComponent<Collider>());

        // HealthSystem
        var hs       = go.AddComponent<HealthSystem>();
        hs.maxHealth = mob.hp;

        // Health bar
        go.AddComponent<WorldSpaceHealthBar>();

        // NavMeshAgent
        var agent          = go.AddComponent<NavMeshAgent>();
        agent.radius       = 0.38f;
        agent.height       = 2f;
        agent.speed        = mob.spd;
        agent.angularSpeed = 240f;
        agent.acceleration = 14f;
        agent.stoppingDistance = mob.range * 0.65f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

        // AI brain
        var brain            = go.AddComponent<G1SandboxMob>();
        brain.damage         = mob.dmg;
        brain.attackRange    = mob.range;
        brain.attackInterval = mob.interval;

        // Track alive
        _alive.Add(go);
        hs.OnDeath += (p, n) =>
        {
            _alive.Remove(go);
            G1Audio.Play("enemy_death", p, 0.7f);
        };

        G1Audio.Play2D("door_servo", 0.4f, 1.75f);
        Debug.Log($"[MOB SPAWNER] {go.name} spawned at {navHit.position:F1}");
    }

    void KillAll()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i]) Destroy(_alive[i]);
        _alive.Clear();
        G1Audio.Play2D("door_servo", 0.55f, 0.7f);
    }

    int CountAlive()
    {
        _alive.RemoveAll(g => g == null);
        return _alive.Count;
    }

    // ── GUI style builder ────────────────────────────────────────────────────
    void BuildStyles()
    {
        _ready   = true;
        _txPanel = MkTex(new Color(.04f,.05f,.07f,.95f));
        _txBtn   = MkTex(new Color(.14f,.18f,.24f,1f));
        _txKill  = MkTex(new Color(.28f,.05f,.05f,1f));

        _stPanel = S(GUI.skin.box);   _stPanel.normal.background = _txPanel;
        _stPanel.border = new RectOffset(4,4,4,4);

        _stTitle = S(GUI.skin.label); _stTitle.fontSize = 14; _stTitle.fontStyle = FontStyle.Bold;
        _stTitle.normal.textColor = new Color(1f,.80f,.15f);

        _stLabel = S(GUI.skin.label); _stLabel.fontSize = 10;
        _stLabel.normal.textColor = new Color(.65f,.70f,.78f);

        _stBtn = S(GUI.skin.button);  _stBtn.fontSize = 11; _stBtn.fontStyle = FontStyle.Bold;
        _stBtn.normal.background = _txBtn; _stBtn.normal.textColor = Color.white;
        _stBtn.hover.textColor   = new Color(1f,.85f,.2f);

        _stKill = S(GUI.skin.button); _stKill.fontSize = 12; _stKill.fontStyle = FontStyle.Bold;
        _stKill.normal.background = _txKill; _stKill.normal.textColor = new Color(1f,.45f,.45f);
        _stKill.hover.textColor   = Color.white;
    }

    static GUIStyle  S(GUIStyle src)  => new GUIStyle(src);
    static Texture2D MkTex(Color c)   { var t = new Texture2D(1,1); t.SetPixel(0,0,c); t.Apply(); return t; }
}
