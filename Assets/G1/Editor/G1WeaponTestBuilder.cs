using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class G1WeaponTestBuilder
{
    static Material Mat(Color c, float emission = 0f)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * emission);
        }
        return m;
    }

    static GameObject Slab(string name, Vector3 pos, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static GameObject TargetDummy(string name, Vector3 pos, Color c, float hp = 100f)
    {
        var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        dummy.name = name;
        dummy.transform.position = pos;
        var r = dummy.GetComponent<Renderer>();
        r.sharedMaterial = Mat(c);

        var health = dummy.AddComponent<HealthSystem>();
        health.maxHealth = hp;

        dummy.AddComponent<WorldSpaceHealthBar>();
        return dummy;
    }

    static void SpawnPickup(string name, G1WeaponPickup.WeaponType type, Vector3 pos, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        go.GetComponent<Renderer>().sharedMaterial = m;
        var col = go.GetComponent<BoxCollider>();
        col.isTrigger = true;

        var pickup = go.AddComponent<G1WeaponPickup>();
        pickup.weaponType = type;
        go.AddComponent<G1WeaponSpinner>();
    }

    static void SpawnHealthPickup(string name, Vector3 pos, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        go.GetComponent<Renderer>().sharedMaterial = m;
        var col = go.GetComponent<BoxCollider>();
        col.isTrigger = true;

        go.AddComponent<G1HealthPack>();
        go.AddComponent<G1WeaponSpinner>();
    }

    static void SpawnAmmoPickup(string name, Vector3 pos)
    {
        var ammo = G1AmmoPack.Create(pos);
        ammo.name = name;
    }

    [MenuItem("G1/Build Weapon Testing Range")]
    public static void BuildTestRange()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: Please exit Play Mode first before building scenes.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- 1. Lighting & Environment
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance = 75f;
        RenderSettings.fogColor = new Color(0.4f, 0.42f, 0.45f);

        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1.0f, 0.95f, 0.85f);
        sun.intensity = 1.3f;
        sunGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        // --- 2. Materials
        var matFloor = Mat(new Color(0.22f, 0.24f, 0.26f));
        var matWall = Mat(new Color(0.45f, 0.47f, 0.50f));
        var matConcrete = Mat(new Color(0.55f, 0.55f, 0.55f));
        var matHazard = Mat(new Color(0.85f, 0.65f, 0.10f), 0.3f);
        var matWood = Mat(new Color(0.55f, 0.38f, 0.22f));
        var matGreen = Mat(new Color(0.15f, 0.75f, 0.25f), 0.2f);

        // --- 3. Main Testing Arena Geometry (Width: 44m, Length: 60m)
        Slab("TestingDeck", new Vector3(0f, -0.25f, 15f), new Vector3(44f, 0.5f, 60f), matFloor);

        // Perimetral Walls
        Slab("WallBack", new Vector3(0f, 3f, -14f), new Vector3(44f, 6f, 1f), matWall);
        Slab("WallFront", new Vector3(0f, 3f, 44f), new Vector3(44f, 6f, 1f), matWall);
        Slab("WallLeft", new Vector3(-21.5f, 3f, 15f), new Vector3(1f, 6f, 60f), matWall);
        Slab("WallRight", new Vector3(21.5f, 3f, 15f), new Vector3(1f, 6f, 60f), matWall);

        // Firing Lane Dividers
        Slab("Divider1", new Vector3(-7f, 1.5f, 15f), new Vector3(0.4f, 3f, 50f), matConcrete);
        Slab("Divider2", new Vector3(7f, 1.5f, 15f), new Vector3(0.4f, 3f, 50f), matConcrete);

        // --- 4. Distance Markers (Z = 5m, 10m, 20m, 30m)
        float[] distances = new float[] { 5f, 10f, 20f, 30f };
        foreach (float z in distances)
        {
            Slab($"DistanceMarker_{z}m", new Vector3(0f, 0.01f, z), new Vector3(42f, 0.02f, 0.3f), matHazard);
        }

        // --- 5. Supply Depot & Pickup Stations (Near Player Spawn Z = -7m)
        Slab("SupplyDepotCounter", new Vector3(0f, 0.4f, -7f), new Vector3(12f, 0.8f, 1.2f), matConcrete);
        SpawnHealthPickup("HealthPack_1", new Vector3(-5f, 0.95f, -7f), matGreen);
        SpawnHealthPickup("HealthPack_2", new Vector3(-3f, 0.95f, -7f), matGreen);
        SpawnAmmoPickup("AmmoPack_1", new Vector3(-1f, 0.95f, -7f));
        SpawnAmmoPickup("AmmoPack_2", new Vector3(1f, 0.95f, -7f));
        SpawnAmmoPickup("AmmoPack_3", new Vector3(3f, 0.95f, -7f));
        SpawnPickup("GrenadeBox", G1WeaponPickup.WeaponType.Grenade, new Vector3(5f, 0.95f, -7f), matHazard);

        // --- 6. LANE 1: Short Range, Melee & Breakables (Left Lane: X = -14m)
        // Stack of breakable crates
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                var crate = Slab($"Crate_{x}_{y}", new Vector3(-15f + x * 0.85f, 0.45f + y * 0.85f, 5f), Vector3.one * 0.8f, matWood);
                var h = crate.AddComponent<HealthSystem>();
                h.maxHealth = 25f;
                crate.AddComponent<Breakable>();
            }
        }
        // Practice dummies at 5m, 10m, 15m
        TargetDummy("Dummy_Zombie_5m", new Vector3(-14f, 1f, 5f), new Color(0.3f, 0.6f, 0.3f), 100f);
        TargetDummy("Dummy_Zombie_10m", new Vector3(-12f, 1f, 10f), new Color(0.3f, 0.6f, 0.3f), 150f);
        TargetDummy("Dummy_Heavy_15m", new Vector3(-14f, 1f, 15f), new Color(0.7f, 0.2f, 0.2f), 300f);

        // --- 7. LANE 2: Medium Range & Precision Cover Combat (Center Lane: X = 0m)
        // Cover Slabs
        Slab("CoverSlab_10m", new Vector3(-2f, 0.6f, 10f), new Vector3(1.8f, 1.2f, 0.4f), matConcrete);
        Slab("CoverSlab_20m", new Vector3(2f, 0.6f, 20f), new Vector3(1.8f, 1.2f, 0.4f), matConcrete);

        // Practice dummies behind cover and open space
        TargetDummy("Dummy_Target_10m", new Vector3(0f, 1f, 10f), new Color(0.9f, 0.7f, 0.2f), 100f);
        TargetDummy("Dummy_Target_20m", new Vector3(-2f, 1f, 20f), new Color(0.9f, 0.7f, 0.2f), 100f);
        TargetDummy("Dummy_Target_30m", new Vector3(2f, 1f, 30f), new Color(0.9f, 0.7f, 0.2f), 100f);
        TargetDummy("Dummy_Boss_35m", new Vector3(0f, 1.2f, 35f), new Color(0.9f, 0.1f, 0.1f), 500f);

        // --- 8. LANE 3: Explosive & Ricochet Zone (Right Lane: X = 14m)
        // Ricochet backstop wall
        Slab("RicochetWall", new Vector3(14f, 2.5f, 25f), new Vector3(12f, 5f, 0.6f), matConcrete);
        
        // Group of low targets for splash damage testing
        TargetDummy("Dummy_Cluster_1", new Vector3(12f, 1f, 20f), new Color(0.6f, 0.4f, 0.8f), 100f);
        TargetDummy("Dummy_Cluster_2", new Vector3(14f, 1f, 20.5f), new Color(0.6f, 0.4f, 0.8f), 100f);
        TargetDummy("Dummy_Cluster_3", new Vector3(16f, 1f, 20f), new Color(0.6f, 0.4f, 0.8f), 100f);

        // Stacked crates against the wall for explosive demolition
        for (int i = 0; i < 4; i++)
        {
            var expCrate = Slab($"ExplosiveCrate_{i}", new Vector3(11f + i * 1.0f, 0.5f, 24f), Vector3.one * 0.9f, matWood);
            var h = expCrate.AddComponent<HealthSystem>();
            h.maxHealth = 10f;
            expCrate.AddComponent<Breakable>();
        }

        // --- 9. Player Spawn & Max Weapon Loadout Setup
        var player = G1SceneBuilder.BuildStandardPlayer();
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.position = new Vector3(0f, 0.05f, -10f);
        player.transform.rotation = Quaternion.identity;
        if (cc) cc.enabled = true;

        // Configure HUD Banner
        var card = player.GetComponent<G1StoryCard>();
        if (card)
        {
            card.title = "WEAPON TESTING RANGE";
            card.subtitle = "Target Firing Lanes & Unlimited Supply Depot";
        }

        // Unlock ALL weapons & Maximize Reserves
        var switcher = player.GetComponentInChildren<WeaponSwitcher>();
        if (switcher)
        {
            switcher.unlocked = new bool[] { true, true, true, true, true, true };
            switcher.Select(0); // Start with Crowbar
        }

        var pistol = player.GetComponentInChildren<G1Pistol>(true);
        if (pistol) pistol.reserve = 250;

        var smg = player.GetComponentInChildren<G1Smg>(true);
        if (smg) smg.reserve = 300;

        var shotgun = player.GetComponentInChildren<G1Shotgun>(true);
        if (shotgun) shotgun.reserve = 64;

        var magnum = player.GetComponentInChildren<G1Magnum>(true);
        if (magnum) magnum.reserve = 36;

        var grenade = player.GetComponentInChildren<G1Grenade>(true);
        if (grenade) grenade.count = 10;

        // Never run dry while testing
        player.AddComponent<G1InfiniteAmmoSandbox>();

        // --- 9b. Target Reset Terminal at the Supply Depot (press E)
        var terminal = Slab("TargetResetTerminal", new Vector3(7.5f, 0.9f, -7f),
                            new Vector3(0.7f, 1.4f, 0.7f), Mat(new Color(0.1f, 0.7f, 0.7f), 0.6f));
        terminal.AddComponent<G1TargetResetTerminal>();
        Slab("TargetResetSign", new Vector3(7.5f, 1.8f, -7f),
             new Vector3(0.9f, 0.5f, 0.1f), Mat(new Color(0.1f, 0.7f, 0.7f), 0.4f));

        // --- 10. Bake NavMesh & Save Scene
        var navGo = new GameObject("NavMesh");
        var surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = 1 << 0;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();

        string navAssetPath = "Assets/G1/NavMesh_WeaponTest.asset";
        AssetDatabase.DeleteAsset(navAssetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navAssetPath);

        string scenePath = "Assets/Scenes/WeaponTestScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        G1MenuBuilder.RegisterScenes();
        AssetDatabase.SaveAssets();

        Debug.Log($"✅ G1: Successfully built Weapon Testing Range scene at '{scenePath}'!");
    }
}
