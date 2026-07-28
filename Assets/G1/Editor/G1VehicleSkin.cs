using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Paints an imported vehicle from a palette keyed by its Blender material name.
///
/// The vehicles are modelled with nine materials each — olive body, dark trim,
/// canvas, steel, near-black tyres, glass, lamps — and in Blender every one of
/// those gets its colour from a grime node network rather than from a constant.
/// An FBX cannot carry a node network, and Blender's exporter, faced with a
/// Base Color that is *linked* rather than set, writes nothing useful. So the
/// first truck to reach the game arrived with every slot the same pale default
/// and rendered as one flat tan shape with tan tyres.
///
/// Rather than flatten the Blender materials — which would cost the wear that
/// makes them worth having in the renders — the colour is restated here, keyed
/// by the name that *does* survive the export. Nine names, one table, and the
/// two sides can be compared by eye.
///
/// Materials are written to disk for the same reason the character skins are:
/// a vehicle can end up inside a prefab, and a prefab silently drops any
/// material that is not a file.
public static class G1VehicleSkin
{
    const string MatDir = "Assets/G1/Materials/Vehicles";

    /// Base colour, smoothness, metallic — matched to build_vehicles.py's
    /// palette rather than invented, so the game and the turnaround renders
    /// show the same vehicle.
    static readonly Dictionary<string, (Color c, float smooth, float metal)> Palette =
        new Dictionary<string, (Color, float, float)>
        {
            // cargo truck
            ["veh_olive"] = (new Color(0.155f, 0.170f, 0.110f), 0.12f, 0f),
            ["veh_olive_d"] = (new Color(0.105f, 0.118f, 0.078f), 0.10f, 0f),
            ["veh_canvas"] = (new Color(0.255f, 0.240f, 0.170f), 0.03f, 0f),
            ["veh_steel"] = (new Color(0.36f, 0.37f, 0.40f), 0.58f, 0.85f),
            ["veh_dark"] = (new Color(0.045f, 0.045f, 0.050f), 0.08f, 0f),
            ["veh_tyre"] = (new Color(0.055f, 0.055f, 0.058f), 0.03f, 0f),
            ["veh_glass"] = (new Color(0.10f, 0.14f, 0.16f), 0.90f, 0.2f),
            ["veh_lamp"] = (new Color(0.95f, 0.92f, 0.75f), 0.9f, 0f),
            ["veh_rear"] = (new Color(0.60f, 0.05f, 0.04f), 0.8f, 0f),
            // APC
            ["apc_hull"] = (new Color(0.175f, 0.190f, 0.150f), 0.18f, 0f),
            ["apc_hull_d"] = (new Color(0.120f, 0.132f, 0.104f), 0.14f, 0f),
            ["apc_steel"] = (new Color(0.34f, 0.35f, 0.38f), 0.60f, 0.85f),
            ["apc_dark"] = (new Color(0.04f, 0.04f, 0.045f), 0.08f, 0f),
            ["apc_tyre"] = (new Color(0.05f, 0.05f, 0.053f), 0.03f, 0f),
            ["apc_vision"] = (new Color(0.08f, 0.12f, 0.14f), 0.92f, 0.3f),
            ["apc_lamp"] = (new Color(0.95f, 0.92f, 0.75f), 0.9f, 0f),
            // tank
            ["tk_hull"] = (new Color(0.150f, 0.162f, 0.128f), 0.16f, 0f),
            ["tk_hull_d"] = (new Color(0.100f, 0.110f, 0.086f), 0.12f, 0f),
            ["tk_steel"] = (new Color(0.32f, 0.33f, 0.36f), 0.60f, 0.85f),
            ["tk_track"] = (new Color(0.075f, 0.072f, 0.070f), 0.10f, 0.5f),
            ["tk_dark"] = (new Color(0.04f, 0.04f, 0.045f), 0.08f, 0f),
            ["tk_canvas"] = (new Color(0.24f, 0.225f, 0.160f), 0.03f, 0f),
            ["tk_vision"] = (new Color(0.08f, 0.12f, 0.14f), 0.92f, 0.3f),
            ["tk_lamp"] = (new Color(0.95f, 0.92f, 0.75f), 0.9f, 0f),
        };

    /// Names that should glow rather than merely be bright.
    static bool IsLamp(string n) => n.EndsWith("_lamp") || n == "veh_rear";

    public static int Apply(GameObject go)
    {
        if (go == null) return 0;
        EnsureFolder();
        int painted = 0;

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var slots = r.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                string n = slots[i] != null ? slots[i].name : "";
                // Unity appends a suffix when it instantiates a copy; the
                // Blender name is the part before it
                int space = n.IndexOf(' ');
                if (space > 0) n = n.Substring(0, space);

                if (!Palette.TryGetValue(n, out var spec)) continue;
                slots[i] = Get(n, spec);
                painted++;
            }
            r.sharedMaterials = slots;
        }
        if (painted > 0) AssetDatabase.SaveAssets();
        return painted;
    }

    static Material Get(string name, (Color c, float smooth, float metal) spec)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = spec.c;
        m.SetFloat("_Glossiness", spec.smooth);
        m.SetFloat("_Metallic", spec.metal);
        if (IsLamp(name))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", spec.c * 2.0f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/G1/Materials"))
            AssetDatabase.CreateFolder("Assets/G1", "Materials");
        if (!AssetDatabase.IsValidFolder(MatDir))
            AssetDatabase.CreateFolder("Assets/G1/Materials", "Vehicles");
    }
}
