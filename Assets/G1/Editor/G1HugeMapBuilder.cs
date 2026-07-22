using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Builds "The Corvus Sprawl" — a huge two-faction battlefield on the
/// Blender-generated HugeMap.fbx, populated with many allies (scientists +
/// security) and enemies (HECU, zombies, aliens, elites, a gunship, the
/// Auditor). Menu: G1 → Build Huge Battlefield.
public static class G1HugeMapBuilder
{
    const string Models = "Assets/G1/Models";
    const string MapFbx = "Assets/G1/Models/Environment/HugeMap.fbx";
    const string ScenePath = "Assets/Scenes/HugeMap.unity";

    static Material Mat(Color c, float emission = 0f)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        if (emission > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * emission); }
        return m;
    }

    static int EnsureLayer(string name)
    {
        var tm = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < 32; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
        for (int i = 8; i < 32; i++)
        {
            var sp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            { sp.stringValue = name; tm.ApplyModifiedProperties(); return i; }
        }
        return 0;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    [MenuItem("G1/Build Huge Battlefield")]
    public static void BuildHugeMap()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building scenes.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- lighting: overcast battlefield
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(52f, -40f, 0f);
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.96f, 0.9f);
        sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.36f, 0.4f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 240f;
        RenderSettings.fogColor = new Color(0.4f, 0.43f, 0.48f);

        int enemyLayer = EnsureLayer("Enemy");

        // --- the map
        var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapFbx);
        if (mapPrefab == null)
        {
            Debug.LogError("HugeMap.fbx missing — run Tools/blender/build_huge_map.py first.");
            return;
        }
        var map = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab);
        map.name = "CorvusSprawl";
        map.transform.position = Vector3.zero;
        foreach (var mf in map.GetComponentsInChildren<MeshFilter>())
        {
            var mc = mf.gameObject.GetComponent<MeshCollider>();
            if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }

        // --- player (spawns at the south gate, facing the sprawl)
        var player = G1SceneBuilder.BuildStandardPlayer();
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.position = new Vector3(0f, 0.2f, -96f);
        player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        if (cc) cc.enabled = true;
        var card = player.GetComponent<G1StoryCard>();
        if (card) { card.title = "THE CORVUS SPRAWL"; card.subtitle = "Two factions. One battlefield."; }
        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null) switcher.unlocked = new[] { true, true, true, true, true, true };

        // ---------------- ALLIES (good side) ----------------
        // Security (blue) — hold the allied base and advance toward center.
        Vector3[] security = {
            new(-70, 0, -6), new(-70, 0, 6), new(-58, 0, -10), new(-58, 0, 10),
            new(-46, 0, 0), new(-40, 0, -14), new(-40, 0, 14), new(-30, 0, 0),
        };
        foreach (var p in security)
            SpawnAlly(p, true, new Color(0.22f, 0.4f, 0.7f), "Security");

        // Scientists (orange) — non-combatants around the lab/command.
        Vector3[] sci = {
            new(-8, 0, 60), new(8, 0, 60), new(0, 0, 52), new(-14, 0, 66),
            new(14, 0, 66), new(-6, 0, 20), new(6, 0, 20), new(0, 0, 30),
        };
        foreach (var p in sci)
            SpawnAlly(p, false, new Color(0.85f, 0.42f, 0.06f), "Scientist");

        // ---------------- ENEMIES (bad side) ----------------
        // HECU platoon — hangar / east, pushing toward center.
        Vector3[] hecu = {
            new(72, 0, 8), new(72, 0, -8), new(64, 0, 0), new(58, 0, 12),
            new(58, 0, -12), new(46, 0, 0), new(40, 0, 10), new(40, 0, -10),
            new(30, 0, 6), new(30, 0, -6),
        };
        foreach (var p in hecu)
            SpawnEnemy("Assets/G1/Prefabs/HECUSoldier.prefab", p, enemyLayer, 250f);

        // Zombies (the Taken) — pouring from the southern ruins.
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            SpawnEnemy("Assets/G1/Prefabs/Zombie.prefab",
                new Vector3(Mathf.Cos(a) * 18f, 0f, -70f + Mathf.Sin(a) * 12f), enemyLayer);
        }

        // Aliens (Strays) — the breach; a few elites, bigger and tougher.
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            var al = SpawnEnemy("Assets/G1/Prefabs/Alien.prefab",
                new Vector3(Mathf.Cos(a) * 10f, 0f, -70f + Mathf.Sin(a) * 8f), enemyLayer);
            if (al != null && i % 3 == 0)   // every third is an elite
            {
                al.name = "EliteAlien";
                al.transform.localScale = Vector3.one * 1.6f;
                var hp = al.GetComponent<HealthSystem>();
                if (hp) hp.maxHealth = 220f;
            }
        }

        // Gunship boss — patrols the airspace over the plaza.
        BuildGunship(new Vector3(0f, 16f, 0f), enemyLayer);

        // The Auditor — atop the command tower, watching, unreachable.
        Cameo(new Vector3(0f, 18.5f, 0f), 180f);

        // --- navmesh over the map geometry only (Default layer)
        var navGo = new GameObject("NavMesh");
        var surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = 1 << 0;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();

        EnsureFolder("Assets/Scenes");
        AssetDatabase.DeleteAsset("Assets/Scenes/HugeMapNavMesh.asset");
        AssetDatabase.CreateAsset(surface.navMeshData, "Assets/Scenes/HugeMapNavMesh.asset");
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterScene();
        AssetDatabase.SaveAssets();
        Debug.Log("G1 HUGE MAP BUILD OK — Corvus Sprawl assembled with allies + enemies.");
    }

    // ------------------------------------------------------------- helpers
    static GameObject SpawnAlly(Vector3 pos, bool combat, Color tint, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Protagonist.fbx");
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/G1/Anim/Protagonist.controller");
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.position = pos;
        var anim = go.GetComponent<Animator>();
        if (!anim) anim = go.AddComponent<Animator>();
        if (ctrl) anim.runtimeAnimatorController = ctrl;
        int idx = 0;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var m = r.sharedMaterial != null ? new Material(r.sharedMaterial) : new Material(Shader.Find("Standard"));
            m.color = idx++ == 0 ? tint : tint * 0.6f;
            r.sharedMaterial = m;
        }
        var col = go.AddComponent<CapsuleCollider>();
        col.height = 1.8f; col.radius = 0.35f; col.center = new Vector3(0, 0.9f, 0);
        var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.height = 1.8f; agent.radius = 0.35f;
        agent.speed = combat ? 3.2f : 2.4f; agent.angularSpeed = 400f; agent.acceleration = 14f;
        var hp = go.AddComponent<HealthSystem>();
        hp.maxHealth = combat ? 120f : 70f;
        go.AddComponent<G1DeathPhysics>();
        var ally = go.AddComponent<G1Ally>();
        ally.combat = combat;
        go.AddComponent<AgentNavMeshWarp>();
        return go;
    }

    static GameObject SpawnEnemy(string path, Vector3 pos, int enemyLayer, float yaw = 0f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("Missing " + path + " — build Level 1 (Test Scene) first to generate enemy prefabs.");
            return null;
        }
        var go = (GameObject)Object.Instantiate(prefab, pos, Quaternion.Euler(0, yaw, 0));
        go.name = prefab.name;
        SetLayerRecursive(go, enemyLayer);
        if (go.GetComponent<AgentNavMeshWarp>() == null)
            go.AddComponent<AgentNavMeshWarp>();
        return go;
    }

    static void BuildGunship(Vector3 pos, int enemyLayer)
    {
        var boss = new GameObject("GunshipBoss");
        boss.transform.position = pos;
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "GunshipBody"; body.transform.SetParent(boss.transform, false);
        body.transform.localScale = new Vector3(2.2f, 1.5f, 4.8f);
        body.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.16f, 0.2f, 0.16f));
        var rotor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(rotor.GetComponent<Collider>());
        rotor.name = "GunshipRotor"; rotor.transform.SetParent(boss.transform, false);
        rotor.transform.localPosition = new Vector3(0, 1.1f, 0);
        rotor.transform.localScale = new Vector3(5.5f, 0.05f, 5.5f);
        rotor.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.1f, 0.1f, 0.12f));
        rotor.AddComponent<G1WeaponSpinner>();
        var hp = boss.AddComponent<HealthSystem>();
        hp.maxHealth = 500f;
        var b = boss.AddComponent<G1HelicopterBoss>();
        b.arenaCenter = new Vector3(0f, 0f, 0f);
        b.altitude = 16f; b.strafeWidth = 34f;
        var bar = boss.AddComponent<WorldSpaceHealthBar>();
        bar.heightOffset = 2.6f;
    }

    static void Cameo(Vector3 pos, float yaw)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Villain.fbx");
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/G1/Anim/Villain.controller");
        if (fbx == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = "TheAuditor";
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var anim = go.GetComponent<Animator>();
        if (!anim) anim = go.AddComponent<Animator>();
        if (ctrl) anim.runtimeAnimatorController = ctrl;
        go.AddComponent<G1GManCameo>();
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder("Assets", "Scenes");
    }

    static void RegisterScene()
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == ScenePath))
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
