using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Parks drivable trucks around the Sprawl.
///
/// Built from primitives rather than a Blender export on purpose: the map's
/// own trucks are baked into the merged terrain mesh and cannot be separated,
/// and a vehicle needs its own transform to drive. This matches how the
/// gunship boss is assembled.
///
/// Placement matters more than the model. A truck the player never walks past
/// is a truck that does not exist, so they sit where you are already standing:
/// at Kane's staging post before the gate, in the motor pool, and at the far
/// districts where the walk back is longest.
public static class G1VehicleBuilder
{
    [MenuItem("G1/Build Vehicles")]
    public static void BuildStandalone()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building vehicles.");
            return;
        }
        Build();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    public static int Build(float gateZ = -352f)
    {
        // Six trucks on 640,000 square metres meant most journeys started with
        // a walk to find one. These are laid on the two ring roads and at every
        // district, so from anywhere on the map there is one within about 80m —
        // close enough that "drive there" is a real option rather than a lucky
        // find.
        var spots = new List<(Vector3 at, float yaw, string name)>
        {
            // the ones you meet first
            (new Vector3(-9f, 0f, gateZ - 16f),  0f,   "Truck_Staging"),
            (new Vector3(11f, 0f, gateZ - 20f),  0f,   "Truck_Staging2"),
            (new Vector3(-6f, 0f, gateZ + 46f),  0f,   "Truck_PastGate"),
            // districts
            (new Vector3(-40f, 0f, -62f),        25f,  "Truck_MotorPool"),
            (new Vector3(-150f, 0f, 34f),        90f,  "Truck_AlliedBase"),
            (new Vector3(-176f, 0f, -30f),       90f,  "Truck_AlliedBase2"),
            (new Vector3(150f, 0f, -30f),       -90f,  "Truck_Hangar"),
            (new Vector3(176f, 0f, 26f),        -90f,  "Truck_Hangar2"),
            (new Vector3(20f, 0f, 150f),         180f, "Truck_Ruins"),
            (new Vector3(-30f, 0f, -150f),       0f,   "Truck_Labs"),
            (new Vector3(140f, 0f, 130f),        180f, "Truck_Comms"),
            (new Vector3(150f, 0f, -150f),       0f,   "Truck_Warehouse"),
            (new Vector3(-140f, 0f, -140f),      45f,  "Truck_Quarters"),
            (new Vector3(-286f, 0f, 20f),        90f,  "Truck_TankPark"),
            (new Vector3(250f, 0f, -40f),       -90f,  "Truck_Airstrip"),
            (new Vector3(-40f, 0f, 296f),        90f,  "Truck_AmmoField"),
        };
        // and one at each quarter of both ring roads, so the long hauls always
        // have something parked on the way
        foreach (int r in new[] { 200, 330 })
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                spots.Add((new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r),
                           -a * Mathf.Rad2Deg, $"Truck_Ring{r}_{i}"));
            }

        int made = 0;
        foreach (var s in spots)
        {
            // a truck half-buried in a berm is worse than no truck
            var at = G1Placement.FindClearFootprint(s.at, new Vector2(2.4f, 4.2f), s.name);
            Truck(s.name, at, s.yaw);
            made++;
        }
        Debug.Log($"G1: {made} drivable trucks parked — press E at one to drive.");
        return made;
    }

    /// Park one truck at an arbitrary spot. Cradle Station's motor pool
    /// declares its own bays in the map manifest, so it needs the factory
    /// without the Sprawl's baked-in list of parking spaces.
    public static void SpawnAt(Vector3 at, float yaw, string name = "Truck")
    {
        var spot = G1Placement.FindClearFootprint(at, new Vector2(2.4f, 4.2f), name);
        Truck(name, spot, yaw);
    }

    const string TruckFbx = "Assets/G1/Models/Vehicles/Truck.fbx";

    /// Park a tank or an APC. Not drivable — armour on a base is scenery, and
    /// scenery this size does a job the trucks cannot: it gives the motor pool
    /// and the tank park something at their centre that says what they are
    /// from two hundred metres away.
    public static GameObject Armour(string kind, Vector3 at, float yaw)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/G1/Models/Vehicles/{kind}.fbx");
        if (fbx == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = kind;
        go.transform.position = G1Placement.FindClearFootprint(
            at, new Vector2(3.4f, 7.2f), kind);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        // one box rather than the hull mesh: an AI that catches on a road wheel
        // is worse than one that walks around a slightly bigger rectangle
        foreach (var c in go.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(c);
        // The FBX origin sits on the ground between the tracks/wheels, so the
        // collider is measured up from zero. Sized to the hull rather than to a
        // guess: an AI that walks through the back half of a tank is worse than
        // one that walks round a box slightly larger than it.
        var col = go.AddComponent<BoxCollider>();
        col.center = kind == "Tank" ? new Vector3(0f, 1.35f, 0f)
                                    : new Vector3(0f, 1.45f, 0f);
        col.size = kind == "Tank" ? new Vector3(3.6f, 2.70f, 7.60f)
                                  : new Vector3(3.0f, 2.90f, 7.40f);
        G1VehicleSkin.Apply(go);
        return go;
    }

    /// The armour a base would actually have standing on it.
    public static int ParkArmour((Vector3 at, float yaw, string kind)[] spots)
    {
        int n = 0;
        foreach (var s in spots)
            if (Armour(s.kind, s.at, s.yaw) != null) n++;
        Debug.Log($"G1: {n} armoured vehicles parked.");
        return n;
    }


    static void Truck(string name, Vector3 at, float yaw)
    {
        var olive = Mat(new Color(0.22f, 0.24f, 0.16f));
        var dark = Mat(new Color(0.06f, 0.06f, 0.07f));
        var glass = Mat(new Color(0.12f, 0.18f, 0.20f));

        var root = new GameObject(name);
        root.transform.position = at + Vector3.up * 0.55f;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // The modelled 6x6 if it has been generated, the assembled boxes if
        // not. Everything below — collider, seat, lights, G1Vehicle — is the
        // same either way, so the truck drives identically and only its
        // appearance changes. The fallback stays because a missing FBX should
        // cost you a good-looking truck, not a drivable one.
        var body = AssetDatabase.LoadAssetAtPath<GameObject>(TruckFbx);
        if (body != null)
        {
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(body);
            mesh.name = "Body";
            mesh.transform.SetParent(root.transform, false);
            // the Blender origin is on the ground between the wheels; the root
            // is raised half a metre so the box fallback sits right
            mesh.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            foreach (var c in mesh.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
            G1VehicleSkin.Apply(mesh);
            InstallTruckExtras(root, dark);
            return;
        }

        Slab("Chassis", root, new Vector3(0f, 0.15f, 0f), new Vector3(2.3f, 0.7f, 5.2f), olive);
        Slab("Cab", root, new Vector3(0f, 0.95f, -1.25f), new Vector3(2.1f, 1.1f, 2.0f), olive);
        Slab("Windshield", root, new Vector3(0f, 1.05f, -2.24f), new Vector3(1.8f, 0.7f, 0.1f), glass);
        Slab("Bed", root, new Vector3(0f, 0.85f, 1.5f), new Vector3(2.2f, 0.9f, 2.5f), olive);
        Slab("BedRail", root, new Vector3(0f, 1.35f, 1.5f), new Vector3(2.25f, 0.1f, 2.55f), dark);
        Slab("Bumper", root, new Vector3(0f, 0.1f, -2.7f), new Vector3(2.3f, 0.35f, 0.25f), dark);

        foreach (var (x, z) in new[] { (-1.05f, -1.5f), (1.05f, -1.5f), (-1.05f, 1.5f), (1.05f, 1.5f) })
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(w.GetComponent<Collider>());
            w.name = "Wheel";
            w.transform.SetParent(root.transform, false);
            w.transform.localPosition = new Vector3(x, -0.15f, z);
            w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            w.transform.localScale = new Vector3(0.7f, 0.16f, 0.7f);
            w.GetComponent<Renderer>().sharedMaterial = dark;
        }

        InstallTruckExtras(root, dark);
    }

    /// Collider, seat, headlights and the drive component — the parts that make
    /// a truck a vehicle rather than a decoration.
    static void InstallTruckExtras(GameObject root, Material dark)
    {
        var lights = new List<Light>();
        foreach (float x in new[] { -0.75f, 0.75f })
        {
            var lens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(lens.GetComponent<Collider>());
            lens.name = "Headlight";
            lens.transform.SetParent(root.transform, false);
            lens.transform.localPosition = new Vector3(x, 0.5f, -2.66f);
            lens.transform.localScale = new Vector3(0.34f, 0.24f, 0.1f);
            lens.GetComponent<Renderer>().sharedMaterial = Mat(new Color(1f, 0.95f, 0.8f), 2.2f);

            var lgo = new GameObject("HeadlightBeam");
            lgo.transform.SetParent(root.transform, false);
            lgo.transform.localPosition = new Vector3(x, 0.5f, -2.7f);
            lgo.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = 45f; l.spotAngle = 55f; l.intensity = 2.6f;
            l.color = new Color(1f, 0.96f, 0.85f);
            l.enabled = false;                  // on only while someone is driving
            lights.Add(l);
        }

        // One box for the whole vehicle: the player rides inside it, so it must
        // not be a mesh the driver can catch on, and G1Vehicle uses this exact
        // box for both the wall sweep and the ram — a truck that hits things
        // with a box smaller than itself drives its nose through walls and its
        // bumper through soldiers without touching either.
        //
        // Sized to the modelled 6x6: 6.9 m long, 2.2 wide, roof of the tilt
        // 2.66 m off the ground. The root sits 0.55 m up, so the ground is at
        // local y = -0.55 and the roof at local y = 2.11.
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.78f, 0.30f);
        col.size = new Vector3(2.24f, 2.66f, 6.90f);

        var seat = new GameObject("Seat");
        seat.transform.SetParent(root.transform, false);
        seat.transform.localPosition = new Vector3(-0.45f, 0.55f, -1.5f);

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var v = root.AddComponent<G1Vehicle>();
        v.seat = seat.transform;
        v.headlights = lights.ToArray();

        var wp = root.AddComponent<G1Waypoint>();
        wp.label = "TRUCK";
        wp.isActive = false;                    // only the scanner-style HUD uses it
    }

    static GameObject Slab(string name, GameObject parent, Vector3 pos, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

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
}
