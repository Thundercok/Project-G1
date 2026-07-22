using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// Assembles a playable scene around the large Blender-generated facility mesh
/// (Tools/blender/build_map.py -> CorvusFacility.fbx). Adds collision, lighting,
/// the full player rig + opening cinematic, story lore spread across the wings,
/// a light enemy sprinkle (Level 1 stays easy), an exit at the motor-pool gate,
/// and bakes the NavMesh.
///
/// Prereq: run the Blender script first so the FBX exists:
///   blender --background --python Tools/blender/build_map.py -- . Assets/G1/Models/Environment
public static class G1BigMapBuilder
{
    const string ScenePath = "Assets/Scenes/BigMap.unity";
    const string NavPath   = "Assets/Scenes/BigMapNavMesh.asset";
    const string FbxPath   = "Assets/G1/Models/Environment/CorvusFacility.fbx";
    const string EnvDir    = "Assets/G1/Models/Environment";
    const string SceneName = "BigMap";

    [MenuItem("G1/Build Big Map (Corvus Facility)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building.");
            return;
        }

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbx == null)
        {
            Debug.LogError($"G1: {FbxPath} not found. Generate it first:\n" +
                "blender --background --python Tools/blender/build_map.py -- . " + EnvDir);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- Lighting ----
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.4f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.85f, 0.88f, 1f);
        sun.intensity = 1.1f;
        sun.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
        sun.shadows = LightShadows.Soft;

        // ---- Facility mesh + collision ----
        var map = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        map.name = "CorvusFacility";
        map.transform.position = Vector3.zero;
        foreach (var mf in map.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var mc = mf.GetComponent<MeshCollider>();
            if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;   // static, non-convex
        }
        SetLayer(map, 0);   // Default layer so NavMesh bakes over it

        // Fill lights across the big footprint.
        PointLight(new Vector3(0f, 5f, 0f), new Color(0.9f, 0.92f, 1f), 34f, 1.4f);
        PointLight(new Vector3(0f, 5f, 30f), new Color(0.95f, 0.95f, 0.9f), 30f, 1.2f);   // labs
        PointLight(new Vector3(0f, 5f, -32f), new Color(0.2f, 1f, 0.75f), 30f, 1.4f);     // reactor
        PointLight(new Vector3(-38f, 5f, 0f), new Color(0.7f, 0.75f, 0.9f), 26f, 1.1f);   // records
        PointLight(new Vector3(38f, 5f, 0f), new Color(1f, 0.75f, 0.45f), 30f, 1.2f);     // motor pool

        // ---- Player rig + opening cinematic ----
        var player = G1SceneBuilder.BuildStandardPlayer();
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = new Vector3(0f, 0.15f, -10f);   // atrium, facing north
        cc.enabled = true;

        new GameObject("CutsceneManager").AddComponent<G1CutsceneManager>();
        var intro = player.GetComponent<G1IntroCutsceneTrigger>();
        if (intro != null)
        {
            intro.chapterTitle = "CHAPTER ONE: COLD START";
            intro.locationSubtitle = "Corvus Deep Research Annex — Sub-Level C";
            intro.introSceneName = SceneName;   // so the narrative plays here too
        }

        // ---- Story lore across the wings ----
        // Atrium hub — the briefing.
        Term(new Vector3(0f, 1.2f, -14f), 0f,
            "CORVUS DEEP RESEARCH ANNEX — SUB-LEVEL C. PROJECT G1: hold open the Threshold. If the alarm is sounding, it has already failed. It always fails. Reach the motor-pool gate — EAST.");
        Card(new Vector3(0f, 1.5f, -6f), new Vector3(8f, 3f, 6f),
            "CHAPTER ONE", "COLD START — explore the Annex; learn what keeps happening");
        Cctv(new Vector3(-1f, 2.6f, 14.4f), 180f);
        Auditor(new Vector3(0f, 2.9f, 15f), 180f);   // watching from the mezzanine

        // Records vault (west) — the personnel/maintenance truth + a past you.
        Term(new Vector3(-40f, 1.2f, 6f), 90f, "PERSONNEL FILE — C. THUNDERCOCK: " + G1LoreText.LoreCards[0].body);
        Term(new Vector3(-40f, 1.2f, -6f), 90f, "MAINTENANCE LOG — SUB-LEVEL C: " + G1LoreText.LoreCards[1].body);
        Graf(new Vector3(-59.4f, 2.6f, 4f), 90f, 1, "IT FAILS AT 0600");
        Graf(new Vector3(-59.4f, 2.6f, -4f), 90f, 1, "WE'VE BEEN HERE");
        EnvProp("prop_body_soldier", new Vector3(-30f, 0f, 0f), 25f, 0.85f, "DeadEngineer_PastLoop");

        // Reactor hall (south) — the Auditor's audit + a warning.
        Term(new Vector3(-10f, 1.2f, -46f), 0f, "RECOVERED AUDIO — THE AUDITOR: " + G1LoreText.LoreCards[2].body);
        Term(new Vector3(10f, 1.2f, -46f), 0f, "CONCORDANCE MEMO: " + G1LoreText.LoreCards[3].body);
        Graf(new Vector3(-23.4f, 2.6f, -40f), 90f, 2, "HE COUNTS US");
        Graf(new Vector3(23.4f, 2.6f, -40f), -90f, 2, "THE DOOR GOES BACKWARDS");

        // Labs (north) — a couple of notes.
        Term(new Vector3(0f, 1.2f, 46f), 180f, "LAB LOG: widening test scheduled 0600. Resonance climbing. Recommend abort. Abort request DENIED — no requesting officer on record.");
        Graf(new Vector3(0f, 2.6f, 47.4f), 180f, 3, "COUNT THE DOORS");

        // Motor pool (east) — pointer to the exit.
        Graf(new Vector3(57f, 2.6f, 3f), -90f, 1, "SURFACE ACCESS →");

        // ---- Enemies (Level 1 stays easy: a light sprinkle over a huge map) ----
        Enemy("Zombie", new Vector3(10f, 0f, 28f));
        Enemy("Zombie", new Vector3(-12f, 0f, 34f));
        Enemy("Zombie", new Vector3(22f, 0f, 22f));
        Enemy("Alien",  new Vector3(0f, 0f, -30f));
        Enemy("Alien",  new Vector3(9f, 0f, -38f));
        Enemy("HECUSoldier", new Vector3(34f, 0f, 7f));
        Enemy("HECUSoldier", new Vector3(40f, 0f, -7f));
        new GameObject("SquadBlackboard").AddComponent<SquadBlackboard>();

        // ---- Level task: a story question that gates the exit ----
        // The answer is discoverable in the records/reactor lore — explore to pass.
        var objMgr = new GameObject("ObjectiveManager").AddComponent<G1ObjectiveManager>();
        objMgr.AddObjective("solve_gate", "Solve the gate console (answer is in the facility records)", mandatory: true);

        var qGo = EnvProp("prop_computer_terminal", new Vector3(52f, 1.0f, 3f), -90f, 1.0f, "GateQuestionConsole");
        var q = qGo.AddComponent<G1QuestionTerminal>();
        q.question = "The records say Project G1 has failed before. How many times?";
        q.options = new[] { "Only once — today", "A handful of times", "Hundreds — it loops (ITERATION ##7)" };
        q.correctIndex = 2;
        q.objectiveId = "solve_gate";

        // ---- Exit at the motor-pool gate (east), locked until the question is solved ----
        var exit = new GameObject("LevelExitTrigger");
        exit.transform.position = new Vector3(57f, 1f, 0f);
        var ecol = exit.AddComponent<BoxCollider>();
        ecol.isTrigger = true;
        ecol.size = new Vector3(3f, 3f, 8f);
        var et = exit.AddComponent<G1LevelExitTrigger>();
        et.nextScene = "Level2";
        et.requireUnlock = true;         // opens only after the console is solved

        // ---- NavMesh bake (Default-layer geometry only) ----
        var surface = new GameObject("NavMesh").AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = 1 << 0;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();

        EnsureFolder("Assets/Scenes");
        AssetDatabase.DeleteAsset(NavPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavPath);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("G1 BIG MAP BUILD OK — CorvusFacility assembled. Open Assets/Scenes/BigMap.unity and press Play.");
    }

    // --------------------------------------------------------------- helpers
    static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
    }

    static void PointLight(Vector3 p, Color c, float range, float intensity)
    {
        var l = new GameObject("Light").AddComponent<Light>();
        l.type = LightType.Point;
        l.transform.position = p;
        l.color = c;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
    }

    static void Term(Vector3 p, float yaw, string msg)
    {
        var go = EnvProp("prop_computer_terminal", p, yaw, 0.9f, "LoreTerminal");
        go.AddComponent<G1Terminal>().logMessage = msg;
    }

    static void Graf(Vector3 p, float yaw, int tier, string text)
    {
        var go = new GameObject("MapGraffiti");
        go.transform.position = p;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var g = go.AddComponent<G1Graffiti>();
        g.tier = tier;
        g.text = text;
    }

    static void Card(Vector3 p, Vector3 size, string title, string sub)
    {
        var go = new GameObject("MapLoreCard");
        go.transform.position = p;
        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = size;
        var c = go.AddComponent<G1StoryCard>();
        c.showOnStart = false;
        c.title = title;
        c.subtitle = sub;
    }

    static void Cctv(Vector3 p, float yaw)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Map_CCTV_Screen";
        q.transform.position = p;
        q.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        q.transform.localScale = new Vector3(1.4f, 1.0f, 1f);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.AddComponent<G1CCTVScreen>();
    }

    static void Auditor(Vector3 p, float yaw)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/G1/Models/Villain.fbx");
        if (fbx == null) return;
        var go = (GameObject)Object.Instantiate(fbx, p, Quaternion.Euler(0f, yaw, 0f));
        go.name = "TheAuditor_Map";
        go.AddComponent<G1GManCameo>().vanishDistance = 8f;
    }

    static GameObject EnvProp(string asset, Vector3 pos, float yaw, float scale, string name)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/{asset}.fbx");
        GameObject go;
        if (fbx != null)
            go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = Vector3.one * scale;
        }
        go.name = name;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (fbx != null) go.transform.localScale = Vector3.one * scale;
        return go;
    }

    static void Enemy(string prefabName, Vector3 pos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/G1/Prefabs/{prefabName}.prefab");
        if (prefab == null)
        {
            Debug.LogWarning($"G1: prefab {prefabName} missing — build Level 1 (G1 > Build Test Scene) once to generate enemy prefabs.");
            return;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = pos;
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            int i = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
    }
}
