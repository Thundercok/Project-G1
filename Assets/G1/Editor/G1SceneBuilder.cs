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

    /// Standard player rig for other level builders (Level 2/3): configures
    /// the weapon FBX, rebuilds their controllers, and assembles the full
    /// player hierarchy exactly as Level 1 does.
    public static GameObject BuildStandardPlayer()
    {
        ConfigureFbx($"{Models}/Pistol.fbx", loopAll: false);
        ConfigureFbx($"{Models}/Smg.fbx", loopAll: false);
        ConfigureFbx($"{Models}/Shotgun.fbx", loopAll: false);
        ConfigureFbx($"{Models}/Magnum.fbx", loopAll: false);
        return BuildPlayer(
            MakePistolController($"{Models}/Pistol.fbx", $"{AnimDir}/Pistol.controller"),
            MakePistolController($"{Models}/Smg.fbx", $"{AnimDir}/Smg.controller"),
            MakePistolController($"{Models}/Shotgun.fbx", $"{AnimDir}/Shotgun.controller"),
            MakeMagnumController($"{Models}/Magnum.fbx", $"{AnimDir}/Magnum.controller"));
    }

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

        // Story Cutscene Manager & Chapter 1 Intro Trigger
        var cutsceneGo = new GameObject("CutsceneManager");
        cutsceneGo.AddComponent<G1CutsceneManager>();

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
        G1MenuBuilder.RegisterScenes();   // menu (if built) = index 0, game = 1
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

    static Material MakeMat(string name, Color color, float smooth = 0.15f, string texName = null, float tileX = 1f, float tileY = 1f)
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

        if (!string.IsNullOrEmpty(texName))
        {
            string texPath = $"Assets/G1/Textures/{texName}.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.mainTextureScale = new Vector2(tileX, tileY);
            }
        }
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
        Material concrete = MakeMat("Concrete", new Color(0.85f, 0.87f, 0.90f), 0.2f, "tex_concrete_wall", 4f, 2f);
        Material floorMat = MakeMat("Floor", new Color(0.75f, 0.77f, 0.80f), 0.35f, "tex_floor_metal_grid", 6f, 6f);
        Material hazard = MakeMat("HazardOrange", new Color(1f, 1f, 1f), 0.2f, "tex_hazard_stripe", 2f, 2f);
        Material wood = MakeMat("CrateWood", new Color(0.55f, 0.42f, 0.25f), 0.15f, "tex_steel_panel", 1f, 1f);
        Material doorMat = MakeMat("DoorSteel", new Color(0.85f, 0.88f, 0.92f), 0.4f, "tex_steel_panel", 2f, 2f);
        Material metalMat = MakeMat("PropMetal", new Color(0.8f, 0.85f, 0.88f), 0.3f, "tex_steel_panel", 2f, 2f);
        Material greenMat = MakeMat("IndustrialGreen", new Color(0.5f, 0.7f, 0.5f), 0.2f, "tex_steel_panel", 2f, 2f);

        // 1. LOCKER ROOM (START)
        Slab("LockerRoomFloor", new Vector3(0, -0.25f, -8f), new Vector3(12, 0.5f, 10), floorMat);
        Slab("LockerRoomWallS", new Vector3(0, 1.5f, -13f), new Vector3(12, 3, 0.5f), concrete);
        Slab("LockerRoomWallW", new Vector3(-6f, 1.5f, -8f), new Vector3(0.5f, 3, 10), concrete);
        Slab("LockerRoomWallE", new Vector3(6f, 1.5f, -8f), new Vector3(0.5f, 3, 10), concrete);
        Slab("LockerRoomWallNW", new Vector3(-4.5f, 1.5f, -3f), new Vector3(3, 3, 0.5f), concrete);
        Slab("LockerRoomWallNE", new Vector3(4.5f, 1.5f, -3f), new Vector3(3, 3, 0.5f), concrete);

        // HEV station: a wall charger + a battery cell at the start
        G1WallCharger.Create(new Vector3(-5.4f, 1.1f, -10f));
        G1ArmorPack.Create(new Vector3(-4f, 0.5f, -10f));

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

        // Auditor (G-Man) Cameo behind Control Room window observation deck
        var auditorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/G1/Models/Villain.fbx");
        if (auditorPrefab != null)
        {
            var auditor = (GameObject)Object.Instantiate(auditorPrefab, new Vector3(14f, 1.0f, 25f), Quaternion.Euler(0f, -120f, 0f));
            auditor.name = "TheAuditor_ControlRoom";
            var actor = auditor.AddComponent<G1AuditorCutsceneActor>();
            actor.dialogLine = "What the... who is that guy in the dark suit observing me behind the glass?";
            actor.triggerRadius = 10f;
            actor.vanishRadius = 4.5f;
        }

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

        // =====================================================================
        // 5. ALIEN BREACH ZONE — redesigned with 3-beat Valve structure
        // =====================================================================
        // BEAT 1 (Z=56–63): Discovery. No enemies. Visual atmosphere + resources.
        // BEAT 2 (Z=63–74): 3-wave encounter behind cover; waves are telegraphed.
        // BEAT 3 (Z=74–83): Earned exit — override terminal gates Door 4.
        // =====================================================================

        // --- Geometry: wider (12 m, was 8 m) so player has room to strafe ---
        Slab("BreachFloor", new Vector3(12f, -0.25f, 65f), new Vector3(12, 0.5f, 18), floorMat);
        Slab("BreachWallW", new Vector3(6f,  1.5f, 65f), new Vector3(0.5f, 3, 18), concrete);
        Slab("BreachWallE", new Vector3(18f, 1.5f, 65f), new Vector3(0.5f, 3, 18), concrete);
        // Partial north walls frame Door 4 at Z=74
        Slab("BreachWallN_L", new Vector3( 8f, 1.5f, 74f), new Vector3(4, 3, 0.5f), concrete);
        Slab("BreachWallN_R", new Vector3(16f, 1.5f, 74f), new Vector3(4, 3, 0.5f), concrete);

        // Ceiling — low oppressive 3 m ceiling reinforces the "closing in" feel
        Slab("BreachCeiling", new Vector3(12f, 3.25f, 65f), new Vector3(12, 0.5f, 18), concrete);

        // --- BEAT 1: Atmospheric dressing (no enemies, no damage) ---

        // Torn wall sections at entry (visual storytelling — something broke through)
        var rub1 = Slab("BreachRubble_W1", new Vector3(7.5f, 0.25f, 58f), new Vector3(2f, 0.5f, 1.2f), concrete);
        rub1.transform.rotation = Quaternion.Euler(18f, 35f, 8f);
        var rub2 = Slab("BreachRubble_E1", new Vector3(16.5f, 0.2f, 60f), new Vector3(1.8f, 0.4f, 1.5f), concrete);
        rub2.transform.rotation = Quaternion.Euler(-12f, -20f, 5f);
        var rub3 = Slab("BreachRubble_C",  new Vector3(12f,  0.15f, 57.5f), new Vector3(3.5f, 0.3f, 1.0f), concrete);
        rub3.transform.rotation = Quaternion.Euler(5f, 15f, -6f);

        // Alien pod glow objects — Beat 1 atmosphere, tell player what is in this zone
        Material podMat = MakeMat("AlienPod", new Color(1f, 1f, 1f), 0.1f, "tex_alien_bio", 2f, 2f);
        podMat.EnableKeyword("_EMISSION");
        podMat.SetColor("_EmissionColor", new Color(0f, 0.45f, 0.42f));
        for (int i = 0; i < 3; i++)
        {
            float px = (i == 0 ? 7.5f : (i == 1 ? 16.5f : 12f));
            float pz = (i == 0 ? 60f  : (i == 1 ? 59f   : 62f));
            var pod = Slab($"AlienPod_{i}", new Vector3(px, 0.3f, pz), new Vector3(0.5f, 0.6f, 0.5f), podMat);
            pod.GetComponent<Renderer>().sharedMaterial = podMat;
            // Teal accent point light per pod
            SpawnLight($"PodLight_{i}", new Vector3(px, 1.1f, pz),
                new Color(0f, 1f, 0.8f), 3.5f, 1.2f, LightShadows.None);
        }

        // Warning zone — NO damage, only triggers geiger counter click + HUD rad icon.
        // Teaches the "toxic floor" mechanic safely before the real puddle.
        Material warnMat = MakeMat("HazardWarnStripe", new Color(1f, 1f, 1f), 0.05f, "tex_hazard_stripe", 6f, 1f);
        warnMat.EnableKeyword("_EMISSION");
        warnMat.SetColor("_EmissionColor", new Color(0.25f, 0.18f, 0f));
        var warnZone = Slab("HazardWarningFloor", new Vector3(12f, -0.19f, 62.5f), new Vector3(12f, 0.3f, 2f), warnMat);
        var warnHazard = warnZone.AddComponent<G1HazardZone>();
        warnHazard.warningOnly = true;
        warnHazard.damagePerSecond = 0f;
        warnZone.GetComponent<Collider>().isTrigger = true;

        // Actual toxic puddle — tight against EAST wall, only 1.8 m wide.
        // Player can always walk along the west half of the 12 m corridor safely.
        Material toxicMat = MakeMat("ToxicWaste", new Color(0.2f, 1f, 0.3f), 0.1f, "tex_alien_bio", 3f, 3f);
        toxicMat.EnableKeyword("_EMISSION");
        toxicMat.SetColor("_EmissionColor", new Color(0.04f, 0.55f, 0.04f));
        var toxicWaste = Slab("ToxicWastePuddle", new Vector3(17f, -0.15f, 67f), new Vector3(1.8f, 0.4f, 4f), toxicMat);
        var toxicHazard = toxicWaste.AddComponent<G1HazardZone>();
        toxicHazard.damagePerSecond = 7f;   // real damage — but it is avoidable
        toxicWaste.GetComponent<Collider>().isTrigger = true;
        // Orange glow border so puddle edge is always readable
        SpawnLight("ToxicPuddleLight", new Vector3(17f, 0.8f, 67f),
            new Color(0.3f, 1f, 0.2f), 4f, 1.1f, LightShadows.None);

        // Jump pad — WEST wall, telegraphed with teal floor arrow stripe.
        // Optional escape route / exploration, not required.
        Material jumpPadMat = MakeMat("XenJumpPad", new Color(0f, 0.95f, 0.85f), 0.2f);
        jumpPadMat.EnableKeyword("_EMISSION");
        jumpPadMat.SetColor("_EmissionColor", new Color(0f, 0.65f, 0.55f));
        var jumpPad = Slab("XenJumpPadPlatform", new Vector3(7.2f, -0.15f, 62f), new Vector3(1.8f, 0.3f, 1.8f), jumpPadMat);
        jumpPad.AddComponent<G1JumpPad>().launchForce = 12.0f;
        jumpPad.GetComponent<Collider>().isTrigger = true;
        // Arrow stripe pointing toward pad so player understands it is intentional
        var arrowStripe = Slab("JumpPadArrow", new Vector3(7.2f, -0.22f, 60.5f), new Vector3(0.5f, 0.05f, 2.5f), jumpPadMat);
        arrowStripe.GetComponent<Collider>().enabled = false;

        // --- BEAT 1: Pre-encounter resources — deliberately placed BEFORE trigger ---
        // Player sees them, grabs them, then steps into the encounter.
        SpawnModular("prop_health_pack", new Vector3(12f, 0.3f, 63f), Quaternion.identity, Vector3.one * 0.6f, concrete)
            .AddComponent<G1HealthPack>();
        SpawnModular("prop_ammo_box",    new Vector3(10f, 0.3f, 63f), Quaternion.identity, Vector3.one * 0.6f, metalMat)
            .AddComponent<G1AmmoPack>();
        G1ArmorPack.Create(new Vector3(13.5f, 0.5f, 63f), 50f);

        // Shotgun pickup for players who missed it in the corridor (second chance)
        // Only if player hasn't grabbed it — placed in a corner, not on the main path
        SpawnWeaponPickup("Shotgun", G1WeaponPickup.WeaponType.Shotgun,
            new Vector3(7.5f, 0.4f, 60.5f), Quaternion.identity, wood);

        // --- BEAT 2: Three cover blocks — all have G1CoverPoints for soldier AI ---
        // Block A: first cover visible right from entry — safe side (west of puddle)
        var coverA = Slab("BreachCover_A", new Vector3(9.5f, 0.55f, 65f), new Vector3(1.8f, 1.1f, 0.4f), concrete);
        var cpA1 = new GameObject("CP_A_South"); cpA1.transform.position = new Vector3(9.5f, 0.05f, 64.3f); cpA1.AddComponent<G1CoverPoint>();
        var cpA2 = new GameObject("CP_A_North"); cpA2.transform.position = new Vector3(9.5f, 0.05f, 65.7f); cpA2.AddComponent<G1CoverPoint>();
        // Block B: further in, on west side — flanking protection
        var coverB = Slab("BreachCover_B", new Vector3(9f, 0.55f, 69f), new Vector3(1.8f, 1.1f, 0.4f), concrete);
        var cpB1 = new GameObject("CP_B_South"); cpB1.transform.position = new Vector3(9f, 0.05f, 68.3f); cpB1.AddComponent<G1CoverPoint>();
        var cpB2 = new GameObject("CP_B_North"); cpB2.transform.position = new Vector3(9f, 0.05f, 69.7f); cpB2.AddComponent<G1CoverPoint>();
        // Block C: near exit — last stand position
        var coverC = Slab("BreachCover_C", new Vector3(9.5f, 0.55f, 72f), new Vector3(1.8f, 1.1f, 0.4f), concrete);
        var cpC1 = new GameObject("CP_C_South"); cpC1.transform.position = new Vector3(9.5f, 0.05f, 71.3f); cpC1.AddComponent<G1CoverPoint>();
        var cpC2 = new GameObject("CP_C_North"); cpC2.transform.position = new Vector3(9.5f, 0.05f, 72.7f); cpC2.AddComponent<G1CoverPoint>();

        // Signal light — dark until wave spawner activates it (player sees it flip on = warning)
        var signalLightGo = new GameObject("WaveSignalLight");
        signalLightGo.transform.position = new Vector3(12f, 2.8f, 65f);
        var signalLt = signalLightGo.AddComponent<Light>();
        signalLt.type      = LightType.Point;
        signalLt.color     = new Color(0f, 1f, 0.8f);
        signalLt.range     = 14f;
        signalLt.intensity = 2.8f;
        signalLt.shadows   = LightShadows.None;
        signalLt.enabled   = false; // off until waves start

        // --- Door 4 (locked until override terminal activated) ---
        Slab("BreachExitFrameL", new Vector3( 9.8f, 1.25f, 74f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("BreachExitFrameR", new Vector3(14.2f, 1.25f, 74f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("BreachExitLintel", new Vector3(12f,   2.6f,  74f), new Vector3(4.8f, 0.3f, 0.4f), concrete);
        var door4 = SpawnModular("door_sliding_auto", new Vector3(12f, 1.1f, 74f),
            Quaternion.identity, new Vector3(1.6f, 2.2f, 0.18f), doorMat);
        door4.name = "SlidingDoor_4";
        var slideDoor4 = door4.AddComponent<SlidingDoor>();

        // Grenade pickup (slot 6) near the alien pods
        var grenadePickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        grenadePickup.name = "GrenadePickup";
        grenadePickup.transform.position = new Vector3(10f, 0.6f, 62f);
        grenadePickup.transform.localScale = Vector3.one * 0.3f;
        grenadePickup.GetComponent<Renderer>().sharedMaterial =
            MakeMat("GrenadeOlive", new Color(0.5f, 0.5f, 0f));
        var grenadeCol = grenadePickup.GetComponent<SphereCollider>();
        grenadeCol.isTrigger = true;
        grenadeCol.radius = 2.2f;
        grenadePickup.AddComponent<G1WeaponPickup>().weaponType =
            G1WeaponPickup.WeaponType.Grenade;
        grenadePickup.AddComponent<G1WeaponSpinner>();

        // Checkpoints: after Locker Room, Control Room, Industrial Hall
        foreach (var (cpName, cpPos) in new (string, Vector3)[]
        {
            ("Checkpoint_Locker", new Vector3(0f, 0f, -1.5f)),
            ("Checkpoint_Control", new Vector3(6f, 0f, 28f)),
            ("Checkpoint_Industrial", new Vector3(12f, 0f, 54f)),
        })
        {
            var cp = new GameObject(cpName);
            cp.transform.position = cpPos;
            cp.AddComponent<G1Checkpoint>();
        }

        // Override terminal — west wall, accessible from cover B/C position
        var overrideTermGo = SpawnModular("prop_computer_terminal",
            new Vector3(7.5f, 0.9f, 71.5f), Quaternion.Euler(0f, 90f, 0f),
            Vector3.one * 0.9f, floorMat);
        overrideTermGo.name = "OverrideTerminal";
        var overrideTerm = overrideTermGo.AddComponent<G1OverrideTerminal>();
        overrideTerm.targetDoor = slideDoor4;
        overrideTerm.lockedMessage = "EMERGENCY CONSOLE: OVERRIDE LOCKED. ELIMINATE ALL SPECIMENS IN BREACH ZONE FIRST.";
        var wpTerm = overrideTermGo.AddComponent<G1Waypoint>();
        wpTerm.objectiveId = "override_terminal";
        wpTerm.label = "OVERRIDE CONSOLE";

        // Wave spawner — wires signal light, override terminal, and Door 4 together
        var waveTriggerGo = new GameObject("WaveTrigger_Breach");
        waveTriggerGo.transform.position = new Vector3(12f, 1.5f, 63.5f);
        var waveColl = waveTriggerGo.AddComponent<BoxCollider>();
        waveColl.isTrigger = true;
        waveColl.size = new Vector3(12f, 3f, 1.5f);
        var waveSpawner = waveTriggerGo.AddComponent<G1WaveSpawner>();
        waveSpawner.spawnFarCenter  = new Vector3(12f, 0f, 72f);
        waveSpawner.spawnFlankLeft  = new Vector3( 7f, 0f, 68f);
        waveSpawner.spawnFlankRight = new Vector3(17f, 0f, 68f);
        waveSpawner.spawnElite      = new Vector3(12f, 0f, 73f);
        waveSpawner.signalLight     = signalLt;
        waveSpawner.overrideTerminal = overrideTerm;
        var wpBreach = waveTriggerGo.AddComponent<G1Waypoint>();
        wpBreach.objectiveId = "breach_wave";
        wpBreach.label = "BREACH ZONE";

        // Ambient portal light (atmosphere, always on)
        var l10 = SpawnLight("Breach_PortalLight", new Vector3(12f, 2.5f, 68f),
            new Color(0f, 1f, 0.8f), 12f, 1.8f);
        l10.gameObject.AddComponent<G1LightEffects>().effectType = G1LightEffects.EffectType.Pulse;

        // Horde trigger node (now feeds ThreatDirector ambient horde, not the wave system)
        // Moved to be in the back of the zone, only fires if ThreatDirector wants to
        // escalate intensity naturally — not a level-scripted spike.
        var ambientHordeTrigger = new GameObject("AmbientHordeTrigger_Breach");
        ambientHordeTrigger.transform.position = new Vector3(12f, 1f, 71f);
        var ambientHordeColl = ambientHordeTrigger.AddComponent<BoxCollider>();
        ambientHordeColl.isTrigger = true;
        ambientHordeColl.size = new Vector3(4f, 3f, 2f);
        // No G1HordeTrigger — we intentionally leave this as a nav target only

        // =====================================================================
        // 6. EMERGENCY ELEVATOR — narrative-dressed earned exit
        // =====================================================================
        Slab("ElevatorFloor", new Vector3(12f, -0.25f, 79f), new Vector3(7, 0.5f, 10), floorMat);
        Slab("ElevatorWallW", new Vector3(8.5f, 1.5f, 79f), new Vector3(0.5f, 3, 10), concrete);
        Slab("ElevatorWallE", new Vector3(15.5f,1.5f, 79f), new Vector3(0.5f, 3, 10), concrete);
        Slab("ElevatorWallN", new Vector3(12f, 1.5f, 84f), new Vector3(7, 3, 0.5f), concrete);

        // Narrative: dead HECU soldier — tells player HECU was here and lost
        var deadHECU = SpawnModular("prop_body_soldier",
            new Vector3(9.5f, 0f, 76.5f), Quaternion.Euler(0f, 40f, 0f),
            new Vector3(0.8f, 0.8f, 0.8f), concrete);
        deadHECU.name = "DeadHECU_Elevator";

        // Narrative: scattered equipment — the chaos of a failed evac
        var droppedKit = Slab("DroppedKit", new Vector3(14.5f, 0.1f, 76f), new Vector3(0.6f, 0.2f, 0.4f), metalMat);
        droppedKit.transform.rotation = Quaternion.Euler(0f, 65f, 12f);

        // Reward health pack inside elevator — earned, not free
        SpawnModular("prop_health_pack", new Vector3(13f, 0.3f, 78f), Quaternion.identity, Vector3.one * 0.6f, concrete)
            .AddComponent<G1HealthPack>();

        // Elevator lift platform
        var lift = Slab("ElevatorPlatform", new Vector3(12f, 0.05f, 81f), new Vector3(3f, 0.1f, 3f), metalMat);

        // Stable amber light — opposite mood to the breach zone teal
        SpawnLight("Elevator_MainLight", new Vector3(12f, 2.8f, 79f),
            new Color(1f, 0.8f, 0.55f), 12f, 1.6f, LightShadows.Soft);
        // Warning beacon above lift, pulsing orange
        var l11 = SpawnLight("Elevator_WarningBeacon", new Vector3(12f, 2.9f, 81f),
            new Color(1f, 0.4f, 0f), 6f, 2.2f, LightShadows.None);
        l11.gameObject.AddComponent<G1LightEffects>().effectType = G1LightEffects.EffectType.Pulse;

        // Level exit trigger — requireUnlock = true; door must be opened via terminal
        var triggerObj = new GameObject("LevelExitTrigger");
        triggerObj.transform.position = new Vector3(12f, 0.5f, 81f);
        var triggerCol = triggerObj.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(3f, 2f, 3f);
        var exitTrigger = triggerObj.AddComponent<G1LevelExitTrigger>();
        exitTrigger.nextScene = "Level2";
        exitTrigger.requireUnlock = true;
        // Reset static flag so each play session starts locked
        G1LevelExitTrigger.ElevatorUnlocked = false;

        // Level 1 Objective Manager
        var objMgrGo = new GameObject("ObjectiveManager");
        var objMgr = objMgrGo.AddComponent<G1ObjectiveManager>();
        objMgr.AddObjective("breach_wave", "Eliminate Alien Breach Threat");
        objMgr.AddObjective("override_terminal", "Override Elevator Console & Evacuate");
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
        player.AddComponent<G1PlayerRegen>();
        player.AddComponent<G1CheckpointRestorer>();
        player.AddComponent<G1SaveApplier>();   // menu "Continue" restore
        var card = player.AddComponent<G1StoryCard>();
        card.title = "CHAPTER ONE";
        card.subtitle = "COLD START — Corvus Deep Research Annex, Sub-Level C";
        player.AddComponent<G1Footsteps>();
        player.AddComponent<G1SettingsApplier>();
        player.AddComponent<G1Music>();
        var ambience = player.AddComponent<G1Ambience>();
        ambience.zones = new[]
        {
            new G1Ambience.Zone { clip = "ambient_lab", zMax = 14f },
            new G1Ambience.Zone { clip = "ambient_industrial", zMax = 52f },
            new G1Ambience.Zone { clip = "ambient_alien", zMax = 9999f },
        };
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
        // --- grenade (slot 6): no FBX viewmodel — a small olive sphere in hand
        var grenadeHolder = new GameObject("GrenadeHolder");
        grenadeHolder.transform.SetParent(camGo.transform, false);
        grenadeHolder.transform.localPosition = new Vector3(0.26f, -0.3f, 0.45f);
        var grenadeWeapon = grenadeHolder.AddComponent<G1Grenade>();
        grenadeWeapon.viewCamera = cam;
        grenadeWeapon.movement = move;
        grenadeWeapon.hitMask = shootable;
        var grenadeVm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(grenadeVm.GetComponent<Collider>());
        grenadeVm.transform.SetParent(grenadeHolder.transform, false);
        grenadeVm.transform.localScale = Vector3.one * 0.14f;
        grenadeVm.GetComponent<Renderer>().sharedMaterial =
            MakeMat("GrenadeOlive", new Color(0.5f, 0.5f, 0f));

        switcher.weapons = new[] { crowbarHolder, pistolHolder, smgHolder, shotgunHolder, magnumHolder, grenadeHolder };
        switcher.unlocked = new bool[] { true, false, false, false, false, false };

        pistolHolder.SetActive(false);
        smgHolder.SetActive(false);
        shotgunHolder.SetActive(false);
        magnumHolder.SetActive(false);

        var introTrigger = player.AddComponent<G1IntroCutsceneTrigger>();
        introTrigger.chapterTitle = "CHAPTER ONE: COLD START";
        introTrigger.locationSubtitle = "Corvus Deep Research Annex — Sub-Level C";
        introTrigger.subjectName = "Chad Thundercock";

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

        // Alien AI template — spawned OFF-MAP (y = -50) so it never appears
        // live in the scene. It exists solely to be saved as a prefab and
        // referenced by ThreatDirector.mobPrefabs and G1WaveSpawner.
        var alien = SpawnCharacter($"{Models}/Villain.fbx", new Vector3(-999f, -50f, 0f), villainCtrl);
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
        soldier.AddComponent<G1SoldierBarks>();   // radio chatter on tactics/death
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
            
            // Note: HECU_LastStand removed from breach zone.
            // The zone is now controlled by G1WaveSpawner (alien waves only).
            // The Industrial Hall ambush (Suppress + FlankLeft + FlankRight) is
            // the HECU climax — they should not bleed into the alien zone.
        }

        // Set up ThreatDirector
        var directorGo = new GameObject("ThreatDirector");
        var director = directorGo.AddComponent<ThreatDirector>();
        director.soldierPrefab = soldierPrefab;
        director.mobPrefabs = new GameObject[] { zombiePrefab, alienPrefab };
        director.maxActiveSoldiers = cfg.MaxActiveSoldiers;
        director.relaxDuration = cfg.RelaxDuration;
        director.intensityDecayRate = 0.08f;

        // Spawn nodes: Industrial Hall only.
        var nodesParent = new GameObject("SpawnNodes").transform;
        Vector3[] nodePositions = {
            new Vector3(4f,  0.5f, 32f),   // Industrial Hall SW
            new Vector3(20f, 0.5f, 32f),   // Industrial Hall SE
            new Vector3(4f,  0.5f, 52f),   // Industrial Hall NW
            new Vector3(20f, 0.5f, 52f),   // Industrial Hall NE
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

        // Wire G1WaveSpawner prefab reference — find the WaveTrigger in the scene
        // (built by BuildArena above) and give it the alien prefab
        var waveSpawnerComp = Object.FindObjectOfType<G1WaveSpawner>();
        if (waveSpawnerComp != null)
        {
            waveSpawnerComp.alienPrefab = alienPrefab;
            // Elite uses the same base prefab; G1EliteAlien component boosts stats at runtime
            waveSpawnerComp.eliteAlienPrefab = alienPrefab;
        }

        // ---- parameterized population from the preset (seeded) ----
        PopulateCount(soldier, cfg.Soldiers, rng);
        PopulateCount(zombie,  cfg.Zombies,  rng);
        // Alien template is kept as prefab only — NOT populated live;
        // it is used exclusively by G1WaveSpawner in the Breach Zone.
        // Destroy the off-map template instance after saving.
        Object.DestroyImmediate(alien);
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
