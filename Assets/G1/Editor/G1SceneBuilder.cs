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

    [MenuItem("G1/Build Test Scene")]
    public static void BuildScene()
    {
        AssetDatabase.Refresh();
        EnsureFolder(AnimDir);
        EnsureFolder(MatDir);

        ConfigureFbx($"{Models}/Protagonist.fbx", loopAll: true);
        ConfigureFbx($"{Models}/Villain.fbx", loopAll: true);
        ConfigureFbx($"{Models}/Pistol.fbx", loopAll: false);   // only Idle loops
        ConfigureFbx($"{Models}/Smg.fbx", loopAll: false);
        ConfigureFbx($"{Models}/Shotgun.fbx", loopAll: false);

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

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildLighting();
        BuildArena();
        GameObject player = BuildPlayer(pistolCtrl, smgCtrl, shotgunCtrl);
        BuildNpcs(protagonistCtrl, villainCtrl, player.transform.position);

        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("G1 BUILD OK");
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
        return go;
    }

    static void BuildArena()
    {
        Material concrete = MakeMat("Concrete", new Color(0.33f, 0.34f, 0.32f));
        Material floorMat = MakeMat("Floor", new Color(0.26f, 0.27f, 0.26f));
        Material hazard = MakeMat("HazardOrange", new Color(0.72f, 0.29f, 0.05f));
        Material wood = MakeMat("CrateWood", new Color(0.38f, 0.25f, 0.12f));
        Material doorMat = MakeMat("DoorSteel", new Color(0.45f, 0.36f, 0.16f), 0.4f);

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

        Vector3[] cratePos =
        {
            new Vector3(3.5f, 0.4f, 1.5f), new Vector3(4.4f, 0.4f, 2.1f),
            new Vector3(3.9f, 1.2f, 1.8f), new Vector3(-5f, 0.4f, 6f),
            new Vector3(1f, 0.4f, 9f), new Vector3(-2.5f, 0.4f, -4f),
        };
        foreach (var p in cratePos)
        {
            var crate = Slab("Crate", p, Vector3.one * 0.8f, wood);
            crate.AddComponent<Breakable>();            // auto-adds HealthSystem
            crate.GetComponent<HealthSystem>().maxHealth = 50f;
            var bar = crate.AddComponent<WorldSpaceHealthBar>();
            bar.heightOffset = 1.1f;
        }

        var door = Slab("SlidingDoor", new Vector3(7f, 1.1f, 6f),
                        new Vector3(1.6f, 2.2f, 0.18f), doorMat);
        door.AddComponent<SlidingDoor>();
        Slab("DoorFrameL", new Vector3(6f, 1.25f, 6f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("DoorFrameR", new Vector3(8f, 1.25f, 6f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("DoorLintel", new Vector3(7f, 2.6f, 6f), new Vector3(2.4f, 0.3f, 0.4f), concrete);
    }

    static GameObject BuildPlayer(RuntimeAnimatorController pistolCtrl, RuntimeAnimatorController smgCtrl, RuntimeAnimatorController shotgunCtrl)
    {
        var player = new GameObject("Player");
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
        var fx = player.AddComponent<G1WeaponFX>();

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

        var switcher = camGo.AddComponent<WeaponSwitcher>();
        switcher.weapons = new[] { crowbarHolder, pistolHolder, smgHolder, shotgunHolder };

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
                          RuntimeAnimatorController villainCtrl, Vector3 playerPos)
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
