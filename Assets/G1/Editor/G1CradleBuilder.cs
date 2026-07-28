using System.Collections.Generic;
using System.Globalization;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Builds "Cradle Station" — the second level, on CradleStation.fbx.
///
/// The Sprawl is a battlefield you cross. This is an installation you work
/// through: a gatehouse, barracks people slept in last week, a warehouse, a
/// motor pool, a five-storey headquarters with a lift, a power hall, and a
/// research wing behind two airlocks. It is lit from inside rather than by a
/// setting sun, because the whole point of arriving here is that it should not
/// feel like the place you left.
///
/// Almost nothing here is typed twice. The Blender generator that built the
/// walls also declared every room, every lamp, every firing position and every
/// piece of interactive equipment, and this reads that manifest back. When a
/// shutter moves in the level, it moves because the script that cut the hole
/// for it said "shutter" at those coordinates.
///
/// Menu: G1 → Build Cradle Station.
public static class G1CradleBuilder
{
    const string Models = "Assets/G1/Models";
    const string MapFbx = "Assets/G1/Models/Environment/CradleStation.fbx";
    const string ScenePath = "Assets/Scenes/CradleStation.unity";

    // The gate is at Blender y = -200, and the export flips north/south, so it
    // arrives at Unity z = +200. The research wing the level runs toward is at
    // Unity z = -170. Everything below is in Unity space.
    static readonly Vector3 Spawn = new Vector3(0f, 0.4f, 224f);

    static Material Mat(Color c, float emission = 0f)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        if (emission > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * emission); }
        return m;
    }

    [MenuItem("G1/Build Cradle Station")]
    public static void BuildCradle()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building scenes.");
            return;
        }

        G1Rig.EnsureAvatars($"{Models}/Protagonist.fbx", $"{Models}/Villain.fbx",
                            $"{Models}/Soldier.fbx", $"{Models}/Robot.fbx");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- lighting: night shift
        //
        // The Sprawl gets a raking amber sunset. This gets the opposite: a
        // cold, high, weak moon and almost no ambient, so the level is lit by
        // its own fittings. That is what turns 22 interiors from rooms with
        // lamps in them into the only places worth being.
        var sun = new GameObject("Moon").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(52f, 138f, 0f);
        sun.intensity = 0.34f;
        sun.color = new Color(0.62f, 0.70f, 0.95f);
        sun.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.10f, 0.12f, 0.18f);
        RenderSettings.ambientEquatorColor = new Color(0.07f, 0.08f, 0.11f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.05f);

        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", 0.02f);
        sky.SetFloat("_AtmosphereThickness", 0.6f);
        sky.SetColor("_SkyTint", new Color(0.10f, 0.13f, 0.22f));
        sky.SetColor("_GroundColor", new Color(0.05f, 0.05f, 0.06f));
        sky.SetFloat("_Exposure", 0.35f);
        RenderSettings.skybox = sky;

        // Tighter than the Sprawl's dust: this is a smaller map and the fog is
        // doing a different job — hiding the far fence so the level feels like
        // a facility rather than a diorama on a plate.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 55f;
        RenderSettings.fogEndDistance = 320f;
        RenderSettings.fogColor = new Color(0.06f, 0.07f, 0.10f);

        int enemyLayer = G1HugeMapBuilder.EnsureLayer("Enemy");

        // --- the map
        var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapFbx);
        if (mapPrefab == null)
        {
            Debug.LogError("CradleStation.fbx missing — run " +
                           "Tools/blender/build_research_base.py first.");
            return;
        }
        var map = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab);
        map.name = "CradleStation";
        // photographed surfaces over the flat palette; the district colours
        // survive as a tint so the map is still readable by hue
        G1MapSkin.Apply(map);
        map.transform.position = Vector3.zero;
        foreach (var mf in map.GetComponentsInChildren<MeshFilter>())
        {
            var mc = mf.gameObject.GetComponent<MeshCollider>();
            if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }
        // Colliders created and moved in one editor frame are invisible to
        // raycasts until physics is told. Every placement probe below depends
        // on this, and without it they all run against an empty world.
        Physics.SyncTransforms();

        var floor = new GameObject("GroundCollider");
        floor.transform.position = new Vector3(0f, -0.25f, 0f);
        floor.AddComponent<BoxCollider>().size = new Vector3(500f, 0.5f, 500f);

        var manifest = G1MapManifest.Load(MapFbx);
        int lampCount = G1MapManifest.ApplyLighting(manifest);
        // The station is on emergency power when you arrive. Every fitting the
        // manifest installed comes up at a fraction of its rating, so throwing
        // the breaker in the turbine hall is felt in the room you are standing
        // in rather than reported to you.
        foreach (var l in Object.FindObjectsOfType<Light>())
            if (l.type != LightType.Directional) l.intensity *= 0.45f;
        int stocked = G1MapManifest.StockInteriors(manifest);
        int coverCount = G1MapManifest.ApplyCover(manifest);

        // --- player, at the gate looking in
        var player = G1SceneBuilder.BuildStandardPlayer();
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.position = Spawn;
        player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        if (cc) cc.enabled = true;
        var card = player.GetComponent<G1StoryCard>();
        if (card)
        {
            card.title = "CRADLE STATION";
            card.subtitle = "Corvus Robotics & Research — 04:10, night shift";
        }
        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null) switcher.unlocked = new[] { true, true, true, true, true, true };
        player.AddComponent<G1MissionAssistant>();
        int interiorCount = G1MapManifest.ApplyInteriorSpaces(manifest);

        // --- mission
        var mgr = new GameObject("MissionManager");
        mgr.AddComponent<G1ObjectiveManager>();
        var setup = mgr.AddComponent<G1MissionSetup>();
        setup.objectives = new[]
        {
            new G1MissionSetup.Def { id = "cradle_power", description = "Restore main power at the turbine hall", mandatory = true, count = 1 },
            new G1MissionSetup.Def { id = "cradle_robotics", description = "Reach the robotics bay and find the outbreak source", mandatory = true, count = 1 },
            // Killing things is the game, not a checklist item; it was a tick box for
        // doing what the player was going to do anyway.
        new G1MissionSetup.Def { id = "cradle_hosts", description = "Destroy parasitised units", mandatory = false, count = 8 },
            new G1MissionSetup.Def { id = "cradle_core", description = "Purge the containment core", mandatory = true, count = 1 },
            new G1MissionSetup.Def { id = "cradle_armoury", description = "Break into the armoury", mandatory = false, count = 1 },
        };

        worldOffset = Vector3.zero;
        BuildExtraction(new Vector3(0f, 0f, 214f));

        int devices = ApplyDevices(manifest, enemyLayer);
        int armour = G1VehicleBuilder.ParkArmour(new[]
        {
            (new Vector3(126f, 0f, -78f), 0f, "Tank"),
            (new Vector3(174f, 0f, -78f), 0f, "Apc"),
            (new Vector3(126f, 0f, -96f), 0f, "Apc"),
            (new Vector3(-116f, 0f, 176f), 90f, "Apc"),   // gatehouse approach
        });

        int hosts = SpawnHosts(enemyLayer);

        // --- navmesh over the map geometry only
        var navGo = new GameObject("NavMesh");
        var surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = 1 << 0;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();

        Physics.SyncTransforms();
        int prunedCover = G1MapManifest.PruneCover();

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        AssetDatabase.DeleteAsset("Assets/Scenes/CradleNavMesh.asset");
        AssetDatabase.CreateAsset(surface.navMeshData, "Assets/Scenes/CradleNavMesh.asset");
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterScene();
        AssetDatabase.SaveAssets();

        Debug.Log($"G1 CRADLE BUILD OK — 480x480m, {(manifest != null ? manifest.rooms.Length : 0)} interiors, " +
                  $"{lampCount} lights, {stocked} caches, " +
                  $"{coverCount - prunedCover} cover points ({prunedCover} pruned), " +
                  $"{interiorCount} acoustic spaces, {devices} devices, {armour} armour, " +
                  $"{hosts} parasitised units.");
    }

    /// Adds Cradle Station's objectives to whatever mission manager already
    /// exists, rather than creating a second one — in the shared world the
    /// Sprawl's manager is already in the scene and a second would leave the
    /// HUD reading from one and the triggers writing to the other.
    public static void AddObjectives()
    {
        var mgr = Object.FindObjectOfType<G1MissionSetup>();
        if (mgr == null)
        {
            Debug.LogWarning("G1: no mission manager to add Cradle objectives to.");
            return;
        }
        var list = new List<G1MissionSetup.Def>(mgr.objectives ?? new G1MissionSetup.Def[0]);
        foreach (var d in CradleObjectives)
        {
            bool have = false;
            foreach (var o in list) if (o.id == d.id) have = true;
            if (!have) list.Add(d);
        }
        mgr.objectives = list.ToArray();
    }

    static readonly G1MissionSetup.Def[] CradleObjectives =
    {
        new G1MissionSetup.Def { id = "cradle_power", description = "Restore main power at the turbine hall", mandatory = true, count = 1 },
        new G1MissionSetup.Def { id = "cradle_robotics", description = "Reach the robotics bay and find the outbreak source", mandatory = true, count = 1 },
        // Killing things is the game, not a checklist item; it was a tick box for
        // doing what the player was going to do anyway.
        new G1MissionSetup.Def { id = "cradle_hosts", description = "Destroy parasitised units", mandatory = false, count = 8 },
        new G1MissionSetup.Def { id = "cradle_core", description = "Purge the containment core", mandatory = true, count = 1 },
        new G1MissionSetup.Def { id = "cradle_armoury", description = "Break into the armoury", mandatory = false, count = 1 },
    };

    /// The way out, back through the gate you came in by. Gated on every
    /// mandatory objective, so it is a door you earn rather than one you can
    /// walk back through the moment the level gets frightening.
    static void BuildExtraction(Vector3 pos)
    {
        var gate = new GameObject("ExtractionGate");
        gate.transform.position = pos;
        var ringMat = Mat(new Color(0.15f, 0.5f, 0.5f));
        var rends = new List<Renderer>();
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(seg.GetComponent<Collider>());
            seg.name = "GateRing_" + i;
            seg.transform.SetParent(gate.transform, false);
            seg.transform.localPosition =
                new Vector3(Mathf.Cos(a) * 3.2f, 3.2f + Mathf.Sin(a) * 3.2f, 0f);
            seg.transform.localScale = Vector3.one * 0.5f;
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
        trig.AddComponent<G1Waypoint>().label = "EXTRACTION";
    }

    // ------------------------------------------------------------- devices
    /// Turn every manifest device entry into something that works.
    ///
    /// The geometry for all of these is already in the FBX and already has a
    /// collider; what is missing is a component and, for the moving ones, a
    /// transform to move. So each case here builds only the part that moves —
    /// a shutter panel, a boom arm, a lift car — and leaves the frame, housing
    /// and jambs as map geometry, which is why nothing has to be cut out of
    /// the mesh on this side.
    public static int PopulateDevices(G1MapManifest.Data data, Vector3 offset)
    {
        worldOffset = offset;
        return ApplyDevices(data, G1HugeMapBuilder.EnsureLayer("Enemy"));
    }

    public static int PopulateHosts(Vector3 offset)
    {
        worldOffset = offset;
        return SpawnHosts(G1HugeMapBuilder.EnsureLayer("Enemy"));
    }

    /// Shifts everything this builder places, so the same code can plant
    /// Cradle Station at the origin in its own scene or 1.1 km east of the
    /// Sprawl in the shared one.
    static Vector3 worldOffset = Vector3.zero;

    static int ApplyDevices(G1MapManifest.Data data, int enemyLayer)
    {
        if (data == null || data.devices == null) return 0;

        var root = new GameObject("Equipment");
        var locks = new Dictionary<string, List<MonoBehaviour>>();
        var readers = new Dictionary<string, List<GameObject>>();
        int n = 0;

        foreach (var d in data.devices)
        {
            if (d == null || string.IsNullOrEmpty(d.kind)) continue;
            var at = worldOffset + new Vector3(d.x, d.y, d.z);
            // Blender yaw is about +Z; Unity's is about +Y and the export flips
            // north/south, so the sign has to come with it.
            var rot = Quaternion.Euler(0f, -d.yaw * Mathf.Rad2Deg, 0f);

            switch (d.kind)
            {
                case "rollup":
                {
                    var go = new GameObject("Rollup_" + d.tag);
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(at, rot);
                    var shutter = new GameObject("Slats").transform;
                    shutter.SetParent(go.transform, false);
                    // The slats in the FBX are static map geometry. Rather than
                    // try to re-parent baked mesh, this drops a matching panel
                    // in front of them and slides that; the two read as one.
                    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    panel.name = "ShutterPanel";
                    panel.transform.SetParent(shutter, false);
                    panel.transform.localPosition = new Vector3(0f, 2.6f, -0.06f);
                    panel.transform.localScale = new Vector3(7.0f, 5.2f, 0.22f);
                    panel.GetComponent<Renderer>().sharedMaterial =
                        Mat(new Color(0.42f, 0.44f, 0.47f));
                    var rd = go.AddComponent<G1RollupDoor>();
                    rd.shutter = shutter;
                    rd.lift = 5.4f;
                    rd.label = "ROLLER SHUTTER";
                    Register(locks, d.tag, rd);
                    n++;
                    break;
                }

                case "barrier":
                {
                    var go = new GameObject("Barrier_" + d.tag);
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = at + Vector3.up * 1.25f;
                    var arm = new GameObject("Arm").transform;
                    arm.SetParent(go.transform, false);
                    var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    beam.name = "Boom";
                    beam.transform.SetParent(arm, false);
                    beam.transform.localPosition = new Vector3(4.5f, 0f, 0f);
                    beam.transform.localScale = new Vector3(9f, 0.24f, 0.24f);
                    beam.GetComponent<Renderer>().sharedMaterial =
                        Mat(new Color(0.88f, 0.76f, 0.08f));
                    var bb = go.AddComponent<G1BoomBarrier>();
                    bb.arm = arm;
                    Register(locks, d.tag, bb);
                    n++;
                    break;
                }

                case "blastdoor":
                {
                    var go = new GameObject("BlastDoor_" + d.tag);
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(at, rot);
                    var bd = go.AddComponent<G1BlastDoor>();
                    bd.leftPanel = Leaf(go.transform, -1f);
                    bd.rightPanel = Leaf(go.transform, 1f);
                    bd.travel = 2.0f;
                    bd.doorLabel = (d.tag ?? "DOOR").ToUpperInvariant();
                    // Locked by default: the reader beside it is the way in,
                    // and a door that has never been locked teaches the player
                    // that readers are decoration.
                    bd.locked = d.tag != "lab_inner";
                    Register(locks, d.tag, bd);
                    n++;
                    break;
                }

                case "keycard":
                {
                    var go = new GameObject("Reader_" + d.tag);
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(at, rot);
                    var col = go.AddComponent<BoxCollider>();
                    col.size = new Vector3(0.5f, 0.7f, 0.5f);
                    if (!readers.TryGetValue(d.tag ?? "", out var l))
                        readers[d.tag ?? ""] = l = new List<GameObject>();
                    l.Add(go);
                    n++;
                    break;
                }

                case "elevator":
                {
                    // tag is "<group>|<y0>,<y1>,..." — the stop heights come
                    // from the generator so the two can never disagree about
                    // where the third floor is
                    var parts = (d.tag ?? "").Split('|');
                    var stops = new List<float>();
                    if (parts.Length > 1)
                        foreach (var s in parts[1].Split(','))
                            if (float.TryParse(s, NumberStyles.Float,
                                               CultureInfo.InvariantCulture, out float f))
                                stops.Add(f);
                    if (stops.Count == 0) stops.Add(d.y);

                    var go = new GameObject("Lift_" + parts[0]);
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = at;
                    var carGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    carGo.name = "Car";
                    carGo.transform.SetParent(go.transform, false);
                    carGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    carGo.transform.localScale = new Vector3(2.8f, 0.2f, 2.8f);
                    carGo.GetComponent<Renderer>().sharedMaterial =
                        Mat(new Color(0.55f, 0.58f, 0.62f));
                    var lift = go.AddComponent<G1Lift>();
                    lift.car = carGo.transform;
                    lift.stops = stops.ToArray();
                    lift.label = "LIFT " + parts[0].ToUpperInvariant();
                    // the call button stands next to the shaft, not on the car
                    var btn = new GameObject("LiftCall");
                    btn.transform.SetParent(go.transform, false);
                    btn.transform.localPosition = new Vector3(2.2f, 1.3f, 0f);
                    btn.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.6f, 0.5f);
                    var relay = btn.AddComponent<G1UseRelay>();
                    relay.target = lift;
                    n++;
                    break;
                }

                case "fabricator":
                {
                    var go = new GameObject("Fabricator");
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(at + Vector3.up * 1.2f, rot);
                    go.AddComponent<BoxCollider>().size = new Vector3(2.4f, 2.6f, 1.6f);
                    var led = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    led.name = "Status";
                    led.transform.SetParent(go.transform, false);
                    led.transform.localPosition = new Vector3(0f, 1.35f, -0.7f);
                    led.transform.localScale = new Vector3(1.6f, 0.12f, 0.1f);
                    Object.DestroyImmediate(led.GetComponent<Collider>());
                    led.GetComponent<Renderer>().sharedMaterial =
                        Mat(new Color(0.15f, 0.85f, 1f), 2f);
                    var fab = go.AddComponent<G1Fabricator>();
                    fab.statusRenderer = led.GetComponent<Renderer>();
                    n++;
                    break;
                }

                case "terminal":
                {
                    var go = new GameObject("Terminal_" + d.tag);
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(at + Vector3.up * 1.2f, rot);
                    go.AddComponent<BoxCollider>().size = new Vector3(1.8f, 2.4f, 1.4f);
                    var t = go.AddComponent<G1Terminal>();
                    t.logMessage = string.Join("  //  ", TerminalText(d.tag));
                    n++;
                    break;
                }

                case "vehicle_spawn":
                    G1VehicleBuilder.SpawnAt(at, -d.yaw * Mathf.Rad2Deg);
                    n++;
                    break;

                case "ammo_cache":
                    for (int i = 0; i < 3; i++)
                        G1AmmoPack.Create(at + new Vector3(-2f + i * 2f, 0.5f, 1.5f));
                    G1ArmorPack.Create(at + new Vector3(0f, 0.5f, -1.5f));
                    n++;
                    break;

                case "breaker":
                {
                    // the objective that turns the lights on
                    var go = new GameObject("MainBreaker");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = at + Vector3.up * 1.3f;
                    go.AddComponent<BoxCollider>().size = new Vector3(2.4f, 2.6f, 2.0f);
                    var t = go.AddComponent<G1ObjectiveSwitch>();
                    t.objectiveId = "cradle_power";
                    t.message = "MAIN BUS ONLINE — SEALED DOORS RELEASED";
                    t.unlocksGroup = "lab_outer";
                    n++;
                    break;
                }

                case "reactor":
                {
                    var go = new GameObject("ContainmentCore");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = at + Vector3.up * 5f;
                    var col = go.AddComponent<CapsuleCollider>();
                    col.height = 10f; col.radius = 3.6f;
                    var hp = go.AddComponent<HealthSystem>();
                    hp.maxHealth = 900f;
                    go.AddComponent<G1ObjectiveOnDeath>().objectiveId = "cradle_core";
                    var wp = go.AddComponent<G1Waypoint>();
                    wp.label = "CONTAINMENT CORE";
                    wp.objectiveId = "cradle_core";
                    n++;
                    break;
                }

                case "outbreak_origin":
                {
                    var go = new GameObject("OutbreakZone");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = at + Vector3.up * 1.5f;
                    var col = go.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    col.size = new Vector3(30f, 6f, 20f);
                    go.AddComponent<G1QuestZone>().objectiveId = "cradle_robotics";
                    var wp = go.AddComponent<G1Waypoint>();
                    wp.label = "ROBOTICS BAY";
                    wp.objectiveId = "cradle_robotics";
                    n++;
                    break;
                }

                case "bunkroom":
                case "sample":
                case "mainframe":
                case "helipad":
                case "fuel":
                    // dressing the generator flagged but that needs no behaviour
                    // beyond what the geometry already does
                    break;
            }
        }

        // wire each reader to everything sharing its lock group
        foreach (var kv in readers)
        {
            if (!locks.TryGetValue(kv.Key, out var targets)) continue;
            foreach (var go in kv.Value)
            {
                var kc = go.AddComponent<G1Keycard>();
                kc.group = kv.Key;
                kc.targets = targets.ToArray();
                // The research wing runs off the main bus; the rest of the site
                // has local power. That is what makes the turbine hall a gate
                // rather than an errand.
                kc.powered = kv.Key != "lab_outer" && kv.Key != "containment";
                if (kv.Key == "armoury") kc.objectiveId = "cradle_armoury";
            }
        }
        return n;
    }

    static void Register(Dictionary<string, List<MonoBehaviour>> map, string key, MonoBehaviour mb)
    {
        key = key ?? "";
        if (!map.TryGetValue(key, out var l)) map[key] = l = new List<MonoBehaviour>();
        l.Add(mb);
    }

    static Transform Leaf(Transform parent, float side)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = side < 0 ? "LeafL" : "LeafR";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(side * 1.05f, 1.7f, 0f);
        go.transform.localScale = new Vector3(2.1f, 3.4f, 0.4f);
        go.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.58f, 0.61f, 0.65f));
        return go.transform;
    }

    static string[] TerminalText(string tag)
    {
        switch (tag)
        {
            case "gate": return new[] {
                "CRADLE STATION — VEHICLE CONTROL",
                "NIGHT SHIFT ROSTER: 41 ON SITE",
                "0347 — ROBOTICS BAY DECLARED CONTAINMENT EVENT",
                "0351 — SITE LOCKDOWN. NO EXIT AUTHORISED." };
            case "power": return new[] {
                "TURBINE HALL — SUPERVISORY",
                "MAIN BUS: OFFLINE. CAUSE: MANUAL TRIP FROM ROBOTICS.",
                "SOMEONE CUT THE POWER FROM INSIDE THE BAY.",
                "RESTORE AT THE BREAKER TO REOPEN SEALED DOORS." };
            case "robotics": return new[] {
                "ASSEMBLY LINE 3 — LOG",
                "0244 — SPECIMEN CRATE 11 OPENED. NO WORK ORDER FILED.",
                "0301 — UNIT 07 REJECTED SHUTDOWN. MOTOR CONTROL EXTERNAL.",
                "0312 — 'THEY ARE PUTTING THEM ON LIKE COATS.'" };
            case "servers": return new[] {
                "CORE RECORDS — READ ONLY",
                "SUBJECT CLASS: OBLIGATE NEURAL SYMBIONT",
                "PREFERS CHASSIS TO FLESH. FEWER OBJECTIONS.",
                "AIM FOR THE RIDER. THE MACHINE IS ONLY THE MACHINE." };
            case "containment": return new[] {
                "CONTAINMENT — CORE STATUS",
                "CULTURE MASS: 4.1 TONNES AND CLIMBING",
                "PURGE REQUIRES SUSTAINED FIRE ON THE CORE.",
                "DO NOT BE STANDING ON THE CATWALK WHEN IT GOES." };
            case "warehouse": return new[] {
                "LOGISTICS — SHUTTER CONTROL",
                "BAYS 1-3 OPERABLE ON LOCAL POWER." };
            case "motorpool": return new[] {
                "MOTOR POOL — DISPATCH",
                "THREE TRUCKS FUELLED AND KEYED.",
                "TAKE ONE. IT IS A LONG WAY TO THE RESEARCH WING." };
            default: return new[] { "CRADLE STATION", "TERMINAL OFFLINE" };
        }
    }

    // ------------------------------------------------------------- enemies
    /// Parasitised units, placed where the outbreak would have spread from.
    ///
    /// They start in the robotics bay and thin out with distance — the density
    /// gradient is the level telling the player which way the story goes
    /// without a marker doing it.
    static int SpawnHosts(int enemyLayer)
    {
        var prefabPath = $"{Models}/Robot.fbx";
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        // This level can be built on its own, so it cannot assume the arena
        // builder has already produced the robot's clips and controller.
        if (fbx != null &&
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/G1/Anim/Robot.controller") == null)
        {
            G1SceneBuilder.ConfigureFbx(prefabPath, loopAll: true);
            G1SceneBuilder.MakeNpcController(prefabPath, "Assets/G1/Anim/Robot.controller");
        }
        if (fbx == null)
        {
            Debug.LogWarning("G1: Robot.fbx missing — run build_character.py robot " +
                             "then rig_character.py; Cradle Station will be empty.");
            return 0;
        }

        // Unity space. Blender's robotics yard (-150, +130) lands at (-150, -130).
        Vector3[] at =
        {
            new(-150f, 0f, -130f), new(-138f, 0f, -124f), new(-162f, 0f, -124f),
            new(-150f, 0f, -112f), new(-150f, 0f, -96f),
            new(-150f, 0f, -30f),                                  // power hall
            new(0f, 0f, -160f), new(-40f, 0f, -178f), new(40f, 0f, -178f),  // labs
            new(0f, 0f, -212f),                                    // containment
            new(0f, 0f, -6f), new(150f, 0f, -60f),                 // HQ, motor pool
        };

        int n = 0;
        foreach (var p in at)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            go.name = "ParasitisedUnit";
            go.transform.position = G1Placement.FindStandingSpot(worldOffset + p, "host");
            G1Rig.Setup(go, prefabPath, "Assets/G1/Anim/Robot.controller");
            G1CharacterSkin.Apply(go, "Robot", Color.white, Color.white);
            G1HugeMapBuilder.SetLayerRecursive(go, enemyLayer);

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.8f; col.radius = 0.38f; col.center = new Vector3(0, 0.9f, 0);
            var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.height = 1.8f; agent.radius = 0.38f;
            agent.speed = 2.6f; agent.angularSpeed = 320f; agent.acceleration = 12f;

            // Armoured. The number is high on purpose: it has to be obviously
            // not worth shooting, or the weak point is just a bonus.
            var hp = go.AddComponent<HealthSystem>();
            hp.maxHealth = 420f;
            go.AddComponent<G1DeathPhysics>();
            go.AddComponent<AgentNavMeshWarp>();
            var f = go.AddComponent<G1FactionFighter>();
            f.faction = G1FactionFighter.Faction.Hostile;
            f.kind = G1FactionFighter.Kind.Melee;
            go.AddComponent<G1ObjectiveOnDeath>().objectiveId = "cradle_hosts";

            // the rider, on the shoulder yoke where the model puts it
            var rider = new GameObject("Parasite");
            rider.transform.SetParent(go.transform, false);
            rider.transform.localPosition = new Vector3(0f, 1.71f, 0.01f);
            var rc = rider.AddComponent<SphereCollider>();
            rc.radius = 0.19f;
            var ph = rider.AddComponent<G1ParasiteHost>();
            ph.host = hp;
            n++;
        }
        return n;
    }

    static void RegisterScene()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in list) if (s.path == ScenePath) return;
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
