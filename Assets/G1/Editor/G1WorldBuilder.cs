using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builds both levels into a single scene: one world, one NavMesh, no loading.
///
/// The two maps were built separately and joined by a teleport gate, which was
/// the right call while they were being made — a broken Cradle Station could
/// not break the Sprawl. It is the wrong call to ship: a scene change in the
/// middle of a chase is a hard cut, the player loses every vehicle they had,
/// and "the research facility the outbreak came from" reads as a different game
/// rather than as somewhere you can see from the top of the command tower.
///
/// So Cradle Station is planted 1.1 km east of the Sprawl and joined by road.
/// The distance is not arbitrary: the Sprawl reaches x = 400 and Cradle reaches
/// 240 either side of its own centre, so 1100 leaves 460 m of open ground
/// between the two perimeters — far enough that neither is visible through the
/// other's fog, close enough to drive in about a minute.
///
/// Menu: G1 → Build One World.
public static class G1WorldBuilder
{
    const string ScenePath = "Assets/Scenes/World.unity";
    const string SprawlScene = "Assets/Scenes/HugeMap.unity";
    const string CradleFbx = "Assets/G1/Models/Environment/CradleStation.fbx";

    /// Where Cradle Station's origin lands in the shared world.
    public static readonly Vector3 CradleOffset = new Vector3(1100f, 0f, 0f);

    [MenuItem("G1/Build One World")]
    public static void BuildWorld()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building scenes.");
            return;
        }

        // The Sprawl is the bigger, older and more entangled of the two — it
        // owns the player, the story director, the contacts and the vehicles —
        // so it is built first and everything else is added on top rather than
        // the two being merged as equals.
        G1HugeMapBuilder.BuildHugeMap();
        var scene = EditorSceneManager.OpenScene(SprawlScene);

        InstallTerrain();

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CradleFbx);
        if (fbx == null)
        {
            Debug.LogError("CradleStation.fbx missing — run build_research_base.py first.");
            return;
        }

        var map = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        map.name = "CradleStation";
        map.transform.position = CradleOffset;
        foreach (var mf in map.GetComponentsInChildren<MeshFilter>())
        {
            var mc = mf.gameObject.GetComponent<MeshCollider>();
            if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }
        G1MapSkin.Apply(map);
        Physics.SyncTransforms();

        // Cradle's own ground plane, so the far half of the world is standable
        // even where the FBX ground mesh does not reach.
        var floor = new GameObject("CradleGroundCollider");
        floor.transform.position = CradleOffset + new Vector3(0f, -0.25f, 0f);
        floor.AddComponent<BoxCollider>().size = new Vector3(500f, 0.5f, 500f);

        var manifest = G1MapManifest.Load(CradleFbx);
        int lamps = G1MapManifest.ApplyLighting(manifest, CradleOffset);
        foreach (var l in Object.FindObjectsOfType<Light>())
            if (l.type != LightType.Directional && l.transform.position.x > 700f)
                l.intensity *= 0.45f;              // Cradle is still on standby power
        int stocked = G1MapManifest.StockInteriors(manifest, CradleOffset);
        int cover = G1MapManifest.ApplyCover(manifest, CradleOffset);
        G1MapManifest.AppendInteriorSpaces(manifest, CradleOffset);

        int devices = G1CradleBuilder.PopulateDevices(manifest, CradleOffset);
        int hosts = G1CradleBuilder.PopulateHosts(CradleOffset);
        G1CradleBuilder.AddObjectives();

        int link = BuildLinkRoad();

        // the cold open, once the world it flies over exists
        G1OpeningBuilder.Install(GameObject.FindWithTag("Player"));

        // One NavMesh over both halves. The link road is what makes this worth
        // doing: without it the two islands would be separately reachable and
        // an agent could never path from one to the other, which is exactly the
        // situation the teleport gate existed to paper over.
        foreach (var old in Object.FindObjectsOfType<NavMeshSurface>())
            Object.DestroyImmediate(old.gameObject);
        var navGo = new GameObject("NavMesh");
        var surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = 1 << 0;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();

        Physics.SyncTransforms();
        int pruned = G1MapManifest.PruneCover();

        // The gate no longer moves you between scenes, because there is only
        // one. It stays as the ending.
        foreach (var exit in Object.FindObjectsOfType<G1LevelExitTrigger>())
            exit.nextScene = "MenuScene";

        AssetDatabase.DeleteAsset("Assets/Scenes/WorldNavMesh.asset");
        AssetDatabase.CreateAsset(surface.navMeshData, "Assets/Scenes/WorldNavMesh.asset");
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterScene();
        AssetDatabase.SaveAssets();

        Debug.Log($"G1 WORLD BUILD OK — Sprawl + Cradle in one scene, " +
                  $"{lamps} extra lights, {stocked} extra caches, {cover - pruned} extra cover, " +
                  $"{devices} devices, {hosts} parasitised units, {link} link-road pieces.");
    }

    /// Drops the generated terrain under both bases.
    ///
    /// It is flat and hidden beneath their ground slabs inside the wire, and
    /// only rises past the perimeter — so it can be added to a finished world
    /// without moving a single building. Its collider is a mesh collider
    /// because the hills are the one thing on this map you actually walk on
    /// that is not a box.
    static void InstallTerrain()
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/G1/Models/Environment/Terrain.fbx");
        if (fbx == null)
        {
            Debug.LogWarning("G1: no Terrain.fbx — run build_terrain.py; " +
                             "the world will be flat to the horizon.");
            return;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = "Terrain";
        go.transform.position = Vector3.zero;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            var mc = mf.gameObject.GetComponent<MeshCollider>();
            if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }
        var mat = new Material(Shader.Find("Standard"));
        var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/G1/External/PolyHavenTextures/sandy_gravel/sandy_gravel_Diffuse.jpg");
        var nor = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/G1/External/PolyHavenTextures/sandy_gravel/sandy_gravel_nor_gl.jpg");
        if (diff != null) { mat.mainTexture = diff; mat.mainTextureScale = Vector2.one; }
        if (nor != null) { mat.EnableKeyword("_NORMALMAP"); mat.SetTexture("_BumpMap", nor); }
        mat.color = new Color(0.62f, 0.58f, 0.50f);
        mat.SetFloat("_Glossiness", 0.03f);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
        Physics.SyncTransforms();
    }

    /// A road from the Sprawl's east gate to Cradle Station's south gate.
    ///
    /// It is also the NavMesh bridge, so it has to be continuous and wide
    /// enough for an agent — a decorative strip with a gap in it would leave
    /// the two halves unreachable from each other and nothing would say so
    /// until an ally walked into a fence and stopped.
    static int BuildLinkRoad()
    {
        var mat = new Material(Shader.Find("Standard"));
        var asphalt = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/G1/External/PolyHavenTextures/asphalt_02/asphalt_02_Diffuse.jpg");
        if (asphalt != null) { mat.mainTexture = asphalt; mat.mainTextureScale = new Vector2(4f, 40f); }
        mat.color = new Color(0.55f, 0.55f, 0.56f);
        mat.SetFloat("_Glossiness", 0.1f);

        var root = new GameObject("LinkRoad");
        // from x=380 (inside the Sprawl's east wall) to x=880 (Cradle's west
        // approach), then north to the gatehouse road at Cradle z=+200
        var spans = new List<(Vector3 at, Vector3 size)>
        {
            (new Vector3(640f, 0.03f, 0f), new Vector3(540f, 0.12f, 14f)),
            (new Vector3(1100f, 0.03f, 110f), new Vector3(14f, 0.12f, 240f)),
            (new Vector3(1000f, 0.03f, 0f), new Vector3(220f, 0.12f, 14f)),
        };
        int n = 0;
        foreach (var s in spans)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Link_" + n;
            go.transform.SetParent(root.transform, false);
            go.transform.position = s.at;
            go.transform.localScale = s.size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            n++;
        }
        // a sign at the junction, so the road reads as going somewhere
        var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "LinkSign";
        sign.transform.SetParent(root.transform, false);
        sign.transform.position = new Vector3(420f, 4f, 10f);
        sign.transform.localScale = new Vector3(0.4f, 2.2f, 12f);
        sign.GetComponent<Renderer>().sharedMaterial = mat;
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
