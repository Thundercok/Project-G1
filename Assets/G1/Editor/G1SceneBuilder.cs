using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Builds the whole G1 test scene from code: run once via the menu
/// (G1 > Build Test Scene) or -executeMethod G1SceneBuilder.BuildScene.
public static class G1SceneBuilder
{
    const string Models = "Assets/G1/Models";
    const string AnimDir = "Assets/G1/Anim";
    const string MatDir = "Assets/G1/Materials";
    const string ScenePath = "Assets/Scenes/TestScene.unity";

    /// Arena generation parameters. Geometry randomness comes exclusively from
    /// an independent System.Random seeded with Seed, so rebuilds are exactly
    /// reproducible and the global UnityEngine.Random state is never touched.
    public sealed class ArenaConfig
    {
        public int Seed = 1337;
        public int Soldiers = 1;
        public int Zombies = 1;
        public int Aliens = 1;
        public int Crates = 6;
        public int CoverBlocks = 8;
        public int MaxActiveSoldiers = 4;
        public float RelaxDuration = 8f;

        public static ArenaConfig Standard() => new ArenaConfig();
        public static ArenaConfig SoloHecu() => new ArenaConfig
        {
            Seed = 101, Soldiers = 1, Zombies = 0, Aliens = 0,
            MaxActiveSoldiers = 1,
        };
        public static ArenaConfig Horde() => new ArenaConfig
        {
            Seed = 666, Soldiers = 2, Zombies = 3, Aliens = 3,
            Crates = 4, CoverBlocks = 10, MaxActiveSoldiers = 8,
            RelaxDuration = 3f,
        };
        public static ArenaConfig LowCover() => new ArenaConfig
        {
            Seed = 42, Soldiers = 2, Zombies = 1, Aliens = 1,
            Crates = 2, CoverBlocks = 2,
        };
    }

    [MenuItem("G1/Build Test Scene")]
    public static void BuildScene() => BuildScene(ArenaConfig.Standard());

    [MenuItem("G1/Rebuild Arena/Standard Arena")]
    static void MenuStandard() => BuildScene(ArenaConfig.Standard());

    [MenuItem("G1/Rebuild Arena/Solo HECU Test")]
    static void MenuSoloHecu() => BuildScene(ArenaConfig.SoloHecu());

    [MenuItem("G1/Rebuild Arena/Horde Overwhelm Test")]
    static void MenuHorde() => BuildScene(ArenaConfig.Horde());

    [MenuItem("G1/Rebuild Arena/Low Cover Test")]
    static void MenuLowCover() => BuildScene(ArenaConfig.LowCover());

    public static void BuildScene(ArenaConfig cfg)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: Cannot rebuild scene during Play Mode. Please exit Play Mode first.");
            return;
        }

        var rng = new System.Random(cfg.Seed);
        AssetDatabase.Refresh();
        EnsureFolder(AnimDir);
        EnsureFolder(MatDir);

        ConfigureFbx($"{Models}/Protagonist.fbx", loopAll: true);
        ConfigureFbx($"{Models}/Villain.fbx", loopAll: true);
        ConfigureFbx($"{Models}/Pistol.fbx", loopAll: false);   // only Idle loops
        ConfigureFbx($"{Models}/Smg.fbx", loopAll: false);
        ConfigureFbx($"{Models}/Shotgun.fbx", loopAll: false);
        ConfigureFbx($"{Models}/Magnum.fbx", loopAll: false);

        RuntimeAnimatorController protagonistCtrl =
            MakeNpcController($"{Models}/Protagonist.fbx", $"{AnimDir}/Protagonist.controller");
        RuntimeAnimatorController villainCtrl =
            MakeNpcController($"{Models}/Villain.fbx", $"{AnimDir}/Villain.controller");
        RuntimeAnimatorController pistolCtrl =
            MakePistolController($"{Models}/Pistol.fbx", $"{AnimDir}/Pistol.controller");
        RuntimeAnimatorController smgCtrl =
            MakePistolController($"{Models}/Smg.fbx", $"{AnimDir}/Smg.controller");
        RuntimeAnimatorController shotgunCtrl =
            MakePistolController($"{Models}/Shotgun.fbx", $"{AnimDir}/Shotgun.controller");
        RuntimeAnimatorController magnumCtrl =
            MakeMagnumController($"{Models}/Magnum.fbx", $"{AnimDir}/Magnum.controller");

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildLighting();
        BuildArena(cfg, rng);
        GameObject player = BuildPlayer(pistolCtrl, smgCtrl, shotgunCtrl, magnumCtrl);
        BuildNpcs(protagonistCtrl, villainCtrl, player.transform.position, cfg, rng);

        // Modern bake (com.unity.ai.navigation): a NavMeshSurface over the
        // Default layer only, so NPCs (Enemy layer) and the player never get
        // frozen into the mesh. The baked NavMeshData MUST be saved as an
        // asset — an in-memory-only mesh silently dies on the play-mode domain
        // reload ("Failed to create agent because there is no valid NavMesh").
        var navGo = new GameObject("NavMesh");
        var surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = 1 << 0;                     // Default layer geometry
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();
        EnsureFolder("Assets/Scenes");
        AssetDatabase.DeleteAsset("Assets/Scenes/TestSceneNavMesh.asset");
        AssetDatabase.DeleteAsset("Assets/Scenes/TestScene");   // stale legacy bake
        AssetDatabase.CreateAsset(surface.navMeshData, "Assets/Scenes/TestSceneNavMesh.asset");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log($"G1 BUILD OK (seed {cfg.Seed}: {cfg.Soldiers} soldiers, "
                  + $"{cfg.Zombies} zombies, {cfg.Aliens} aliens, "
                  + $"{cfg.CoverBlocks} cover blocks)");
    }

    // ------------------------------------------------------------- assets
    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = path.Substring(0, path.LastIndexOf('/'));
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }

    static void ConfigureFbx(string path, bool loopAll)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            int bar = clip.takeName.LastIndexOf('|');
            clip.name = bar >= 0 ? clip.takeName.Substring(bar + 1) : clip.takeName;
            clip.loopTime = loopAll || clip.name == "Idle";
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static AnimationClip LoadClip(string fbxPath, string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .First(c => !c.name.Contains("__preview__") && c.name.EndsWith(name));
    }

    static AnimatorController NewController(string ctrlPath)
    {
        AssetDatabase.DeleteAsset(ctrlPath);
        return AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
    }

    static RuntimeAnimatorController MakeNpcController(string fbxPath, string ctrlPath)
    {
        var ctrl = NewController(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = LoadClip(fbxPath, "Idle");
        sm.defaultState = idle;
        var walk = sm.AddState("Walk");
        walk.motion = LoadClip(fbxPath, "Walk");
        return ctrl;
    }

    static RuntimeAnimatorController MakePistolController(string fbxPath, string ctrlPath)
    {
        var ctrl = NewController(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = LoadClip(fbxPath, "Idle");
        sm.defaultState = idle;
        foreach (string name in new[] { "Fire", "Reload" })
        {
            var state = sm.AddState(name);
            state.motion = LoadClip(fbxPath, name);
            var back = state.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0.05f;
        }
        return ctrl;
    }

    static RuntimeAnimatorController MakeMagnumController(string fbxPath, string ctrlPath)
    {
        var ctrl = NewController(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = LoadClip(fbxPath, "Idle");
        sm.defaultState = idle;

        var fire = sm.AddState("Fire");
        fire.motion = LoadClip(fbxPath, "Fire");
        var fireToIdle = fire.AddTransition(idle);
        fireToIdle.hasExitTime = true;
        fireToIdle.exitTime = 1f;
        fireToIdle.duration = 0.05f;

        var rOpen = sm.AddState("ReloadOpen");
        rOpen.motion = LoadClip(fbxPath, "ReloadOpen");

        var rInsert = sm.AddState("ReloadInsert");
        rInsert.motion = LoadClip(fbxPath, "ReloadInsert");

        var rClose = sm.AddState("ReloadClose");
        rClose.motion = LoadClip(fbxPath, "ReloadClose");
        var rCloseToIdle = rClose.AddTransition(idle);
        rCloseToIdle.hasExitTime = true;
        rCloseToIdle.exitTime = 1f;
        rCloseToIdle.duration = 0.05f;

        return ctrl;
    }

    static Material MakeMat(string name, Color color, float smooth = 0.15f)
    {
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Glossiness", smooth);
        return mat;
    }

    static int EnsureLayer(string name)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        var tm = new SerializedObject(assets[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < 32; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == name)
                return i;
        for (int i = 8; i < 32; i++)
        {
            var sp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = name;
                tm.ApplyModifiedProperties();
                return i;
            }
        }
        Debug.LogWarning("No free layer slot; using Default");
        return 0;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
    // ------------------------------------------------------------- scene
    static void BuildLighting()
    {
        // Faint directional fill representing ventilation shaft moonlight
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(65f, -45f, 0f);
        sun.intensity = 0.12f;
        sun.color = new Color(0.7f, 0.8f, 1f);
        sun.shadows = LightShadows.None;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.10f); // dark cool facility shadow fill
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 15f;
        RenderSettings.fogEndDistance = 65f;
        RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.12f);
    }

    static Light SpawnLight(string name, Vector3 pos, Color color, float range, float intensity, LightShadows shadows = LightShadows.Soft)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var lt = go.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.color = color;
        lt.range = range;
        lt.intensity = intensity;
        lt.shadows = shadows;
        return lt;
    }

    static GameObject Slab(string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject SpawnModular(string assetName, Vector3 pos, Quaternion rot, Vector3 scale, Material fallbackMat)
    {
        string modelPath = $"Assets/G1/Models/Environment/{assetName}.fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (prefab != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = assetName;
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            if (go.GetComponent<Collider>() == null && go.GetComponentInChildren<Collider>() == null)
            {
                var col = go.AddComponent<BoxCollider>();
                var filter = go.GetComponentInChildren<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    col.center = filter.sharedMesh.bounds.center;
                    col.size = filter.sharedMesh.bounds.size;
                }
            }
            return go;
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"[Placeholder] {assetName}";
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = fallbackMat;
            return go;
        }
    }

    static GameObject SpawnWeaponPickup(string assetName, G1WeaponPickup.WeaponType type, Vector3 pos, Quaternion rot, Material mat)
    {
        string modelPath = $"Assets/G1/Models/{assetName}.fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        GameObject go;
        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = $"{assetName}_Pickup";
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.AddComponent<G1WeaponSpinner>();
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"[Pickup Placeholder] {assetName}";
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = new Vector3(0.5f, 0.2f, 0.2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            var triggerCol = go.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector3(0.8f, 0.8f, 0.8f);
        }

        var pickup = go.AddComponent<G1WeaponPickup>();
        pickup.weaponType = type;
        return go;
    }

    static void BuildArena(ArenaConfig cfg, System.Random rng)
    {
        Material concrete = MakeMat("Concrete", new Color(0.33f, 0.34f, 0.32f));
        Material floorMat = MakeMat("Floor", new Color(0.26f, 0.27f, 0.26f));
        Material hazard = MakeMat("HazardOrange", new Color(0.72f, 0.29f, 0.05f));
        Material wood = MakeMat("CrateWood", new Color(0.38f, 0.25f, 0.12f));
        Material doorMat = MakeMat("DoorSteel", new Color(0.45f, 0.36f, 0.16f), 0.4f);
        Material metalMat = MakeMat("PropMetal", new Color(0.4f, 0.42f, 0.45f));
        Material greenMat = MakeMat("IndustrialGreen", new Color(0.29f, 0.37f, 0.29f));

        // 1. LOCKER ROOM (START)
        Slab("LockerRoomFloor", new Vector3(0, -0.25f, -8f), new Vector3(12, 0.5f, 10), floorMat);
        Slab("LockerRoomWallS", new Vector3(0, 1.5f, -13f), new Vector3(12, 3, 0.5f), concrete);
        Slab("LockerRoomWallW", new Vector3(-6f, 1.5f, -8f), new Vector3(0.5f, 3, 10), concrete);
        Slab("LockerRoomWallE", new Vector3(6f, 1.5f, -8f), new Vector3(0.5f, 3, 10), concrete);
        Slab("LockerRoomWallNW", new Vector3(-4.5f, 1.5f, -3f), new Vector3(3, 3, 0.5f), concrete);
        Slab("LockerRoomWallNE", new Vector3(4.5f, 1.5f, -3f), new Vector3(3, 3, 0.5f), concrete);
        
        // Doorframe 1 (Locker Room to Corridor)
        Slab("LockerRoomFrameL", new Vector3(-1.6f, 1.25f, -3f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("LockerRoomFrameR", new Vector3(1.6f, 1.25f, -3f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("LockerRoomLintel", new Vector3(0, 2.6f, -3f), new Vector3(3.6f, 0.3f, 0.4f), concrete);
        var door1 = SpawnModular("door_sliding_auto", new Vector3(0, 1.1f, -3f), Quaternion.identity, new Vector3(1.6f, 2.2f, 0.18f), doorMat);
        door1.name = "SlidingDoor_1";
        door1.AddComponent<SlidingDoor>();

        for (int i = 0; i < 4; i++)
            SpawnModular("prop_filing_cabinet", new Vector3(-5.3f, 0.9f, -11f + i * 1.2f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.6f, 1.8f, 0.6f), metalMat);
        SpawnModular("prop_lab_table", new Vector3(0f, 0.45f, -8f), Quaternion.identity, new Vector3(1.6f, 0.9f, 0.8f), metalMat);
        var lrTerminal = SpawnModular("prop_computer_terminal", new Vector3(0f, 1.05f, -8f), Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f), floorMat);
        var lrTermComp = lrTerminal.AddComponent<G1Terminal>();
        lrTermComp.logMessage = "LOG: PORTAL CORE COLLAPSE DETECTED IN SECTOR C. CONTAINMENT DOORS SEALED. SYSTEM SHUTDOWN IMMINENT.";

        // Spawn Pistol pickup on Locker Room table
        SpawnWeaponPickup("Pistol", G1WeaponPickup.WeaponType.Pistol, new Vector3(0f, 1.05f, -8f), Quaternion.identity, concrete);

        // Spawn Locker Room Lights
        SpawnLight("LockerRoom_Light1", new Vector3(-3f, 2.5f, -8f), new Color(0.85f, 0.9f, 1f), 10f, 1.4f);
        SpawnLight("LockerRoom_Light2", new Vector3(3f, 2.5f, -8f), new Color(0.85f, 0.9f, 1f), 10f, 1.4f);

        // 2. LAB CORRIDOR
        Slab("CorridorFloor", new Vector3(0, -0.25f, 6.5f), new Vector3(4, 0.5f, 19), floorMat);
        Slab("CorridorWallW", new Vector3(-2f, 1.5f, 6.5f), new Vector3(0.5f, 3, 19), concrete);
        Slab("CorridorWallE", new Vector3(2f, 1.5f, 6.5f), new Vector3(0.5f, 3, 19), concrete);
        
        // Doorframe 2 (Corridor to Control Room)
        Slab("CorridorFrameL", new Vector3(-1f, 1.25f, 16f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("CorridorFrameR", new Vector3(1f, 1.25f, 16f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("CorridorLintel", new Vector3(0, 2.6f, 16f), new Vector3(2.4f, 0.3f, 0.4f), concrete);
        var door2 = SpawnModular("door_sliding_auto", new Vector3(0, 1.1f, 16f), Quaternion.identity, new Vector3(1.0f, 2.2f, 0.18f), doorMat);
        door2.name = "SlidingDoor_2";
        door2.AddComponent<SlidingDoor>();

        // Spawn Shotgun pickup in Lab Corridor
        SpawnWeaponPickup("Shotgun", G1WeaponPickup.WeaponType.Shotgun, new Vector3(-1f, 0.4f, 8f), Quaternion.identity, wood);

        // Spawn Lab Corridor Lights
        var l3 = SpawnLight("Corridor_Light1", new Vector3(0f, 2.5f, 3f), new Color(0.95f, 0.95f, 0.9f), 9f, 1.3f);
        l3.gameObject.AddComponent<G1LightEffects>().effectType = G1LightEffects.EffectType.Flicker;
        SpawnLight("Corridor_Light2", new Vector3(0f, 2.5f, 11f), new Color(0.95f, 0.95f, 0.9f), 9f, 1.3f);

        // 3. CONTROL ROOM
        Slab("ControlRoomFloor", new Vector3(6f, -0.25f, 22f), new Vector3(16, 0.5f, 12), floorMat);
        Slab("ControlRoomWallS_R", new Vector3(8f, 1.5f, 16f), new Vector3(12, 3, 0.5f), concrete);
        Slab("ControlRoomWallW", new Vector3(-2f, 1.5f, 22f), new Vector3(0.5f, 3, 12), concrete);
        Slab("ControlRoomWallE", new Vector3(14f, 1.5f, 22f), new Vector3(0.5f, 3, 12), concrete);
        Slab("ControlRoomWallN_L", new Vector3(2f, 1.5f, 28f), new Vector3(8, 3, 0.5f), concrete);
        Slab("ControlRoomWallN_R", new Vector3(12f, 1.5f, 28f), new Vector3(4, 3, 0.5f), concrete);
        
        // Window overlooking Industrial Hall
        var glass = Slab("ControlRoomWindow", new Vector3(8f, 1.5f, 28f), new Vector3(4, 3, 0.1f), MakeMat("WindowGlass", new Color(0.2f, 0.6f, 0.7f, 0.3f), 0.9f));
        glass.GetComponent<Collider>().isTrigger = true;

        // Doorframe 3 (Control Room to Industrial Hall)
        Slab("ControlRoomExitFrameL", new Vector3(-2.2f, 1.25f, 28f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("ControlRoomExitFrameR", new Vector3(0.2f, 1.25f, 28f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("ControlRoomExitLintel", new Vector3(-1f, 2.6f, 28f), new Vector3(2.4f, 0.3f, 0.4f), concrete);
        var door3 = SpawnModular("door_sliding_auto", new Vector3(-1f, 1.1f, 28f), Quaternion.identity, new Vector3(1.6f, 2.2f, 0.18f), doorMat);
        door3.name = "SlidingDoor_3";
        door3.AddComponent<SlidingDoor>();

        SpawnModular("prop_monitor_stack", new Vector3(8f, 0.9f, 23f), Quaternion.identity, Vector3.one, metalMat);

        var cctvScreen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cctvScreen.name = "CCTV_Monitor_Screen";
        cctvScreen.transform.position = new Vector3(8f, 1.42f, 22.65f);
        cctvScreen.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        cctvScreen.transform.localScale = new Vector3(1.1f, 0.8f, 1f);
        Object.DestroyImmediate(cctvScreen.GetComponent<Collider>());
        cctvScreen.AddComponent<G1CCTVScreen>();

        var crTerminal1 = SpawnModular("prop_computer_terminal", new Vector3(6f, 0.9f, 23f), Quaternion.identity, Vector3.one * 0.8f, floorMat);
        var crTermComp1 = crTerminal1.AddComponent<G1Terminal>();
        crTermComp1.logMessage = "MEMO: HECU COMBAT UNITS DISPATCHED FOR 'CLEANUP'. ALL RESEARCH PERSONNEL SUB-SURFACE ACCESS REVOKED.";

        var crTerminal2 = SpawnModular("prop_computer_terminal", new Vector3(10f, 0.9f, 23f), Quaternion.identity, Vector3.one * 0.8f, floorMat);
        var crTermComp2 = crTerminal2.AddComponent<G1Terminal>();
        crTermComp2.logMessage = "WARNING: EMERGENCY ESCAPE ELEVATOR OVERRIDE CODES REQUIRED. ACCESS VIA LOWER BREACH ZONE ONLY.";
        SpawnModular("prop_lab_table", new Vector3(4f, 0.45f, 20f), Quaternion.Euler(0f, 45f, 90f), new Vector3(1.6f, 0.9f, 0.8f), metalMat);

        // Spawn SMG pickup in Control Room on table
        SpawnWeaponPickup("Smg", G1WeaponPickup.WeaponType.Smg, new Vector3(4f, 1.05f, 20f), Quaternion.identity, metalMat);

        // Spawn Control Room Lights (cool blue terminal glow)
        SpawnLight("ControlRoom_Light", new Vector3(8f, 2.3f, 22f), new Color(0.35f, 0.75f, 1f), 12f, 1.6f);

        // 4. INDUSTRIAL HALL (Ambush Faction Arena)
        Slab("IndustrialFloor", new Vector3(12f, -0.25f, 42f), new Vector3(32, 0.5f, 28), floorMat);
        Slab("IndustrialWallS_L", new Vector3(-3f, 1.5f, 28f), new Vector3(2, 3, 0.5f), concrete);
        Slab("IndustrialWallS_R", new Vector3(21f, 1.5f, 28f), new Vector3(14, 3, 0.5f), concrete);
        Slab("IndustrialWallW", new Vector3(-4f, 1.5f, 42f), new Vector3(0.5f, 3, 28), concrete);
        Slab("IndustrialWallE", new Vector3(28f, 1.5f, 42f), new Vector3(0.5f, 3, 28), concrete);
        Slab("IndustrialWallN_L", new Vector3(2f, 1.5f, 56f), new Vector3(12, 3, 0.5f), concrete);
        Slab("IndustrialWallN_R", new Vector3(22f, 1.5f, 56f), new Vector3(12, 3, 0.5f), concrete);

        SpawnModular("prop_pillar_structural", new Vector3(4f, 1.5f, 35f), Quaternion.identity, new Vector3(0.7f, 3f, 0.7f), hazard);
        SpawnModular("prop_pillar_structural", new Vector3(20f, 1.5f, 35f), Quaternion.identity, new Vector3(0.7f, 3f, 0.7f), hazard);
        SpawnModular("prop_pillar_structural", new Vector3(4f, 1.5f, 49f), Quaternion.identity, new Vector3(0.7f, 3f, 0.7f), hazard);
        SpawnModular("prop_pillar_structural", new Vector3(20f, 1.5f, 49f), Quaternion.identity, new Vector3(0.7f, 3f, 0.7f), hazard);

        SpawnModular("prop_generator_large", new Vector3(-2f, 0.75f, 31f), Quaternion.identity, new Vector3(1.2f, 1.5f, 1.8f), greenMat);
        SpawnModular("prop_generator_large", new Vector3(26f, 0.75f, 53f), Quaternion.identity, new Vector3(1.2f, 1.5f, 1.8f), greenMat);

        for (int i = 0; i < 3; i++)
            SpawnModular("prop_filing_cabinet", new Vector3(-3.3f, 0.9f, 40f + i * 1.2f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.6f, 1.8f, 0.6f), metalMat);

        // Ambush cover blocks and points
        var coverParent = new GameObject("CoverPoints").transform;
        Vector3[] coverPts = { new Vector3(8f, 0.55f, 40f), new Vector3(16f, 0.55f, 45f), new Vector3(12f, 0.55f, 50f) };
        for (int i = 0; i < coverPts.Length; i++)
        {
            var block = SpawnModular("wall_straight_panel", coverPts[i], Quaternion.identity, new Vector3(1.7f, 1.1f, 0.4f), concrete);
            block.name = $"CoverBlock_{i}";
            for (int side = -1; side <= 1; side += 2)
            {
                var cp = new GameObject($"CoverPoint_{i}_{(side < 0 ? "a" : "b")}");
                cp.transform.SetParent(coverParent, false);
                cp.transform.position = block.transform.position + Vector3.forward * (0.75f * side) + Vector3.up * 0.05f;
                cp.AddComponent<G1CoverPoint>();
            }
        }

        Vector3[] cratePts = { new Vector3(6f, 0.4f, 38f), new Vector3(14f, 0.4f, 48f), new Vector3(22f, 0.4f, 42f) };
        for (int i = 0; i < cratePts.Length; i++)
        {
            var crate = SpawnModular("prop_crate_wooden", cratePts[i], Quaternion.identity, Vector3.one * 0.8f, wood);
            crate.name = "Crate";
            crate.AddComponent<Breakable>();
            crate.GetComponent<HealthSystem>().maxHealth = 50f;
            var bar = crate.AddComponent<WorldSpaceHealthBar>();
            bar.heightOffset = 1.1f;
        }

        // Catwalk for Suppress soldier
        Slab("Catwalk", new Vector3(4f, 3f, 49f), new Vector3(4f, 0.2f, 3f), metalMat);
        Slab("CatwalkRailing", new Vector3(4f, 3.6f, 47.5f), new Vector3(4f, 0.4f, 0.1f), metalMat);

        // Spawn Magnum pickup in Industrial Hall near generator
        SpawnWeaponPickup("Magnum", G1WeaponPickup.WeaponType.Magnum, new Vector3(18f, 0.4f, 40f), Quaternion.identity, hazard);

        // Spawn Industrial Hall point lights
        SpawnLight("Industrial_Light1", new Vector3(4f, 5f, 35f), new Color(1f, 0.85f, 0.65f), 16f, 1.8f);
        SpawnLight("Industrial_Light2", new Vector3(20f, 5f, 35f), new Color(1f, 0.85f, 0.65f), 16f, 1.8f);
        SpawnLight("Industrial_Light3", new Vector3(4f, 5f, 49f), new Color(1f, 0.85f, 0.65f), 16f, 1.8f);
        SpawnLight("Industrial_Light4", new Vector3(20f, 5f, 49f), new Color(1f, 0.85f, 0.65f), 16f, 1.8f);

        // 5. ALIEN BREACH ZONE (Portal Chaos)
        Slab("BreachFloor", new Vector3(12f, -0.25f, 64f), new Vector3(8, 0.5f, 16), floorMat);
        Slab("BreachWallW", new Vector3(8f, 1.5f, 64f), new Vector3(0.5f, 3, 16), concrete);
        Slab("BreachWallE", new Vector3(16f, 1.5f, 64f), new Vector3(0.5f, 3, 16), concrete);
        Slab("BreachWallN_L", new Vector3(9f, 1.5f, 72f), new Vector3(2, 3, 0.5f), concrete);
        Slab("BreachWallN_R", new Vector3(15f, 1.5f, 72f), new Vector3(2, 3, 0.5f), concrete);

        // Toxic radiation puddle
        Material toxicMat = MakeMat("ToxicWaste", new Color(0.12f, 0.85f, 0.16f), 0.1f);
        toxicMat.EnableKeyword("_EMISSION");
        toxicMat.SetColor("_EmissionColor", new Color(0.04f, 0.45f, 0.04f));
        var toxicWaste = Slab("ToxicWastePuddle", new Vector3(12f, -0.15f, 64f), new Vector3(6f, 0.4f, 4f), toxicMat);
        toxicWaste.AddComponent<G1HazardZone>();
        toxicWaste.GetComponent<Collider>().isTrigger = true;

        // Xen Jump Pad
        Material jumpPadMat = MakeMat("XenJumpPad", new Color(0f, 0.95f, 0.85f), 0.2f);
        jumpPadMat.EnableKeyword("_EMISSION");
        jumpPadMat.SetColor("_EmissionColor", new Color(0f, 0.65f, 0.55f));
        var jumpPad = Slab("XenJumpPadPlatform", new Vector3(12f, -0.15f, 58.5f), new Vector3(2f, 0.3f, 2f), jumpPadMat);
        jumpPad.AddComponent<G1JumpPad>().launchForce = 13.5f;
        jumpPad.GetComponent<Collider>().isTrigger = true;

        // Doorframe 4 (Breach Zone to Elevator)
        Slab("BreachExitFrameL", new Vector3(9.8f, 1.25f, 72f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("BreachExitFrameR", new Vector3(14.2f, 1.25f, 72f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("BreachExitLintel", new Vector3(12f, 2.6f, 72f), new Vector3(4.8f, 0.3f, 0.4f), concrete);
        var door4 = SpawnModular("door_sliding_auto", new Vector3(12f, 1.1f, 72f), Quaternion.identity, new Vector3(1.6f, 2.2f, 0.18f), doorMat);
        door4.name = "SlidingDoor_4";
        door4.AddComponent<SlidingDoor>();

        // Decorative Rubble
        var rub1 = Slab("Rubble1", new Vector3(10f, 0.2f, 60f), new Vector3(1.5f, 0.3f, 2.0f), concrete);
        rub1.transform.rotation = Quaternion.Euler(20f, 30f, 10f);
        var rub2 = Slab("Rubble2", new Vector3(14f, 0.1f, 68f), new Vector3(1.2f, 0.2f, 1.6f), concrete);
        rub2.transform.rotation = Quaternion.Euler(-15f, 45f, -5f);

        // Horde trigger — player steps into zone to activate 8 aliens
        var hordeTrigger = new GameObject("HordeTrigger_Breach");
        hordeTrigger.transform.position = new Vector3(12f, 1f, 60f);
        var hordeColl = hordeTrigger.AddComponent<BoxCollider>();
        hordeColl.isTrigger = true;
        hordeColl.size = new Vector3(8f, 3f, 4f);

        var hordeComp = hordeTrigger.AddComponent<G1HordeTrigger>();
        hordeComp.spawnCount = 8;
        hordeComp.spawnCenter = new Vector3(12f, 0f, 68f);
        hordeComp.spawnRadius = 4f;

        // Spawn Portal Light (neon teal, pulsing effect)
        var l10 = SpawnLight("Breach_PortalLight", new Vector3(12f, 2.5f, 64f), new Color(0f, 1f, 0.8f), 15f, 2.5f);
        l10.gameObject.AddComponent<G1LightEffects>().effectType = G1LightEffects.EffectType.Pulse;

        // 6. EMERGENCY ELEVATOR (EXIT)
        Slab("ElevatorFloor", new Vector3(12f, -0.25f, 76f), new Vector3(6, 0.5f, 8), floorMat);
        Slab("ElevatorWallW", new Vector3(9f, 1.5f, 76f), new Vector3(0.5f, 3, 8), concrete);
        Slab("ElevatorWallE", new Vector3(15f, 1.5f, 76f), new Vector3(0.5f, 3, 8), concrete);
        Slab("ElevatorWallN", new Vector3(12f, 1.5f, 80f), new Vector3(6, 3, 0.5f), concrete);

        var lift = SpawnModular("prop_generator_large", new Vector3(12f, 0.05f, 77.5f), Quaternion.identity, new Vector3(2.5f, 0.1f, 2.5f), metalMat);
        lift.name = "ElevatorPlatform";
        
        // Spawn Elevator warning beacon (hazard orange, pulsing)
        var l11 = SpawnLight("Elevator_WarningLight", new Vector3(12f, 2.6f, 77.5f), new Color(1f, 0.4f, 0f), 12f, 2.5f);
        l11.gameObject.AddComponent<G1LightEffects>().effectType = G1LightEffects.EffectType.Pulse;

        // Add Level Complete Trigger Zone
        var triggerObj = new GameObject("LevelExitTrigger");
        triggerObj.transform.position = new Vector3(12f, 0.5f, 77.5f);
        var triggerCol = triggerObj.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(2.5f, 2.0f, 2.5f);
        triggerObj.AddComponent<G1LevelExitTrigger>();
    }

    /// Seeded point in the arena, kept clear of the player spawn and doorway.
    static Vector3 ScatterPoint(System.Random rng)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var p = new Vector3(RandRange(rng, -12f, 12f), 0f,
                                RandRange(rng, -12f, 13f));
            if ((p - new Vector3(0f, 0f, -8f)).sqrMagnitude < 16f)
                continue;                               // player spawn area
            if ((p - new Vector3(7f, 0f, 6f)).sqrMagnitude < 9f)
                continue;                               // doorway
            return p;
        }
        return new Vector3(0f, 0f, 12f);
    }

    static float RandRange(System.Random rng, float min, float max)
        => (float)(min + rng.NextDouble() * (max - min));

    static GameObject BuildPlayer(RuntimeAnimatorController pistolCtrl, RuntimeAnimatorController smgCtrl, RuntimeAnimatorController shotgunCtrl, RuntimeAnimatorController magnumCtrl)
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 0.05f, -8f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.4f;
        var move = player.AddComponent<PlayerMovement>();

        var health = player.AddComponent<HealthSystem>();
        health.maxHealth = 100f;
        player.AddComponent<PlayerHUD>();
        player.AddComponent<ArenaDebugHUD>();
        player.AddComponent<G1PlayerDeath>();
        var fx = player.AddComponent<G1WeaponFX>();

        var monitor = player.AddComponent<CombatArenaMonitor>();
        monitor.player = player.transform;
        monitor.playerHealth = health;

        var camGo = new GameObject("ViewCamera");
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 75f;
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();
        var look = camGo.AddComponent<MouseLook>();
        look.body = player.transform;
        var camFX = camGo.AddComponent<CameraEffects>();
        camGo.AddComponent<G1Flashlight>();

        var use = player.AddComponent<PlayerUse>();
        use.viewCamera = cam;

        int playerLayer = EnsureLayer("Player");
        LayerMask shootable = ~(1 << playerLayer);

        // --- crowbar
        var crowbarHolder = new GameObject("CrowbarHolder");
        crowbarHolder.transform.SetParent(camGo.transform, false);
        crowbarHolder.transform.localPosition = new Vector3(0.32f, -0.34f, 0.55f);
        var crowbar = crowbarHolder.AddComponent<Crowbar>();
        crowbar.viewCamera = cam;
        crowbar.movement = move;
        crowbar.hitMask = shootable;
        MountViewmodel($"{Models}/Crowbar.fbx", crowbarHolder.transform,
                       new Vector3(0f, -0.06f, 0f), Quaternion.Euler(-62f, 18f, 12f));

        // --- pistol
        var pistolHolder = new GameObject("PistolHolder");
        pistolHolder.transform.SetParent(camGo.transform, false);
        pistolHolder.transform.localPosition = new Vector3(0.24f, -0.27f, 0.42f);
        var pistol = pistolHolder.AddComponent<G1Pistol>();
        pistol.viewCamera = cam;
        pistol.movement = move;
        pistol.hitMask = shootable;
        pistol.weaponFX = fx;
        pistol.camFX = camFX;
        var pistolMuzzle = new GameObject("MuzzlePoint");
        pistolMuzzle.transform.SetParent(pistolHolder.transform, false);
        pistolMuzzle.transform.localPosition = new Vector3(0f, 0.075f, 0.144f);
        pistol.muzzlePoint = pistolMuzzle.transform;
        var pistolModel = MountViewmodel($"{Models}/Pistol.fbx", pistolHolder.transform,
                                         Vector3.zero, Quaternion.identity);
        var pistolAnim = pistolModel.GetComponent<Animator>();
        if (!pistolAnim)
            pistolAnim = pistolModel.AddComponent<Animator>();
        pistolAnim.runtimeAnimatorController = pistolCtrl;
        pistol.modelAnimator = pistolAnim;

        // --- smg
        var smgHolder = new GameObject("SmgHolder");
        smgHolder.transform.SetParent(camGo.transform, false);
        smgHolder.transform.localPosition = new Vector3(0.22f, -0.32f, 0.48f);
        var smg = smgHolder.AddComponent<G1Smg>();
        smg.viewCamera = cam;
        smg.movement = move;
        smg.hitMask = shootable;
        smg.weaponFX = fx;
        smg.camFX = camFX;
        var smgMuzzle = new GameObject("MuzzlePoint");
        smgMuzzle.transform.SetParent(smgHolder.transform, false);
        smgMuzzle.transform.localPosition = new Vector3(0f, 0.08f, 0.31f);
        smg.muzzlePoint = smgMuzzle.transform;
        var smgModel = MountViewmodel($"{Models}/Smg.fbx", smgHolder.transform,
                                      Vector3.zero, Quaternion.identity);
        var smgAnim = smgModel.GetComponent<Animator>();
        if (!smgAnim)
            smgAnim = smgModel.AddComponent<Animator>();
        smgAnim.runtimeAnimatorController = smgCtrl;
        smg.modelAnimator = smgAnim;

        // --- shotgun
        var shotgunHolder = new GameObject("ShotgunHolder");
        shotgunHolder.transform.SetParent(camGo.transform, false);
        shotgunHolder.transform.localPosition = new Vector3(0.24f, -0.28f, 0.45f);
        var shotgun = shotgunHolder.AddComponent<G1Shotgun>();
        shotgun.viewCamera = cam;
        shotgun.movement = move;
        shotgun.hitMask = shootable;
        shotgun.weaponFX = fx;
        shotgun.camFX = camFX;
        var shotgunMuzzle = new GameObject("MuzzlePoint");
        shotgunMuzzle.transform.SetParent(shotgunHolder.transform, false);
        shotgunMuzzle.transform.localPosition = new Vector3(0f, 0.08f, 0.65f); // aligned to barrel end
        shotgun.muzzlePoint = shotgunMuzzle.transform;
        var shotgunModel = MountViewmodel($"{Models}/Shotgun.fbx", shotgunHolder.transform,
                                         Vector3.zero, Quaternion.identity);
        var shotgunAnim = shotgunModel.GetComponent<Animator>();
        if (!shotgunAnim)
            shotgunAnim = shotgunModel.AddComponent<Animator>();
        shotgunAnim.runtimeAnimatorController = shotgunCtrl;
        shotgun.modelAnimator = shotgunAnim;

        // --- magnum
        var magnumHolder = new GameObject("MagnumHolder");
        magnumHolder.transform.SetParent(camGo.transform, false);
        magnumHolder.transform.localPosition = new Vector3(0.24f, -0.27f, 0.44f);
        var magnum = magnumHolder.AddComponent<G1Magnum>();
        magnum.viewCamera = cam;
        magnum.movement = move;
        magnum.hitMask = shootable;
        magnum.weaponFX = fx;
        magnum.camFX = camFX;
        var magnumMuzzle = new GameObject("MuzzlePoint");
        magnumMuzzle.transform.SetParent(magnumHolder.transform, false);
        magnumMuzzle.transform.localPosition = new Vector3(0f, 0.08f, 0.35f); // aligned to barrel end
        magnum.muzzlePoint = magnumMuzzle.transform;
        var magnumModel = MountViewmodel($"{Models}/Magnum.fbx", magnumHolder.transform,
                                         Vector3.zero, Quaternion.identity);
        var magnumAnim = magnumModel.GetComponent<Animator>();
        if (!magnumAnim)
            magnumAnim = magnumModel.AddComponent<Animator>();
        magnumAnim.runtimeAnimatorController = magnumCtrl;
        magnum.modelAnimator = magnumAnim;

        var switcher = camGo.AddComponent<WeaponSwitcher>();
        switcher.weapons = new[] { crowbarHolder, pistolHolder, smgHolder, shotgunHolder, magnumHolder };
        switcher.unlocked = new bool[] { true, false, false, false, false };

        pistolHolder.SetActive(false);
        smgHolder.SetActive(false);
        shotgunHolder.SetActive(false);
        magnumHolder.SetActive(false);

        SetLayerRecursive(player, playerLayer);
        return player;
    }

    static GameObject MountViewmodel(string fbxPath, Transform parent,
                                     Vector3 localPos, Quaternion localRot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var vm = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        vm.transform.SetParent(parent, false);
        vm.transform.localPosition = localPos;
        vm.transform.localRotation = localRot;
        foreach (var col in vm.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(col);
        return vm;
    }

    static void BuildNpcs(RuntimeAnimatorController protagonistCtrl,
                          RuntimeAnimatorController villainCtrl, Vector3 playerPos,
                          ArenaConfig cfg, System.Random rng)
    {
        var protagonist = SpawnCharacter($"{Models}/Protagonist.fbx",
                                         new Vector3(2f, 0f, -8f), protagonistCtrl);
        var patrol = new GameObject("PatrolPath").transform;
        Vector3[] pts =
        {
            new Vector3(2f, 0f, -8f), new Vector3(-2f, 0f, -8f),
            new Vector3(-2f, 0f, -5f), new Vector3(2f, 0f, -5f),
        };
        var waypoints = pts.Select((p, i) =>
        {
            var wp = new GameObject($"WP{i}").transform;
            wp.SetParent(patrol, false);
            wp.position = p;
            return wp;
        }).ToArray();
        protagonist.GetComponent<NPCController>().waypoints = waypoints;
        var pHealth = protagonist.AddComponent<HealthSystem>();
        pHealth.maxHealth = 100f;
        var pBar = protagonist.AddComponent<WorldSpaceHealthBar>();
        pBar.heightOffset = 2.15f;
        protagonist.AddComponent<G1DeathPhysics>();

        // Ensure Enemy Layer and SquadBlackboard
        int enemyLayer = EnsureLayer("Enemy");
        new GameObject("SquadBlackboard").AddComponent<SquadBlackboard>();

        // Spawn Zombie (sickly green skin + procedural Headcrab on head bone)
        var zombie = SpawnCharacter($"{Models}/Villain.fbx", new Vector3(0f, 0f, 6f), villainCtrl);
        zombie.name = "Zombie";
        SetLayerRecursive(zombie, enemyLayer);

        // Sickly green-decay skin color
        foreach (var r in zombie.GetComponentsInChildren<Renderer>())
        {
            var m = new Material(r.sharedMaterial);
            m.color = new Color(0.35f, 0.45f, 0.25f);
            r.sharedMaterial = m;
        }

        // Attach fleshy Headcrab to head bone
        Transform headBone = FindBone(zombie.transform, "head");
        if (headBone)
        {
            var hc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(hc.GetComponent<Collider>());
            hc.name = "Headcrab";
            hc.transform.SetParent(headBone, false);
            hc.transform.localPosition = new Vector3(0f, 0.08f, 0.05f); // sit on top of head
            hc.transform.localScale = new Vector3(0.24f, 0.18f, 0.22f);
            var hcMat = new Material(Shader.Find("Standard"));
            hcMat.color = new Color(0.8f, 0.5f, 0.35f); // fleshy orange
            hc.GetComponent<Renderer>().sharedMaterial = hcMat;

            // Fleshy legs
            for (int i = 0; i < 4; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(leg.GetComponent<Collider>());
                leg.transform.SetParent(hc.transform, false);
                float side = (i % 2 == 0) ? 0.4f : -0.4f;
                float fwd = (i < 2) ? 0.4f : -0.4f;
                leg.transform.localPosition = new Vector3(side, -0.4f, fwd);
                leg.transform.localScale = new Vector3(0.2f, 0.5f, 0.2f);
                leg.GetComponent<Renderer>().sharedMaterial = hcMat;
            }
        }

        var zHealth = zombie.AddComponent<HealthSystem>();
        zHealth.maxHealth = 100f;
        var zBar = zombie.AddComponent<WorldSpaceHealthBar>();
        zBar.heightOffset = 2.15f;
        zombie.AddComponent<G1DeathPhysics>();
        zombie.AddComponent<G1ZombieAI>();
        zombie.AddComponent<AgentNavMeshWarp>();

        var zAgent = zombie.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (zAgent)
        {
            zAgent.height = 1.8f;
            zAgent.radius = 0.35f;
            zAgent.speed = 1.6f;
            zAgent.angularSpeed = 400f;
            zAgent.acceleration = 14f;
            zAgent.stoppingDistance = 1.3f;   // just inside 1.8 attack range
        }

        // Spawn Alien AI (cloned template, tinted neon purple)
        var alien = SpawnCharacter($"{Models}/Villain.fbx", new Vector3(12f, 0f, 64f), villainCtrl);
        alien.name = "Alien";
        SetLayerRecursive(alien, enemyLayer);

        foreach (var r in alien.GetComponentsInChildren<Renderer>())
        {
            var m = new Material(r.sharedMaterial);
            m.color = new Color(0.6f, 0.1f, 0.9f); // Neon purple
            m.SetColor("_EmissionColor", new Color(0.2f, 0.05f, 0.3f));
            m.EnableKeyword("_EMISSION");
            r.sharedMaterial = m;
        }

        var aHealth = alien.AddComponent<HealthSystem>();
        aHealth.maxHealth = 80f; // alien slightly squishier but faster
        var aBar = alien.AddComponent<WorldSpaceHealthBar>();
        aBar.heightOffset = 2.15f;
        alien.AddComponent<G1DeathPhysics>();
        alien.AddComponent<G1AlienAI>();
        alien.AddComponent<AgentNavMeshWarp>();

        var aAgent = alien.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (aAgent)
        {
            aAgent.height = 1.8f;
            aAgent.radius = 0.35f;
            aAgent.speed = 2.2f;
            aAgent.angularSpeed = 480f;
            aAgent.acceleration = 16f;
            aAgent.stoppingDistance = 1.3f;   // just inside 1.8 attack range
        }

        // Spawn Soldier AI (HECU soldier style - blue/grey camouflage tint)
        var soldier = SpawnCharacter($"{Models}/Protagonist.fbx", new Vector3(12f, 0f, 45f), protagonistCtrl);
        soldier.name = "HECUSoldier";
        SetLayerRecursive(soldier, enemyLayer);

        // HECU blue-grey and dark vest tinting
        int renderIdx = 0;
        foreach (var r in soldier.GetComponentsInChildren<Renderer>())
        {
            var m = new Material(r.sharedMaterial);
            if (renderIdx == 0) m.color = new Color(0.2f, 0.25f, 0.3f); // blue-grey camo
            else m.color = new Color(0.12f, 0.12f, 0.15f); // dark vest/boots
            r.sharedMaterial = m;
            renderIdx++;
        }

        // Set up Soldier Patrol Path (inside Industrial Hall)
        var sPatrol = new GameObject("SoldierPatrolPath").transform;
        Vector3[] sPts =
        {
            new Vector3(6f, 0f, 32f), new Vector3(6f, 0f, 52f),
            new Vector3(18f, 0f, 52f), new Vector3(18f, 0f, 32f)
        };
        var sWaypoints = sPts.Select((p, i) =>
        {
            var wp = new GameObject($"SWP{i}").transform;
            wp.SetParent(sPatrol, false);
            wp.position = p;
            return wp;
        }).ToArray();

        var sHealth = soldier.AddComponent<HealthSystem>();
        sHealth.maxHealth = 100f;
        var sBar = soldier.AddComponent<WorldSpaceHealthBar>();
        sBar.heightOffset = 2.15f;
        soldier.AddComponent<G1DeathPhysics>();

        var soldierAI = soldier.AddComponent<G1SoldierAI>();
        soldierAI.waypoints = sWaypoints;
        soldier.AddComponent<AIDebugGizmos>();   // before prefab save: spawned
                                                 // reinforcements get gizmos too
        soldier.AddComponent<AgentNavMeshWarp>();

        // Configure NavMeshAgent properties
        var agent = soldier.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent)
        {
            agent.height = 1.8f;
            agent.radius = 0.35f;
            agent.speed = 1.0f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.6f;
        }

        // Save prefabs
        EnsureFolder("Assets/G1/Prefabs");
        GameObject soldierPrefab = PrefabUtility.SaveAsPrefabAsset(soldier, "Assets/G1/Prefabs/HECUSoldier.prefab");
        GameObject zombiePrefab = PrefabUtility.SaveAsPrefabAsset(zombie, "Assets/G1/Prefabs/Zombie.prefab");
        GameObject alienPrefab = PrefabUtility.SaveAsPrefabAsset(alien, "Assets/G1/Prefabs/Alien.prefab");

        // === INDUSTRIAL HALL — 3 HECU SOLDIERS AMBUSH ===
        if (soldierPrefab != null)
        {
            // Suppress — catwalk position (elevated)
            var suppress = (GameObject)Object.Instantiate(soldierPrefab, new Vector3(4f, 3.1f, 49f), Quaternion.Euler(0f, 180f, 0f));
            suppress.name = "HECU_Suppress";
            suppress.AddComponent<AgentNavMeshWarp>();

            // FlankLeft — enter from left
            var flankL = (GameObject)Object.Instantiate(soldierPrefab, new Vector3(-3f, 0f, 42f), Quaternion.Euler(0f, 90f, 0f));
            flankL.name = "HECU_FlankLeft";
            flankL.AddComponent<AgentNavMeshWarp>();

            // FlankRight — enter from right
            var flankR = (GameObject)Object.Instantiate(soldierPrefab, new Vector3(27f, 0f, 42f), Quaternion.Euler(0f, -90f, 0f));
            flankR.name = "HECU_FlankRight";
            flankR.AddComponent<AgentNavMeshWarp>();
            
            // Last Stand HECU in Alien Breach Zone
            var lastStand = (GameObject)Object.Instantiate(soldierPrefab, new Vector3(10f, 0f, 65f), Quaternion.Euler(0f, 90f, 0f));
            lastStand.name = "HECU_LastStand";
            lastStand.AddComponent<AgentNavMeshWarp>();
        }

        // Set up ThreatDirector
        var directorGo = new GameObject("ThreatDirector");
        var director = directorGo.AddComponent<ThreatDirector>();
        director.soldierPrefab = soldierPrefab;
        director.mobPrefabs = new GameObject[] { zombiePrefab, alienPrefab };
        director.maxActiveSoldiers = cfg.MaxActiveSoldiers;
        director.relaxDuration = cfg.RelaxDuration;
        director.intensityDecayRate = 0.08f;

        // Spawn nodes scattered inside Industrial Hall and Alien Breach Zone
        var nodesParent = new GameObject("SpawnNodes").transform;
        Vector3[] nodePositions = {
            new Vector3(4f, 0.5f, 32f),      // Industrial Hall SW
            new Vector3(20f, 0.5f, 32f),     // Industrial Hall SE
            new Vector3(4f, 0.5f, 52f),      // Industrial Hall NW
            new Vector3(20f, 0.5f, 52f),     // Industrial Hall NE
            new Vector3(12f, 0.5f, 60f),     // Breach Zone South
            new Vector3(12f, 0.5f, 68f)      // Breach Zone North
        };
        var spawnNodes = new Transform[nodePositions.Length];
        for (int i = 0; i < nodePositions.Length; i++)
        {
            var node = new GameObject($"SpawnNode_{i}");
            node.transform.SetParent(nodesParent, false);
            node.transform.position = nodePositions[i];
            node.transform.LookAt(new Vector3(12f, 0.5f, 45f));
            spawnNodes[i] = node.transform;
        }
        director.spawnNodes = spawnNodes;

        // ---- parameterized population from the preset (seeded) ----
        PopulateCount(soldier, cfg.Soldiers, rng);
        PopulateCount(zombie, cfg.Zombies, rng);
        PopulateCount(alien, cfg.Aliens, rng);
    }

    /// Clone the fully configured template until `count` instances exist
    /// (seeded placement inside the Industrial Hall), or remove the template
    /// entirely when count is zero. Runs after the prefab save, so the
    /// ThreatDirector keeps valid prefab references either way.
    static void PopulateCount(GameObject template, int count, System.Random rng)
    {
        if (count <= 0)
        {
            Object.DestroyImmediate(template);
            return;
        }
        for (int i = 1; i < count; i++)
        {
            var clone = Object.Instantiate(template);
            clone.name = $"{template.name}_{i}";
            // Spawn extra entities inside the Industrial Hall bounds
            clone.transform.position = new Vector3(
                RandRange(rng, 0f, 24f), 0f, RandRange(rng, 32f, 52f));
        }
    }

    static Transform FindBone(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var found = FindBone(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static GameObject SpawnCharacter(string fbxPath, Vector3 pos,
                                     RuntimeAnimatorController ctrl)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = pos;
        var anim = go.GetComponent<Animator>();
        if (!anim)
            anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        go.AddComponent<NPCController>();
        go.AddComponent<NPCLocomotionSync>();
        var col = go.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.35f;
        col.center = new Vector3(0f, 0.9f, 0f);
        return go;
    }
}
