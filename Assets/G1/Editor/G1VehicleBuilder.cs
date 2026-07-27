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
        var spots = new (Vector3 at, float yaw, string name)[]
        {
            (new Vector3(-9f, 0f, gateZ - 16f), 0f,    "Truck_Staging"),
            (new Vector3(-40f, 0f, -62f),       25f,   "Truck_MotorPool"),
            (new Vector3(-150f, 0f, 34f),       90f,   "Truck_AlliedBase"),
            (new Vector3(150f, 0f, -30f),      -90f,   "Truck_Hangar"),
            (new Vector3(20f, 0f, 150f),        180f,  "Truck_Ruins"),
            (new Vector3(-286f, 0f, 20f),       90f,   "Truck_TankPark"),
        };

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

    static void Truck(string name, Vector3 at, float yaw)
    {
        var olive = Mat(new Color(0.22f, 0.24f, 0.16f));
        var dark = Mat(new Color(0.06f, 0.06f, 0.07f));
        var glass = Mat(new Color(0.12f, 0.18f, 0.20f));

        var root = new GameObject(name);
        root.transform.position = at + Vector3.up * 0.55f;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

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

        // one box collider for the whole vehicle: the player rides inside it,
        // so it must not be a mesh the driver can catch on
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.6f, 0f);
        col.size = new Vector3(2.3f, 1.9f, 5.4f);

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
