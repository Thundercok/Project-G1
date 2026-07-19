using System.Linq;
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

        // Bake NavMesh in Editor
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

        EnsureFolder("Assets/Scenes");
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
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        sun.intensity = 1.05f;
        sun.color = new Color(1f, 0.95f, 0.88f);
        sun.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.30f, 0.31f, 0.34f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance = 80f;
        RenderSettings.fogColor = new Color(0.34f, 0.36f, 0.39f);
    }

    static GameObject Slab(string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
        return go;
    }

    static void BuildArena(ArenaConfig cfg, System.Random rng)
    {
        Material concrete = MakeMat("Concrete", new Color(0.33f, 0.34f, 0.32f));
        Material floorMat = MakeMat("Floor", new Color(0.26f, 0.27f, 0.26f));
        Material hazard = MakeMat("HazardOrange", new Color(0.72f, 0.29f, 0.05f));
        Material wood = MakeMat("CrateWood", new Color(0.38f, 0.25f, 0.12f));
        Material doorMat = MakeMat("DoorSteel", new Color(0.45f, 0.36f, 0.16f), 0.4f);

        // fixed shell — identical for every preset
        Slab("Ground", new Vector3(0, -0.25f, 0), new Vector3(40, 0.5f, 40), floorMat);
        Slab("WallN", new Vector3(0, 1.5f, 16), new Vector3(32.5f, 3, 0.5f), concrete);
        Slab("WallS", new Vector3(0, 1.5f, -16), new Vector3(32.5f, 3, 0.5f), concrete);
        Slab("WallE", new Vector3(16, 1.5f, 0), new Vector3(0.5f, 3, 32.5f), concrete);
        Slab("WallW", new Vector3(-16, 1.5f, 0), new Vector3(0.5f, 3, 32.5f), concrete);

        for (int i = 0; i < 4; i++)
        {
            float x = i < 2 ? -8f : 8f;
            float z = i % 2 == 0 ? -8f : 8f;
            Slab("Pillar", new Vector3(x, 1.5f, z), new Vector3(0.7f, 3f, 0.7f), hazard);
        }

        // seeded crate scatter
        for (int i = 0; i < cfg.Crates; i++)
        {
            Vector3 p = ScatterPoint(rng);
            var crate = Slab("Crate", new Vector3(p.x, 0.4f, p.z),
                             Vector3.one * 0.8f, wood);
            crate.AddComponent<Breakable>();            // auto-adds HealthSystem
            crate.GetComponent<HealthSystem>().maxHealth = 50f;
            var bar = crate.AddComponent<WorldSpaceHealthBar>();
            bar.heightOffset = 1.1f;
        }

        // seeded cover blocks; each broad side gets a G1CoverPoint marker so
        // soldiers can actually claim cover (validation picks the safe side)
        var coverParent = new GameObject("CoverPoints").transform;
        for (int i = 0; i < cfg.CoverBlocks; i++)
        {
            Vector3 p = ScatterPoint(rng);
            bool rotated = rng.Next(2) == 1;
            var block = Slab($"CoverBlock_{i}", new Vector3(p.x, 0.55f, p.z),
                             new Vector3(1.7f, 1.1f, 0.4f), concrete);
            if (rotated)
                block.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Vector3 normal = rotated ? Vector3.right : Vector3.forward;
            for (int side = -1; side <= 1; side += 2)
            {
                var cp = new GameObject($"CoverPoint_{i}_{(side < 0 ? "a" : "b")}");
                cp.transform.SetParent(coverParent, false);
                cp.transform.position = block.transform.position
                    + normal * (0.75f * side) + Vector3.up * 0.05f;
                cp.AddComponent<G1CoverPoint>();
            }
        }

        var door = Slab("SlidingDoor", new Vector3(7f, 1.1f, 6f),
                        new Vector3(1.6f, 2.2f, 0.18f), doorMat);
        door.AddComponent<SlidingDoor>();
        Slab("DoorFrameL", new Vector3(6f, 1.25f, 6f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("DoorFrameR", new Vector3(8f, 1.25f, 6f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("DoorLintel", new Vector3(7f, 2.6f, 6f), new Vector3(2.4f, 0.3f, 0.4f), concrete);
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
                                         new Vector3(3f, 0f, 4f), protagonistCtrl);
        var patrol = new GameObject("PatrolPath").transform;
        Vector3[] pts =
        {
            new Vector3(3f, 0f, 4f), new Vector3(-3f, 0f, 4f),
            new Vector3(-3f, 0f, 10f), new Vector3(3f, 0f, 10f),
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

        var villain = SpawnCharacter($"{Models}/Villain.fbx",
                                     new Vector3(-4f, 0f, 2f), villainCtrl);
        Vector3 toPlayer = playerPos - villain.transform.position;
        toPlayer.y = 0f;
        villain.transform.rotation = Quaternion.LookRotation(toPlayer);

        // the villain can now be hurt (debug bar included) — handles physical tipping death and attacks
        var health = villain.AddComponent<HealthSystem>();
        health.maxHealth = 100f;
        var bar = villain.AddComponent<WorldSpaceHealthBar>();
        bar.heightOffset = 2.15f;
        villain.AddComponent<G1DeathPhysics>();
        villain.AddComponent<G1NPCCombat>();

        // Ensure Enemy Layer and claim/assign layers recursively
        int enemyLayer = EnsureLayer("Enemy");
        SetLayerRecursive(villain, enemyLayer);

        // Set up SquadBlackboard
        new GameObject("SquadBlackboard").AddComponent<SquadBlackboard>();

        // Spawn Zombie (sickly green skin + procedural Headcrab on head bone)
        var zombie = SpawnCharacter($"{Models}/Villain.fbx", new Vector3(0f, 0f, 4f), villainCtrl);
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

        var zAgent = zombie.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (zAgent)
        {
            zAgent.height = 1.8f;
            zAgent.radius = 0.35f;
            zAgent.speed = 1.6f;
        }

        // Spawn Alien AI (cloned template, tinted neon purple)
        var alien = SpawnCharacter($"{Models}/Villain.fbx", new Vector3(2f, 0f, 6f), villainCtrl);
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

        var aAgent = alien.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (aAgent)
        {
            aAgent.height = 1.8f;
            aAgent.radius = 0.35f;
            aAgent.speed = 2.2f;
        }

        // Spawn Soldier AI (HECU soldier style - blue/grey camouflage tint)
        var soldier = SpawnCharacter($"{Models}/Protagonist.fbx", new Vector3(-8f, 0f, 6f), protagonistCtrl);
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

        // Set up Soldier Patrol Path
        var sPatrol = new GameObject("SoldierPatrolPath").transform;
        Vector3[] sPts =
        {
            new Vector3(-8f, 0f, 6f), new Vector3(-8f, 0f, 12f),
            new Vector3(-2f, 0f, 12f), new Vector3(-2f, 0f, 6f)
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

        // Configure NavMeshAgent properties
        var agent = soldier.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent)
        {
            agent.height = 1.8f;
            agent.radius = 0.35f;
            agent.speed = 1.0f;
        }

        // Save prefabs
        EnsureFolder("Assets/G1/Prefabs");
        GameObject soldierPrefab = PrefabUtility.SaveAsPrefabAsset(soldier, "Assets/G1/Prefabs/HECUSoldier.prefab");
        GameObject zombiePrefab = PrefabUtility.SaveAsPrefabAsset(zombie, "Assets/G1/Prefabs/Zombie.prefab");
        GameObject alienPrefab = PrefabUtility.SaveAsPrefabAsset(alien, "Assets/G1/Prefabs/Alien.prefab");

        // Set up ThreatDirector
        var directorGo = new GameObject("ThreatDirector");
        var director = directorGo.AddComponent<ThreatDirector>();
        director.soldierPrefab = soldierPrefab;
        director.mobPrefabs = new GameObject[] { zombiePrefab, alienPrefab };
        director.maxActiveSoldiers = cfg.MaxActiveSoldiers;
        director.relaxDuration = cfg.RelaxDuration;
        director.intensityDecayRate = 0.08f;

        // Spawn nodes around map perimeter
        var nodesParent = new GameObject("SpawnNodes").transform;
        Vector3[] nodePositions = {
            new Vector3(-14f, 0.5f, 14f),
            new Vector3(14f, 0.5f, 14f),
            new Vector3(14f, 0.5f, -14f),
            new Vector3(-14f, 0.5f, -14f),
            new Vector3(-14f, 0.5f, 0f),
            new Vector3(14f, 0.5f, 0f),
            new Vector3(0f, 0.5f, 14f),
            new Vector3(0f, 0.5f, -14f)
        };
        var spawnNodes = new Transform[nodePositions.Length];
        for (int i = 0; i < nodePositions.Length; i++)
        {
            var node = new GameObject($"SpawnNode_{i}");
            node.transform.SetParent(nodesParent, false);
            node.transform.position = nodePositions[i];
            node.transform.LookAt(new Vector3(0f, 0.5f, 0f));
            spawnNodes[i] = node.transform;
        }
        director.spawnNodes = spawnNodes;

        // ---- parameterized population from the preset (seeded) ----
        PopulateCount(soldier, cfg.Soldiers, rng);
        PopulateCount(zombie, cfg.Zombies, rng);
        PopulateCount(alien, cfg.Aliens, rng);
    }

    /// Clone the fully configured template until `count` instances exist
    /// (seeded placement in the arena's upper half), or remove the template
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
            clone.transform.position = new Vector3(
                RandRange(rng, -12f, 12f), 0f, RandRange(rng, 2f, 13f));
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
        var col = go.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.35f;
        col.center = new Vector3(0f, 0.9f, 0f);
        return go;
    }
}
