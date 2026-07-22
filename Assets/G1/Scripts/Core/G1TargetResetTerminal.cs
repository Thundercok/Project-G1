using System.Collections.Generic;
using UnityEngine;

/// Interactive Supply-Depot terminal (press E): snapshots every practice
/// target and breakable crate at scene start, then on use destroys whatever
/// survives and respawns them all fresh. Self-contained — no builder support
/// needed beyond placing the terminal.
public sealed class G1TargetResetTerminal : MonoBehaviour, IUsable
{
    struct Spec
    {
        public PrimitiveType shape;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;
        public Color color;
        public float maxHealth;
        public bool breakable;
        public bool healthBar;
    }

    readonly List<Spec> specs = new List<Spec>();
    readonly List<GameObject> live = new List<GameObject>();

    void Start()
    {
        // Snapshot: crates (Breakable) and dummies (health bar, not an NPC,
        // not the player). Deferred a frame so all builders have finished.
        foreach (var hs in FindObjectsOfType<HealthSystem>())
        {
            if (hs.CompareTag("Player"))
                continue;
            if (hs.GetComponent<UnityEngine.AI.NavMeshAgent>())
                continue;   // live enemy, not a static target
            var mf = hs.GetComponent<MeshFilter>();
            var rend = hs.GetComponent<Renderer>();
            if (mf == null || rend == null)
                continue;

            var spec = new Spec
            {
                shape = mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Capsule")
                    ? PrimitiveType.Capsule : PrimitiveType.Cube,
                pos = hs.transform.position,
                rot = hs.transform.rotation,
                scale = hs.transform.localScale,
                color = rend.sharedMaterial != null ? rend.sharedMaterial.color : Color.grey,
                maxHealth = hs.maxHealth,
                breakable = hs.GetComponent<Breakable>() != null,
                healthBar = hs.GetComponent<WorldSpaceHealthBar>() != null,
            };
            specs.Add(spec);
            live.Add(hs.gameObject);
        }
    }

    public void OnUse(GameObject user)
    {
        for (int i = 0; i < live.Count; i++)
            if (live[i] != null)
                Destroy(live[i]);
        live.Clear();

        foreach (var s in specs)
            live.Add(Spawn(s));

        G1Audio.Play("door_servo", transform.position, 0.7f, 1.4f);
        Debug.Log("Targets reset");
    }

    static GameObject Spawn(Spec s)
    {
        // Build inactive so HealthSystem.Awake sees the correct maxHealth.
        var go = GameObject.CreatePrimitive(s.shape);
        go.SetActive(false);
        go.transform.SetPositionAndRotation(s.pos, s.rot);
        go.transform.localScale = s.scale;
        var mat = new Material(Shader.Find("Standard"));
        mat.color = s.color;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        var hs = go.AddComponent<HealthSystem>();
        hs.maxHealth = s.maxHealth;
        if (s.breakable)
            go.AddComponent<Breakable>();
        if (s.healthBar)
            go.AddComponent<WorldSpaceHealthBar>();

        go.SetActive(true);
        return go;
    }
}
