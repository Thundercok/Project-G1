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

    public static int EnsureLayer(string name)
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

        // must happen before anything instantiates the character models
        G1Rig.EnsureAvatars($"{Models}/Protagonist.fbx", $"{Models}/Villain.fbx");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- lighting: dust and last light
        //
        // Flat overcast at midday is the one condition with no shape to it —
        // every surface gets the same grey and the map reads as a diagram. A
        // low sun does the opposite: long shadows across 800m of open ground,
        // one lit face and one dark face on every plate, and a horizon you can
        // navigate by.
        //
        // The warm/cool split is what makes it read as evening rather than as
        // "the same scene, dimmer": the sun goes amber and the ambient fill
        // goes blue, so shadow and light differ in hue and not only in value.
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(14f, -52f, 0f);   // low, raking
        sun.intensity = 0.78f;
        sun.color = new Color(1f, 0.72f, 0.44f);                    // late amber
        sun.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.20f, 0.21f, 0.27f);
        RenderSettings.ambientEquatorColor = new Color(0.15f, 0.14f, 0.14f);
        RenderSettings.ambientGroundColor = new Color(0.09f, 0.08f, 0.07f);

        // a dusty sky rather than Unity's clear blue default
        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", 0.05f);
        sky.SetFloat("_AtmosphereThickness", 2.2f);   // thick air holds the dust
        sky.SetColor("_SkyTint", new Color(0.40f, 0.33f, 0.27f));
        sky.SetColor("_GroundColor", new Color(0.13f, 0.11f, 0.09f));
        sky.SetFloat("_Exposure", 0.62f);
        RenderSettings.skybox = sky;

        // fog carries the dust: browner, and close enough that distance reads
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 470f;
        RenderSettings.fogColor = new Color(0.29f, 0.25f, 0.20f);

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
        // Placement probes everything below by raycast, and in edit mode the
        // physics engine does not see a collider's transform until it is told.
        // Skip this and every probe runs against the map as it was at the
        // origin the instant it was instantiated.
        Physics.SyncTransforms();

        // GUARANTEED floor: a flat box collider spanning the whole 800m map at
        // ground level, so the player always has something to stand on even if
        // the FBX ground mesh ever imports at an unexpected scale.
        var floor = new GameObject("GroundCollider");
        floor.transform.position = new Vector3(0f, -0.25f, 0f);
        var floorCol = floor.AddComponent<BoxCollider>();
        floorCol.size = new Vector3(820f, 0.5f, 820f);   // top surface at y=0

        // interiors, floodlights and the caches that make them worth entering,
        // all read from the manifest the Blender generator writes
        var manifest = G1MapManifest.Load(MapFbx);
        int lampCount = G1MapManifest.ApplyLighting(manifest);
        int stocked = G1MapManifest.StockInteriors(manifest);
        int coverCount = G1MapManifest.ApplyCover(manifest);

        // --- player (spawns outside the south gate, facing the sprawl)
        var player = G1SceneBuilder.BuildStandardPlayer();
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.position = new Vector3(0f, 0.3f, -378f);
        player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        if (cc) cc.enabled = true;
        var card = player.GetComponent<G1StoryCard>();
        if (card) { card.title = "THE CORVUS SPRAWL"; card.subtitle = "Two factions. One battlefield."; }
        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null) switcher.unlocked = new[] { true, true, true, true, true, true };
        player.AddComponent<G1MissionAssistant>();
        // needs the player, so it can't ride along with the lighting pass above
        int interiorCount = G1MapManifest.ApplyInteriorSpaces(manifest);

        // ---------------- MISSION ----------------
        var mgr = new GameObject("MissionManager");
        mgr.AddComponent<G1ObjectiveManager>();
        var setup = mgr.AddComponent<G1MissionSetup>();
        setup.objectives = new[]
        {
            new G1MissionSetup.Def { id = "rescue", description = "Rescue the stranded researchers", mandatory = true, count = 3 },
            new G1MissionSetup.Def { id = "gunship", description = "Destroy the HECU gunship", mandatory = false, count = 1 },
        };

        // three survivors stranded across the districts — the mandatory spine
        // that unlocks the extraction gate.
        SpawnRescuable(new Vector3(-150f, 0.1f, 20f));    // allied base (west)
        SpawnRescuable(new Vector3(150f, 0.1f, -10f));    // hangar (east)
        SpawnRescuable(new Vector3(-88f, 0.1f, -142f));   // southern ruins

        // the contact network: seven quest-givers spread wider than one scan
        // can reach, so finding them (Q) is its own piece of play.
        G1QuestNpcBuilder.PopulateSprawl();

        // architecture + the opening beat: sealed south gate, SGT. KANE's
        // briefing, the picket that wakes when he opens it, bunkers, compounds.
        // Must run before the NavMesh bake below.
        G1DoorKitBuilder.Build();

        // the main storyline: chapter cards and voiced narration hung off the
        // objectives the contacts hand out, plus the Threshold ring it ends at.
        // After the contacts exist, so the chapters have something to watch.
        G1StoryBuilder.Build();

        // drivable trucks — an 800m map is a lot of walking and sprint only
        // buys seven seconds of it. Parked where the player already stands.
        int trucks = G1VehicleBuilder.Build();

        // lifts to the high ground the map already has — including the command
        // tower, whose roof has held the Auditor and no way up since day one
        int lifts = G1ElevatorBuilder.Build();

        // extraction teleport gate on the plaza's south approach, gated on ALL
        // mandatory objectives (rescues + the engineer's quest).
        BuildExtractionGate(new Vector3(0f, 0f, -40f));

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

        // ---------------- THE OUTER RING (the new ground) ----------------
        // The extra 200m of map has to be worth crossing, so each new district
        // gets a garrison sized to its role rather than a uniform sprinkle.

        // Airstrip (far east): HECU hold it, dug in among the revetments.
        foreach (var p in new Vector3[] { new(258, 0, -90), new(258, 0, 30),
            new(250, 0, 96), new(232, 0, 108), new(224, 0, -60) })
            SpawnEnemy("Assets/G1/Prefabs/HECUSoldier.prefab", p, enemyLayer, 270f);

        // Ammo bunker field (far north): a small guard detail, plus survivors
        // who went to ground in the igloos when the line broke.
        foreach (var p in new Vector3[] { new(-96, 0, 306), new(32, 0, 306), new(160, 0, 306) })
            SpawnEnemy("Assets/G1/Prefabs/HECUSoldier.prefab", p, enemyLayer, 180f);
        SpawnAlly(new Vector3(-160f, 0f, 306f), false, new Color(0.85f, 0.42f, 0.06f), "Scientist");

        // Tank park (far west): allied ground — mechanics working, riflemen
        // covering the approach.
        foreach (var p in new Vector3[] { new(-296, 0, -96), new(-296, 0, 32), new(-290, 0, 96) })
            SpawnAlly(p, true, new Color(0.22f, 0.4f, 0.7f), "Security");
        foreach (var p in new Vector3[] { new(-352, 0, 8), new(-346, 0, -12) })
            SpawnAlly(p, false, new Color(0.85f, 0.42f, 0.06f), "Scientist");

        // The Taken have overrun the training ground in the SW corner.
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            SpawnEnemy("Assets/G1/Prefabs/Zombie.prefab",
                new Vector3(-300f + Mathf.Cos(a) * 22f, 0f, -300f + Mathf.Sin(a) * 18f),
                enemyLayer);
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

        // only meaningful once the navmesh exists
        Physics.SyncTransforms();
        int prunedCover = G1MapManifest.PruneCover();

        EnsureFolder("Assets/Scenes");
        AssetDatabase.DeleteAsset("Assets/Scenes/HugeMapNavMesh.asset");
        AssetDatabase.CreateAsset(surface.navMeshData, "Assets/Scenes/HugeMapNavMesh.asset");
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterScene();
        AssetDatabase.SaveAssets();
        Debug.Log($"G1 HUGE MAP BUILD OK — Corvus Sprawl 800x800m, " +
                  $"{(manifest != null ? manifest.rooms.Length : 0)} interiors, " +
                  $"{lampCount} lights, {stocked} caches, " +
                  $"{coverCount - prunedCover} cover points ({prunedCover} pruned), " +
                  $"{interiorCount} acoustic spaces, {trucks} trucks, {lifts} lifts.");
    }

    // ------------------------------------------------------------- helpers
    public static GameObject SpawnAlly(Vector3 pos, bool combat, Color tint, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Protagonist.fbx");
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.position = pos;
        G1Rig.Setup(go, $"{Models}/Protagonist.fbx", "Assets/G1/Anim/Protagonist.controller");
        G1CharacterSkin.Apply(go, "Protagonist", tint, tint * 0.55f);
        var col = go.AddComponent<CapsuleCollider>();
        col.height = 1.8f; col.radius = 0.35f; col.center = new Vector3(0, 0.9f, 0);
        var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.height = 1.8f; agent.radius = 0.35f;
        agent.speed = combat ? 3.2f : 2.4f; agent.angularSpeed = 400f; agent.acceleration = 14f;
        var hp = go.AddComponent<HealthSystem>();
        hp.maxHealth = combat ? 120f : 70f;
        go.AddComponent<G1DeathPhysics>();
        if (combat)
        {
            var f = go.AddComponent<G1FactionFighter>();
            f.faction = G1FactionFighter.Faction.Allied;
            f.kind = G1FactionFighter.Kind.Ranged;
        }
        else
        {
            go.AddComponent<G1Ally>().combat = false;   // scientists just flee
        }
        go.AddComponent<AgentNavMeshWarp>();
        return go;
    }

    public static GameObject SpawnEnemy(string path, Vector3 pos, int enemyLayer, float yaw = 0f)
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

        // Convert campaign enemy into a battlefield faction fighter: disable the
        // player-chasing campaign AI (keep components for RequireComponent) and
        // add a Hostile fighter — HECU shoot, zombies/aliens melee.
        if (go.TryGetComponent(out G1SoldierAI sai)) sai.enabled = false;
        if (go.TryGetComponent(out G1SoldierBarks sbk)) sbk.enabled = false;
        if (go.TryGetComponent(out G1ZombieAI zai)) zai.enabled = false;
        if (go.TryGetComponent(out G1AlienAI aai)) aai.enabled = false;
        if (go.TryGetComponent(out NPCController npc)) npc.enabled = false;
        var f = go.AddComponent<G1FactionFighter>();
        f.faction = G1FactionFighter.Faction.Hostile;
        bool ranged = prefab.name.Contains("HECU") || prefab.name.Contains("Soldier");
        f.kind = ranged ? G1FactionFighter.Kind.Ranged : G1FactionFighter.Kind.Melee;
        if (!ranged) { f.damage = 14f; f.fireInterval = 1.1f; }   // melee cadence
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
        boss.AddComponent<G1ObjectiveOnDeath>().objectiveId = "gunship";
    }

    static void SpawnRescuable(Vector3 pos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Protagonist.fbx");
        if (prefab == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "Survivor";
        go.transform.position = G1Placement.FindStandingSpot(pos, "Survivor");
        G1Rig.Setup(go, $"{Models}/Protagonist.fbx", "Assets/G1/Anim/Protagonist.controller");
        G1CharacterSkin.Apply(go, "Protagonist",
                              new Color(0.9f, 0.85f, 0.3f), new Color(0.5f, 0.47f, 0.18f));
        go.AddComponent<G1Rescuable>().objectiveId = "rescue";
    }

    static void BuildExtractionGate(Vector3 pos)
    {
        var gate = new GameObject("ExtractionGate");
        gate.transform.position = pos;
        var ringMat = Mat(new Color(0.15f, 0.5f, 0.5f));   // dim until objectives done
        var rends = new System.Collections.Generic.List<Renderer>();
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(seg.GetComponent<Collider>());
            seg.name = "GateRing_" + i;
            seg.transform.SetParent(gate.transform, false);
            seg.transform.localPosition = new Vector3(Mathf.Cos(a) * 3.2f, 3.2f + Mathf.Sin(a) * 3.2f, 0f);
            seg.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            seg.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
            seg.GetComponent<Renderer>().sharedMaterial = ringMat;
            rends.Add(seg.GetComponent<Renderer>());
        }
        gate.AddComponent<G1TeleportGate>().ringRenderers = rends.ToArray();

        var trig = new GameObject("ExtractionTrigger");
        trig.transform.position = pos + Vector3.up * 2f;
        var col = trig.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(6f, 5f, 3f);
        trig.AddComponent<G1LevelExitTrigger>().nextScene = "MenuScene";
        var wp = trig.AddComponent<G1Waypoint>();
        wp.label = "EXTRACTION";
    }

    static void Cameo(Vector3 pos, float yaw)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Villain.fbx");
        if (fbx == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = "TheAuditor";
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        G1Rig.Setup(go, $"{Models}/Villain.fbx", "Assets/G1/Anim/Villain.controller");
        // he keeps his own colours; the tint arguments are the material's own
        // so nothing is re-coloured, and his dirt map is near-white by design
        G1CharacterSkin.Apply(go, "Villain", Color.white, Color.white);
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
