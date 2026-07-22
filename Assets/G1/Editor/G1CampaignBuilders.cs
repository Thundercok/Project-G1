using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Campaign levels 2 and 3 (see docs/story.md). Level 1 lives in
/// G1SceneBuilder; these reuse its public player rig.
public static class G1CampaignBuilders
{
    // ---------------------------------------------------------- shared bits
    static Material Mat(Color c, float emission = 0f, string texName = null, float tileX = 1f, float tileY = 1f)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * emission);
        }
        if (!string.IsNullOrEmpty(texName))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/G1/Textures/{texName}.png");
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = new Vector2(tileX, tileY);
            }
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

    static GameObject SpawnPrefabAndReturn(string path, Vector3 pos, float yaw = 0f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab)
        {
            Debug.LogWarning("Missing prefab " + path + " — build Level 1 first");
            return null;
        }
        var go = (GameObject)Object.Instantiate(
            prefab, pos, Quaternion.Euler(0f, yaw, 0f));
        go.name = prefab.name;
        return go;
    }

    static void SpawnPrefab(string path, Vector3 pos, float yaw = 0f)
    {
        SpawnPrefabAndReturn(path, pos, yaw);
    }

    static void Cameo(Vector3 pos, float yaw, string dialog = "The military sweepers are executing everyone on the surface yard... I need to get back down into the Undercroft!")
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/G1/Models/Villain.fbx");
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/G1/Anim/Villain.controller");
        if (!fbx)
            return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = "TheAuditor";
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var anim = go.GetComponent<Animator>();
        if (!anim)
            anim = go.AddComponent<Animator>();
        if (ctrl)
            anim.runtimeAnimatorController = ctrl;
        var actor = go.AddComponent<G1AuditorCutsceneActor>();
        actor.dialogLine = dialog;
        actor.triggerRadius = 12f;
        actor.vanishRadius = 4f;
    }

    // Spray-painted message from a previous loop. Empty text pulls the next line
    // from G1LoreText by tier. Face the wall by setting yaw.
    static void Graffiti(Vector3 pos, float yaw, int tier, string text = "")
    {
        var go = new GameObject("Graffiti");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var g = go.AddComponent<G1Graffiti>();
        g.tier = Mathf.Clamp(tier, 1, 3);
        g.text = text;
    }

    static GameObject Exit(string name, Vector3 pos, Vector3 size, string next, string wpLabel = "EVAC EXIT")
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = size;
        go.AddComponent<G1LevelExitTrigger>().nextScene = next;
        var wp = go.AddComponent<G1Waypoint>();
        wp.label = wpLabel;
        return go;
    }

    static void Checkpoint(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<G1Checkpoint>();
    }

    static void FinishScene(Scene scene, string path, string navAssetPath)
    {
        var navGo = new GameObject("NavMesh");
        var surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = 1 << 0;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();
        AssetDatabase.DeleteAsset(navAssetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navAssetPath);
        EditorSceneManager.SaveScene(scene, path);
        G1MenuBuilder.RegisterScenes();
        AssetDatabase.SaveAssets();
    }

    static GameObject Player(Vector3 pos, string chapter, string subtitle,
                             string ambienceClip)
    {
        var player = G1SceneBuilder.BuildStandardPlayer();
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = pos;
        cc.enabled = true;

        var cutsceneGo = new GameObject("CutsceneManager");
        cutsceneGo.AddComponent<G1CutsceneManager>();

        var introTrigger = player.AddComponent<G1IntroCutsceneTrigger>();
        introTrigger.chapterTitle = chapter;
        introTrigger.locationSubtitle = subtitle;
        introTrigger.subjectName = "Chad Thundercock";

        var card = player.GetComponent<G1StoryCard>();
        if (card != null)
        {
            card.title = chapter;
            card.subtitle = subtitle;
        }

        player.GetComponent<G1Ambience>().zones = new[]
        {
            new G1Ambience.Zone { clip = ambienceClip, zMax = 9999f },
        };
        return player;
    }

    // ------------------------------------------------------------- LEVEL 2
    [MenuItem("G1/Build Level 2 (Quarantine)")]
    public static void BuildLevel2()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode first.");
            return;
        }
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // overcast dawn: flat grey-blue ambient, no sun, distant fog
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.36f, 0.42f, 0.50f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 30f;
        RenderSettings.fogEndDistance = 90f;
        RenderSettings.fogColor = new Color(0.45f, 0.50f, 0.56f);

        var concrete = Mat(new Color(0.85f, 0.88f, 0.90f), 0f, "tex_concrete_wall", 8f, 2f);
        var asphalt = Mat(new Color(0.75f, 0.77f, 0.80f), 0f, "tex_floor_metal_grid", 10f, 10f);
        var green = Mat(new Color(0.6f, 0.7f, 0.6f), 0f, "tex_steel_panel", 2f, 2f);
        var wood = Mat(new Color(0.6f, 0.5f, 0.3f), 0f, "tex_steel_panel", 1f, 1f);

        Slab("Yard", new Vector3(0, -0.25f, 0), new Vector3(60, 0.5f, 40), asphalt);
        Slab("WallN", new Vector3(0, 2f, 20), new Vector3(60.5f, 4, 0.6f), concrete);
        Slab("WallS", new Vector3(0, 2f, -20), new Vector3(60.5f, 4, 0.6f), concrete);
        Slab("WallW", new Vector3(-30, 2f, 0), new Vector3(0.6f, 4, 40.5f), concrete);
        Slab("WallE", new Vector3(30, 2f, 0), new Vector3(0.6f, 4, 40.5f), concrete);

        // elevator exit alcove (player spawn, west side)
        Slab("ElevatorBox", new Vector3(-27f, 1.5f, 12f), new Vector3(4, 3, 4), concrete);

        // helicopter on its pad (center-east)
        Slab("HeliPad", new Vector3(12f, 0.05f, 0f), new Vector3(12, 0.1f, 12), concrete);
        Slab("HeliBody", new Vector3(12f, 1.2f, 0f), new Vector3(4f, 1.4f, 1.8f), green);
        Slab("HeliTail", new Vector3(15.4f, 1.5f, 0f), new Vector3(3f, 0.5f, 0.4f), green);
        Slab("HeliSkidL", new Vector3(12f, 0.3f, 0.9f), new Vector3(3.4f, 0.12f, 0.15f), green);
        Slab("HeliSkidR", new Vector3(12f, 0.3f, -0.9f), new Vector3(3.4f, 0.12f, 0.15f), green);
        var rotor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rotor.name = "HeliRotor";
        rotor.transform.position = new Vector3(12f, 2.1f, 0f);
        rotor.transform.localScale = new Vector3(4.4f, 0.03f, 4.4f);
        rotor.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.12f, 0.12f, 0.14f));

        // === HECU GUNSHIP BOSS — airborne over the yard ===
        var boss = new GameObject("HelicopterBoss");
        boss.transform.position = new Vector3(6f, 12f, 0f);
        var bossBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bossBody.name = "GunshipBody";
        bossBody.transform.SetParent(boss.transform, false);
        bossBody.transform.localScale = new Vector3(2f, 1.4f, 4.4f);
        bossBody.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.18f, 0.22f, 0.18f));
        var bossTail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bossTail.name = "GunshipTail";
        bossTail.transform.SetParent(boss.transform, false);
        bossTail.transform.localPosition = new Vector3(0f, 0.2f, 3.4f);
        bossTail.transform.localScale = new Vector3(0.4f, 0.5f, 3f);
        bossTail.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.18f, 0.22f, 0.18f));
        var bossRotor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bossRotor.name = "GunshipRotor";
        Object.DestroyImmediate(bossRotor.GetComponent<Collider>());
        bossRotor.transform.SetParent(boss.transform, false);
        bossRotor.transform.localPosition = new Vector3(0f, 1f, 0f);
        bossRotor.transform.localScale = new Vector3(5f, 0.04f, 5f);
        bossRotor.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.1f, 0.1f, 0.12f));
        bossRotor.AddComponent<G1WeaponSpinner>();   // spins for the rotor look
        var bossHealth = boss.AddComponent<HealthSystem>();
        bossHealth.maxHealth = 400f;
        boss.AddComponent<G1HelicopterBoss>();
        var bossBar = boss.AddComponent<WorldSpaceHealthBar>();
        bossBar.heightOffset = 2.4f;

        // scattered cover: crates + barrels
        var cratePos = new[]
        {
            new Vector3(-12, 0.4f, 4), new Vector3(-8, 0.4f, -6),
            new Vector3(0, 0.4f, 10), new Vector3(4, 0.4f, -10),
            new Vector3(-2, 0.4f, -2), new Vector3(20, 0.4f, 8),
        };
        foreach (var p in cratePos)
        {
            var crate = Slab("Crate", p, Vector3.one * 0.8f, wood);
            crate.AddComponent<Breakable>();
            crate.GetComponent<HealthSystem>().maxHealth = 50f;
        }

        // sweeper patrol (prefabs carry full AI; they hold position and aggro)
        var s1 = SpawnPrefabAndReturn("Assets/G1/Prefabs/HECUSoldier.prefab", new Vector3(6f, 0f, 12f), 200f);
        var s2 = SpawnPrefabAndReturn("Assets/G1/Prefabs/HECUSoldier.prefab", new Vector3(2f, 0f, -12f), 320f);
        var s3 = SpawnPrefabAndReturn("Assets/G1/Prefabs/HECUSoldier.prefab", new Vector3(22f, 0f, -4f), 270f);

        // Setup Level 2 Objectives
        var objGo = new GameObject("ObjectiveManager");
        var objMgr = objGo.AddComponent<G1ObjectiveManager>();
        objMgr.AddObjective("hecu_patrol", "Eliminate Surface HECU Sweeper Patrols", mandatory: true, requiredCount: 3);
        objMgr.AddObjective("evac_shaft", "Locate Maintenance Access Shaft to Undercroft", mandatory: false);

        foreach (var soldier in new[] { s1, s2, s3 })
        {
            if (soldier != null)
            {
                var hp = soldier.GetComponent<HealthSystem>();
                if (hp != null)
                    hp.OnDeath += (pos, nrm) => { if (G1ObjectiveManager.Instance != null) G1ObjectiveManager.Instance.IncrementProgress("hecu_patrol"); };
            }
        }

        G1HealthPack.Create(new Vector3(-20f, 0.5f, -8f));
        G1AmmoPack.Create(new Vector3(-14f, 0.5f, 10f));
        G1AmmoPack.Create(new Vector3(18f, 0.5f, 12f));
        G1ArmorPack.Create(new Vector3(-24f, 0.5f, 10f), 50f);
        G1WallCharger.Create(new Vector3(-26.6f, 1.1f, 12f));

        Checkpoint("Checkpoint_Yard", new Vector3(0f, 0f, 0f));
        Cameo(new Vector3(24f, 4.2f, 16f), 210f);   // on the perimeter wall

        // maintenance shaft down (east corner) → Level 3
        Slab("ShaftHousing", new Vector3(26f, 1.2f, -16f), new Vector3(4, 2.4f, 4), concrete);
        Exit("ExitToUndercroft", new Vector3(26f, 1f, -16f),
             new Vector3(2.5f, 2f, 2.5f), "Level3");

        Player(new Vector3(-26f, 0.05f, 8f), "CHAPTER TWO",
               "QUARANTINE — Surface Motor Pool, dawn", "ambient_industrial");

        FinishScene(scene, "Assets/Scenes/Level2.unity",
                    "Assets/Scenes/Level2NavMesh.asset");
        Debug.Log("G1 LEVEL2 BUILD OK");
    }

    // ------------------------------------------------------------- LEVEL 3
    [MenuItem("G1/Build Level 3 (Threshold)")]
    public static void BuildLevel3()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode first.");
            return;
        }
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.09f, 0.10f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 8f;
        RenderSettings.fogEndDistance = 45f;
        RenderSettings.fogColor = new Color(0.02f, 0.07f, 0.08f);

        var rock = Mat(new Color(0.6f, 0.65f, 0.7f), 0f, "tex_concrete_wall", 6f, 6f);
        var teal = new Color(0.16f, 0.75f, 0.75f);

        Slab("Floor", new Vector3(0, -0.25f, 20), new Vector3(30, 0.5f, 56), rock);
        Slab("WallW", new Vector3(-15, 4f, 20), new Vector3(0.6f, 8, 56.5f), rock);
        Slab("WallE", new Vector3(15, 4f, 20), new Vector3(0.6f, 8, 56.5f), rock);
        Slab("WallS", new Vector3(0, 4f, -8), new Vector3(30.5f, 8, 0.6f), rock);
        Slab("WallN", new Vector3(0, 4f, 48), new Vector3(30.5f, 8, 0.6f), rock);

        // alien pods and spore lights along the hall
        var podMat = Mat(Color.white, 0.8f, "tex_alien_bio", 2f, 2f);
        for (int i = 0; i < 6; i++)
        {
            float x = (i % 2 == 0) ? -11f : 11f;
            float z = 4f + i * 7f;
            var pod = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pod.name = "AlienPod_" + i;
            pod.transform.position = new Vector3(x, 0.9f, z);
            pod.transform.localScale = new Vector3(1.4f, 0.9f, 1.4f);
            pod.GetComponent<Renderer>().sharedMaterial = podMat;
            var lightGo = new GameObject("SporeLight_" + i);
            lightGo.transform.position = new Vector3(x, 2.2f, z);
            var li = lightGo.AddComponent<Light>();
            li.type = LightType.Point;
            li.color = teal;
            li.range = 6f;
            li.intensity = 0.9f;
        }

        // the taken and the strays defend the hall
        var enemies = new List<GameObject>
        {
            SpawnPrefabAndReturn("Assets/G1/Prefabs/Zombie.prefab", new Vector3(-6f, 0f, 12f)),
            SpawnPrefabAndReturn("Assets/G1/Prefabs/Zombie.prefab", new Vector3(6f, 0f, 18f)),
            SpawnPrefabAndReturn("Assets/G1/Prefabs/Zombie.prefab", new Vector3(-4f, 0f, 26f)),
            SpawnPrefabAndReturn("Assets/G1/Prefabs/Alien.prefab", new Vector3(8f, 0f, 24f)),
            SpawnPrefabAndReturn("Assets/G1/Prefabs/Alien.prefab", new Vector3(-8f, 0f, 32f)),
            SpawnPrefabAndReturn("Assets/G1/Prefabs/Alien.prefab", new Vector3(4f, 0f, 38f))
        };

        // Setup Level 3 Objectives
        var objGo = new GameObject("ObjectiveManager");
        var objMgr = objGo.AddComponent<G1ObjectiveManager>();
        objMgr.AddObjective("undercroft_guardians", "Neutralize Undercroft Guardians", mandatory: true, requiredCount: 6);
        objMgr.AddObjective("threshold_portal", "Step through the Threshold — OR break its emitters to end the loop", mandatory: false);

        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                var hp = enemy.GetComponent<HealthSystem>();
                if (hp != null)
                    hp.OnDeath += (pos, nrm) => { if (G1ObjectiveManager.Instance != null) G1ObjectiveManager.Instance.IncrementProgress("undercroft_guardians"); };
            }
        }

        G1HealthPack.Create(new Vector3(-12f, 0.5f, 10f));
        G1HealthPack.Create(new Vector3(12f, 0.5f, 30f));
        G1AmmoPack.Create(new Vector3(-12f, 0.5f, 22f));
        G1AmmoPack.Create(new Vector3(12f, 0.5f, 14f));

        Checkpoint("Checkpoint_Undercroft", new Vector3(0f, 0f, 2f));

        // THE THRESHOLD: ring of emissive blocks at the far end
        var ringMat = Mat(teal, 2.2f);
        Vector3 center = new Vector3(0f, 2.4f, 44f);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "ThresholdRing_" + i;
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.transform.position = center
                + new Vector3(Mathf.Cos(a) * 2.2f, Mathf.Sin(a) * 2.2f, 0f);
            block.transform.localScale = new Vector3(0.5f, 0.5f, 0.3f);
            block.transform.rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
            block.GetComponent<Renderer>().sharedMaterial = ringMat;
        }
        var portalLight = new GameObject("ThresholdLight");
        portalLight.transform.position = center;
        var pl = portalLight.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.color = teal;
        pl.range = 14f;
        pl.intensity = 2.5f;

        // RESONANCE EMITTERS: the ring's anchor points. Step through the portal
        // to stabilize (loop continues), or smash every emitter with the crowbar
        // to collapse the ring (loop breaks). See G1ResonanceEmitter / G1EndingCutscene.
        var emitterMat = Mat(new Color(0.95f, 0.5f, 0.12f), 2.6f);   // hot amber — "hit me"
        float[] emitterX = { -3f, 0f, 3f };
        foreach (float ex in emitterX)
        {
            var emitter = GameObject.CreatePrimitive(PrimitiveType.Cube);   // keeps its BoxCollider so the crowbar can hit it
            emitter.name = "ResonanceEmitter";
            // In front of the portal trigger (z 40-44) so the player can smash
            // them to collapse the loop WITHOUT stepping through and stabilizing it.
            emitter.transform.position = new Vector3(ex, 1.1f, 38.5f);
            emitter.transform.localScale = new Vector3(0.45f, 2.2f, 0.45f);
            emitter.GetComponent<Renderer>().sharedMaterial = emitterMat;
            var eh = emitter.AddComponent<HealthSystem>();
            eh.maxHealth = 60f;
            emitter.AddComponent<Breakable>();
            emitter.AddComponent<G1ResonanceEmitter>();
        }

        // Messages from previous loops, telling the player how to actually win.
        Graffiti(new Vector3(-6.2f, 2.2f, 42.8f), 90f, 3, "BREAK THE RING, NOT THE GLASS");
        Graffiti(new Vector3(6.2f, 2.2f, 41.5f), -90f, 3, "YOU ARE THE ANCHOR — END IT");

        Cameo(new Vector3(3.5f, 0f, 43f), 250f, "That suit guy again... standing right beside the portal ring. No time to ask questions — ESCAPE NOW!");

        // ending disposition cutscene trigger at the Xen Threshold Portal
        var endCutsceneTrigger = new GameObject("EndingCutsceneTrigger");
        endCutsceneTrigger.transform.position = new Vector3(0f, 1.5f, 42f);
        var endCol = endCutsceneTrigger.AddComponent<BoxCollider>();
        endCol.isTrigger = true;
        endCol.size = new Vector3(8f, 4f, 4f);
        endCutsceneTrigger.AddComponent<G1EndingCutscene>();

        Player(new Vector3(0f, 0.05f, 0f), "CHAPTER THREE",
               "THRESHOLD — The Undercroft", "ambient_alien");

        FinishScene(scene, "Assets/Scenes/Level3.unity",
                    "Assets/Scenes/Level3NavMesh.asset");
        Debug.Log("G1 LEVEL3 BUILD OK");
    }
}
