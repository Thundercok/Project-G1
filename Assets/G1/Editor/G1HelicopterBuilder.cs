using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Parks flyable helicopters on the pads the map already has.
///
/// Placement is the design: a helicopter at the allied helipad, one on the
/// airstrip apron and one at the command plaza means the three places you
/// most want to leave from are the three places you can. The rest of the map
/// is 800m of walking on purpose.
public static class G1HelicopterBuilder
{
    [MenuItem("G1/Build Helicopters")]
    public static void BuildStandalone()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building helicopters.");
            return;
        }
        Build();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    public static int Build()
    {
        var spots = new (Vector3 at, float yaw, string name)[]
        {
            (new Vector3(-186f, 0f, 0f),    90f, "Heli_AlliedPad"),   // the helipad
            (new Vector3(258f, 0f, 60f),   -90f, "Heli_Airstrip"),
            (new Vector3(-52f, 0f, -40f),    0f, "Heli_Plaza"),
        };
        int n = 0;
        foreach (var s in spots)
        {
            var at = G1Placement.FindClearFootprint(s.at, new Vector2(4f, 6f), s.name,
                                                    searchRadius: 34f);
            Heli(s.name, at, s.yaw);
            n++;
        }
        Debug.Log($"G1: {n} helicopters on the pads — E to board, W/S climb, " +
                  "arrows to fly, E to land and leave.");
        return n;
    }

    static void Heli(string name, Vector3 at, float yaw)
    {
        var olive = Mat(new Color(0.19f, 0.21f, 0.15f));
        var dark = Mat(new Color(0.05f, 0.05f, 0.06f));
        var glass = Mat(new Color(0.10f, 0.15f, 0.17f));

        var root = new GameObject(name);
        root.transform.position = at + Vector3.up * 1.6f;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Slab("Hull", root, new Vector3(0f, 0f, 0.4f), new Vector3(2.4f, 2.0f, 5.6f), olive);
        Slab("Nose", root, new Vector3(0f, -0.2f, -3.0f), new Vector3(2.0f, 1.5f, 1.8f), olive);
        Slab("Canopy", root, new Vector3(0f, 0.35f, -2.4f), new Vector3(1.9f, 1.1f, 1.6f), glass);
        Slab("TailBoom", root, new Vector3(0f, 0.35f, 4.6f), new Vector3(0.6f, 0.6f, 4.4f), olive);
        Slab("TailFin", root, new Vector3(0f, 1.1f, 6.5f), new Vector3(0.2f, 1.4f, 1.0f), olive);
        Slab("Mast", root, new Vector3(0f, 1.2f, 0.2f), new Vector3(0.5f, 0.6f, 0.5f), dark);
        foreach (int s in new[] { -1, 1 })
        {
            Slab($"Skid{s}", root, new Vector3(s * 1.2f, -1.35f, 0.2f),
                 new Vector3(0.18f, 0.18f, 4.6f), dark);
            Slab($"SkidLeg{s}", root, new Vector3(s * 1.1f, -0.75f, 0.2f),
                 new Vector3(0.14f, 1.2f, 0.14f), dark);
        }

        // rotors are separate transforms because the flight script spins them
        var main = new GameObject("MainRotor");
        main.transform.SetParent(root.transform, false);
        main.transform.localPosition = new Vector3(0f, 1.6f, 0.2f);
        for (int i = 0; i < 4; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.name = "Blade";
            b.transform.SetParent(main.transform, false);
            b.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            b.transform.localPosition = b.transform.localRotation * new Vector3(0f, 0f, 3.6f);
            b.transform.localScale = new Vector3(0.34f, 0.06f, 7.2f);
            b.GetComponent<Renderer>().sharedMaterial = dark;
        }
        var tail = new GameObject("TailRotor");
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0.3f, 1.1f, 6.6f);
        for (int i = 0; i < 2; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.name = "TailBlade";
            b.transform.SetParent(tail.transform, false);
            b.transform.localRotation = Quaternion.Euler(i * 90f, 0f, 0f);
            b.transform.localPosition = b.transform.localRotation * new Vector3(0f, 1.3f, 0f);
            b.transform.localScale = new Vector3(0.06f, 2.6f, 0.22f);
            b.GetComponent<Renderer>().sharedMaterial = dark;
        }

        var lights = new List<Light>();
        foreach (var (lx, lz, col) in new[]
        {
            (-1.3f, -1.0f, new Color(1f, 0.2f, 0.15f)),
            (1.3f, -1.0f, new Color(0.2f, 1f, 0.3f)),
        })
        {
            var lgo = new GameObject("NavLight");
            lgo.transform.SetParent(root.transform, false);
            lgo.transform.localPosition = new Vector3(lx, -0.3f, lz);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col; l.range = 14f; l.intensity = 2.4f;
            l.enabled = false;
            lights.Add(l);
        }

        var col2 = root.AddComponent<BoxCollider>();
        col2.center = new Vector3(0f, 0f, 0.6f);
        col2.size = new Vector3(2.6f, 2.6f, 8f);

        var seat = new GameObject("Seat");
        seat.transform.SetParent(root.transform, false);
        seat.transform.localPosition = new Vector3(-0.5f, 0.1f, -2.2f);

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var h = root.AddComponent<G1Helicopter>();
        h.mainRotor = main.transform;
        h.tailRotor = tail.transform;
        h.seat = seat.transform;
        h.navLights = lights.ToArray();

        var wp = root.AddComponent<G1Waypoint>();
        wp.label = "HELICOPTER";
        wp.isActive = false;
    }

    static GameObject Slab(string n, GameObject p, Vector3 pos, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = n;
        go.transform.SetParent(p.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static Material Mat(Color c)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        return m;
    }
}
