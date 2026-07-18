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

        ConfigureCharacterFbx($"{Models}/Protagonist.fbx");
        ConfigureCharacterFbx($"{Models}/Villain.fbx");

        RuntimeAnimatorController protagonistCtrl =
            MakeController($"{Models}/Protagonist.fbx", $"{AnimDir}/Protagonist.controller");
        RuntimeAnimatorController villainCtrl =
            MakeController($"{Models}/Villain.fbx", $"{AnimDir}/Villain.controller");

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildLighting();
        BuildArena();
        GameObject player = BuildPlayer();
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

    static void ConfigureCharacterFbx(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            int bar = clip.takeName.LastIndexOf('|');
            clip.name = bar >= 0 ? clip.takeName.Substring(bar + 1) : clip.takeName;
            clip.loopTime = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static RuntimeAnimatorController MakeController(string fbxPath, string ctrlPath)
    {
        AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .First(c => !c.name.Contains("__preview__") && c.name.EndsWith(name));

        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        idle.motion = Clip("Idle");
        sm.defaultState = idle;
        var walk = sm.AddState("Walk");
        walk.motion = Clip("Walk");
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
            crate.AddComponent<Breakable>();
        }

        var door = Slab("SlidingDoor", new Vector3(7f, 1.1f, 6f),
                        new Vector3(1.6f, 2.2f, 0.18f), doorMat);
        door.AddComponent<SlidingDoor>();
        Slab("DoorFrameL", new Vector3(6f, 1.25f, 6f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("DoorFrameR", new Vector3(8f, 1.25f, 6f), new Vector3(0.4f, 2.5f, 0.4f), concrete);
        Slab("DoorLintel", new Vector3(7f, 2.6f, 6f), new Vector3(2.4f, 0.3f, 0.4f), concrete);
    }

    static GameObject BuildPlayer()
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0.05f, -8f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.4f;
        var move = player.AddComponent<PlayerMovement>();

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

        var use = player.AddComponent<PlayerUse>();
        use.viewCamera = cam;

        var holder = new GameObject("WeaponHolder");
        holder.transform.SetParent(camGo.transform, false);
        holder.transform.localPosition = new Vector3(0.32f, -0.34f, 0.55f);
        var crowbar = holder.AddComponent<Crowbar>();
        crowbar.viewCamera = cam;
        crowbar.movement = move;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Crowbar.fbx");
        var vm = (GameObject)PrefabUtility.InstantiatePrefab(model);
        vm.transform.SetParent(holder.transform, false);
        vm.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        vm.transform.localRotation = Quaternion.Euler(-62f, 18f, 12f);
        foreach (var col in vm.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(col);

        return player;
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

        var villain = SpawnCharacter($"{Models}/Villain.fbx",
                                     new Vector3(-4f, 0f, 2f), villainCtrl);
        Vector3 toPlayer = playerPos - villain.transform.position;
        toPlayer.y = 0f;
        villain.transform.rotation = Quaternion.LookRotation(toPlayer);
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
