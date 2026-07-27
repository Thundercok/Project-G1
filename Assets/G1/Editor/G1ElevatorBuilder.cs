using UnityEditor;
using UnityEngine;

/// Puts lifts on the verticality the map already has.
///
/// The Sprawl is full of high ground — tower decks, warehouse roofs, the
/// command tower — and almost all of it is served by exactly one ramp, if
/// that. The command tower in particular has had the Auditor standing on top
/// of it since the map was built, visible from the whole plaza and reachable
/// by nobody. A lift turns each of those from a puzzle into a destination.
public static class G1ElevatorBuilder
{
    [MenuItem("G1/Build Elevators")]
    public static void BuildStandalone()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building elevators.");
            return;
        }
        Build();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    public static int Build()
    {
        int n = 0;
        // the one that matters: the plaza floor to the Auditor's perch
        n += Lift("Lift_CommandTower", new Vector3(11.5f, 0f, 0f), 0.2f, 38f, "TOWER LIFT") ? 1 : 0;
        // warehouse and hangar roofs, both of which overlook a fight
        n += Lift("Lift_Warehouse", new Vector3(184f, 0f, -176f), 0.2f, 8.0f, "ROOF LIFT") ? 1 : 0;
        n += Lift("Lift_Hangar", new Vector3(196f, 0f, -18f), 0.2f, 16.5f, "HANGAR LIFT") ? 1 : 0;
        // the lab block's upper floor, which the ramp only half serves
        n += Lift("Lift_Labs", new Vector3(-44f, 0f, -178f), 0.2f, 10.8f, "LAB LIFT") ? 1 : 0;
        Debug.Log($"G1: {n} elevators built — stand on one and press E.");
        return n;
    }

    static bool Lift(string name, Vector3 at, float bottom, float top, string label)
    {
        // a lift buried in a wall is worse than a missing lift
        at = G1Placement.FindClearFootprint(at, new Vector2(2.6f, 2.6f), name, searchRadius: 22f);

        var steel = Mat(new Color(0.34f, 0.36f, 0.38f));
        var trim = Mat(new Color(0.85f, 0.62f, 0.08f));

        var root = new GameObject(name);
        root.transform.position = new Vector3(at.x, 0f, at.z);

        // a mast so the shaft reads from the ground — you should be able to see
        // that there is a way up before you are standing on it
        var mast = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(mast.GetComponent<Collider>());
        mast.name = "Mast";
        mast.transform.SetParent(root.transform, false);
        mast.transform.localPosition = new Vector3(0f, (top + 1f) * 0.5f, -1.6f);
        mast.transform.localScale = new Vector3(0.5f, top + 1f, 0.5f);
        mast.GetComponent<Renderer>().sharedMaterial = steel;

        var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.name = "Platform";
        deck.transform.SetParent(root.transform, false);
        deck.transform.localPosition = new Vector3(0f, bottom, 0f);
        deck.transform.localScale = new Vector3(3.2f, 0.3f, 3.2f);
        deck.GetComponent<Renderer>().sharedMaterial = steel;

        foreach (var (dx, dz) in new[] { (-1.5f, 0f), (1.5f, 0f), (0f, 1.5f) })
        {
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(rail.GetComponent<Collider>());
            rail.name = "Rail";
            rail.transform.SetParent(deck.transform, false);
            rail.transform.localPosition = new Vector3(dx / 3.2f, 2.0f, dz / 3.2f);
            rail.transform.localScale = new Vector3(
                dz == 0f ? 0.06f : 1f, 3.5f, dz == 0f ? 1f : 0.06f);
            rail.GetComponent<Renderer>().sharedMaterial = trim;
        }

        var lampGo = new GameObject("CallLamp");
        lampGo.transform.SetParent(root.transform, false);
        lampGo.transform.localPosition = new Vector3(0f, 2.4f, -1.6f);
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 10f; lamp.intensity = 2.2f;

        var lift = root.AddComponent<G1Elevator>();
        lift.platform = deck.transform;
        lift.bottomY = bottom;
        lift.topY = top;
        lift.statusLight = lamp;
        lift.label = label;

        var wp = root.AddComponent<G1Waypoint>();
        wp.label = label;
        wp.isActive = false;
        return true;
    }

    static Material Mat(Color c)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        return m;
    }
}
