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
        RenderSettings.fogStartDistance = 120f;
        RenderSettings.fogEndDistance = 520f;
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

        // GUARANTEED floor: a flat box collider spanning the whole 600m map at
        // ground level, so the player always has something to stand on even if
        // the FBX ground mesh ever imports at an unexpected scale.
        var floor = new GameObject("GroundCollider");
        floor.transform.position = new Vector3(0f, -0.25f, 0f);
        var floorCol = floor.AddComponent<BoxCollider>();
        floorCol.size = new Vector3(620f, 0.5f, 620f);   // top surface at y=0

        // --- player (spawns at the south gate, facing the sprawl)
        var player = G1SceneBuilder.BuildStandardPlayer();
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.position = new Vector3(0f, 0.3f, -278f);
        player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        if (cc) cc.enabled = true;
        var card = player.GetComponent<G1StoryCard>();
        if (card) { card.title = "THE CORVUS SPRAWL"; card.subtitle = "Two factions. One battlefield."; }
        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null) switcher.unlocked = new[] { true, true, true, true, true, true };

        // ---------------- ALLIES (good side) — many, spread across the west ----
        // Security (blue): a 14-strong line advancing from the Allied Base
        // (west, x≈-160) toward the central plaza.
        for (int i = 0; i < 14; i++)
        {
            float x = -170f + (i % 7) * 20f;       // two ranks pushing east
            float z = (i < 7 ? -1 : 1) * 18f + (i % 7 - 3) * 8f;
            SpawnAlly(new Vector3(x, 0, z), true, new Color(0.22f, 0.4f, 0.7f), "Security");
        }
        // A forward squad holding the plaza approaches.
        foreach (var p in new Vector3[] { new(-60, 0, 0), new(-48, 0, 16),
            new(-48, 0, -16), new(-30, 0, 0) })
            SpawnAlly(p, true, new Color(0.22f, 0.4f, 0.7f), "Security");

        // Scientists (orange, non-combat): clustered at the labs (north) and
        // the living quarters (NW).
        Vector3[] sci = {
            new(-26, 0, 150), new(26, 0, 150), new(0, 0, 140), new(-40, 0, 160),
            new(40, 0, 160), new(0, 0, 185),
            new(-150, 0, 140), new(-128, 0, 150), new(-150, 0, 170), new(-128, 0, 128),
        };
        foreach (var p in sci)
            SpawnAlly(p, false, new Color(0.85f, 0.42f, 0.06f), "Scientist");

        // ---------------- ENEMIES (bad side) — many, spread across the east/south ----
        // HECU platoon (16): the Hangar/Motor Pool (east, x≈165) pushing west,
        // plus a flanking squad from the Warehouse (NE).
        for (int i = 0; i < 12; i++)
        {
            float x = 175f - (i % 6) * 22f;
            float z = (i < 6 ? -1 : 1) * 18f + (i % 6 - 3) * 8f;
            SpawnEnemy("Assets/G1/Prefabs/HECUSoldier.prefab", new Vector3(x, 0, z), enemyLayer, 250f);
        }
        foreach (var p in new Vector3[] { new(150, 0, 150), new(130, 0, 140),
            new(160, 0, 128), new(120, 0, 160) })
            SpawnEnemy("Assets/G1/Prefabs/HECUSoldier.prefab", p, enemyLayer, 210f);

        // Zombies (the Taken, 16): pouring out of the southern breach ruins.
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            float r = 20f + (i % 3) * 14f;
            SpawnEnemy("Assets/G1/Prefabs/Zombie.prefab",
                new Vector3(Mathf.Cos(a) * r, 0f, -165f + Mathf.Sin(a) * r * 0.7f), enemyLayer);
        }

        // Aliens (Strays, 12): the breach; every third is a bigger, tougher elite.
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            var al = SpawnEnemy("Assets/G1/Prefabs/Alien.prefab",
                new Vector3(Mathf.Cos(a) * 14f, 0f, -165f + Mathf.Sin(a) * 10f), enemyLayer);
            if (al != null && i % 3 == 0)
            {
                al.name = "EliteAlien";
                al.transform.localScale = Vector3.one * 1.7f;
                var hp = al.GetComponent<HealthSystem>();
                if (hp) hp.maxHealth = 240f;
            }
        }

        // Gunship boss — patrols the airspace over the central plaza.
        BuildGunship(new Vector3(0f, 22f, 0f), enemyLayer);

        // The Auditor — atop the command tower, watching, unreachable.
        Cameo(new Vector3(0f, 38f, 0f), 180f);

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
        b.altitude = 22f; b.strafeWidth = 60f;
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
