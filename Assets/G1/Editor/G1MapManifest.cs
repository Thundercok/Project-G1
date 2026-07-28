using System.IO;
using UnityEditor;
using UnityEngine;

/// Reads the JSON manifest the Blender map script writes next to HugeMap.fbx.
///
/// The FBX is geometry and nothing else — Unity has no way to tell that a
/// particular hollow arrangement of boxes is a room you can walk into. Rather
/// than re-typing fifty interiors' coordinates on this side (and letting the
/// two copies drift the first time a wall moves), the generator exports what
/// it built and this reads it back.
public static class G1MapManifest
{
    [System.Serializable]
    public class Room
    {
        public string name;
        public float x, z, y;       // y is the floor height, z the Unity Z
        public float w, d, h;
        public string doors;
        public bool light;
    }

    [System.Serializable]
    public class Lamp
    {
        public float x, z, y;
        public float range, intensity;
        public bool spot;
        public float[] color;
    }

    [System.Serializable]
    public class Spot
    {
        public float x, z, y;
    }

    /// A piece of interactive equipment the generator placed.
    ///
    /// The room list exists so nobody re-types an interior's coordinates on the
    /// Unity side. This is the same argument one level down: the script that
    /// built a shutter-shaped hole in a wall is the only thing that knows where
    /// the shutter goes, so it says so, and the builder attaches the component
    /// rather than guessing.
    ///
    /// `tag` carries whatever the kind needs — a lock group for doors and
    /// readers, a comma-separated list of stop heights for a lift.
    [System.Serializable]
    public class Device
    {
        public string kind;
        public float x, z, y;
        public float yaw;
        public string tag;
    }

    [System.Serializable]
    public class Data
    {
        public float half;
        public Room[] rooms;
        public Lamp[] lights;
        public Spot[] cover;
        public Device[] devices;
    }

    public static Data Load(string fbxPath)
    {
        string path = Path.ChangeExtension(fbxPath, null) + ".manifest.json";
        if (!File.Exists(path))
        {
            Debug.LogWarning($"G1: no map manifest at {path} — interiors will be unlit. " +
                             "Re-run Tools/blender/build_huge_map.py.");
            return null;
        }
        var data = JsonUtility.FromJson<Data>(File.ReadAllText(path));
        if (data == null || data.rooms == null)
        {
            Debug.LogWarning($"G1: map manifest at {path} did not parse.");
            return null;
        }
        return data;
    }

    /// Lights every interior and raises the map's floodlights. Returns how many
    /// lights it placed.
    public static int ApplyLighting(Data data, Vector3 offset = default)
    {
        if (data == null) return 0;
        var root = new GameObject("MapLighting");
        int n = 0;

        foreach (var r in data.rooms)
        {
            if (r == null || !r.light) continue;
            var go = new GameObject("Lamp_" + r.name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = offset + new Vector3(r.x, r.y + Mathf.Max(1.4f, r.h - 0.7f), r.z);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.83f, 0.58f);   // sodium, not daylight
            // reach the far corner of the room and a little way out of the door,
            // so a doorway reads as lit from outside and is worth walking to
            l.range = Mathf.Max(r.w, r.d) * 0.95f + 3f;
            l.intensity = 2.6f;   // the world outside got dark
            l.shadows = LightShadows.None;    // dozens of shadowed points would
            n++;                              // cost more than the rooms are worth
        }

        foreach (var f in data.lights ?? new Lamp[0])
        {
            var go = new GameObject("Flood");
            go.transform.SetParent(root.transform, false);
            go.transform.position = offset + new Vector3(f.x, f.y, f.z);
            var l = go.AddComponent<Light>();
            l.type = f.spot ? LightType.Spot : LightType.Point;
            if (f.spot)
            {
                go.transform.rotation = Quaternion.Euler(72f, 0f, 0f);
                l.spotAngle = 96f;
            }
            l.color = f.color != null && f.color.Length >= 3
                ? new Color(f.color[0], f.color[1], f.color[2])
                : Color.white;
            l.range = f.range;
            l.intensity = f.intensity;
            l.shadows = LightShadows.None;
            n++;
        }
        return n;
    }

    /// Plants the firing positions the map was built around. The generator
    /// emits these from the barrier geometry itself — behind sandbag lines, on
    /// trench fire steps, at pillbox slits, behind tower parapets — so the AI
    /// is using the cover the level designer built rather than whatever a grid
    /// sampler happened to find.
    public static int ApplyCover(Data data, Vector3 offset = default)
    {
        if (data == null || data.cover == null) return 0;
        var root = new GameObject("CoverPoints");
        int n = 0;
        foreach (var c in data.cover)
        {
            if (c == null) continue;
            var go = new GameObject("Cover");
            go.transform.SetParent(root.transform, false);
            go.transform.position = offset + new Vector3(c.x, c.y, c.z);
            go.AddComponent<G1CoverPoint>();
            n++;
        }
        return n;
    }

    /// Drops any cover point you couldn't actually stand at or path to. Has to
    /// run after the NavMesh bake, and it matters: a fighter that claims an
    /// unreachable point walks at it for the rest of the fight and never
    /// shoots, which looks exactly like broken AI.
    public static int PruneCover()
    {
        int removed = 0;
        foreach (var cp in Object.FindObjectsOfType<G1CoverPoint>())
        {
            var at = cp.transform.position;
            bool clear = !Physics.CheckCapsule(at + Vector3.up * 0.4f, at + Vector3.up * 1.6f,
                                               0.35f, ~0, QueryTriggerInteraction.Ignore);
            bool onMesh = UnityEngine.AI.NavMesh.SamplePosition(
                at, out _, 2.0f, UnityEngine.AI.NavMesh.AllAreas);
            if (clear && onMesh) continue;
            Object.DestroyImmediate(cp.gameObject);
            removed++;
        }
        return removed;
    }

    /// Add a second map's rooms to whatever the player already carries, rather
    /// than replacing them. Building one world out of two maps means the same
    /// G1InteriorSpace has to know about both, and the obvious call — the one
    /// that sets `space.rooms` — would leave the player deaf indoors on
    /// whichever map was applied first.
    public static int AppendInteriorSpaces(Data data, Vector3 offset)
    {
        var player = GameObject.FindWithTag("Player");
        if (data == null || player == null) return 0;
        var space = player.GetComponent<G1InteriorSpace>();
        if (space == null) space = player.AddComponent<G1InteriorSpace>();

        var list = new System.Collections.Generic.List<G1InteriorSpace.Room>(
            space.rooms ?? new G1InteriorSpace.Room[0]);
        int added = 0;
        foreach (var r in data.rooms)
        {
            if (r == null) continue;
            float w = Mathf.Max(1f, r.w - 2f);
            float d = Mathf.Max(1f, r.d - 2f);
            list.Add(new G1InteriorSpace.Room
            {
                name = r.name,
                bounds = new Bounds(offset + new Vector3(r.x, r.y + r.h * 0.5f, r.z),
                                    new Vector3(w, r.h, d)),
                size = Mathf.Max(r.w, r.d),
            });
            added++;
        }
        space.rooms = list.ToArray();
        return added;
    }

    /// Hands the room list to the runtime so the game can tell indoors from
    /// outdoors without raycasting for a ceiling — a catwalk or a berm overhang
    /// fools a raycast, and the manifest already knows the real boxes.
    public static int ApplyInteriorSpaces(Data data)
    {
        if (data == null) return 0;
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("G1: no Player in scene — interior audio not installed.");
            return 0;
        }

        var list = new System.Collections.Generic.List<G1InteriorSpace.Room>();
        foreach (var r in data.rooms)
        {
            if (r == null) continue;
            // inset past the walls so the transition lands when you are properly
            // through the doorway rather than while still standing in it
            float w = Mathf.Max(1f, r.w - 2f);
            float d = Mathf.Max(1f, r.d - 2f);
            list.Add(new G1InteriorSpace.Room
            {
                name = r.name,
                bounds = new Bounds(new Vector3(r.x, r.y + r.h * 0.5f, r.z),
                                    new Vector3(w, r.h, d)),
                size = Mathf.Max(r.w, r.d),
            });
        }

        var space = player.GetComponent<G1InteriorSpace>();
        if (space == null) space = player.AddComponent<G1InteriorSpace>();
        space.rooms = list.ToArray();
        return list.Count;
    }

    /// Puts something worth finding inside a share of the interiors. A room the
    /// player can enter but that holds nothing teaches them not to enter the
    /// next one, so roughly every third interior pays out.
    public static int StockInteriors(Data data, Vector3 offset = default)
    {
        if (data == null) return 0;
        var rng = new System.Random(20601);      // fixed: builds must be repeatable
        int n = 0;
        foreach (var r in data.rooms)
        {
            if (r == null) continue;
            if (r.w < 8f || r.d < 8f) continue;          // sentry boxes stay empty
            if (rng.Next(100) >= 38) continue;

            // stacked against the back wall, not dumped in the middle of the
            // floor — a crate on the centreline blocks the walk-through line
            // from one doorway to the other, which is the whole point of a room
            var at = offset + new Vector3(r.x, r.y + 0.5f, r.z + r.d / 2f - 1.6f);
            int roll = rng.Next(100);
            if (roll < 40)
            {
                G1AmmoPack.Create(at + new Vector3(-1.2f, 0f, 0f));
                G1HealthPack.Create(at + new Vector3(1.2f, 0f, 0f));
            }
            else if (roll < 72)
            {
                G1ArmorPack.Create(at);
                G1AmmoPack.Create(at + new Vector3(1.6f, 0f, -0.6f));
            }
            else
            {
                G1WallCharger.Create(offset + new Vector3(r.x, r.y + 1.2f, r.z + r.d / 2f - 0.9f));
                G1HealthPack.Create(at + new Vector3(-1.6f, 0f, 0f));
            }
            n++;
        }
        return n;
    }
}
