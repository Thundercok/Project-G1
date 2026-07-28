using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Puts photographed surfaces on the map's flat colours.
///
/// Both levels were built from materials that are a single RGB value and
/// nothing else. That is why the base reads as cardboard however much geometry
/// goes into it: a real concrete wall is not grey, it is a thousand slightly
/// different greys with a direction to them, and no amount of extra boxes
/// substitutes for that. Twelve CC0 textures from Poly Haven cover every
/// structural surface on the map, and they matter more than any model swap.
///
/// Two decisions worth stating:
///
/// The texture *multiplies* the material's existing colour rather than
/// replacing it. The districts were deliberately given separate hues so a
/// player can tell where they are from the nearest wall, and throwing that away
/// for photographic accuracy would trade a navigation aid for a screenshot.
/// Concrete in the barracks is tan concrete; concrete in the lab is white
/// concrete; it is the same photograph underneath.
///
/// Tiling is 1. The Blender generators cube-project every chunk at a fixed two
/// metres per UV unit, so a texture is already at world scale everywhere and a
/// tiling factor here would only undo that.
public static class G1MapSkin
{
    const string Tex = "Assets/G1/External/PolyHavenTextures";
    const string MatDir = "Assets/G1/Materials/Map";

    /// Which photograph goes on which of the generators' material names. Both
    /// palettes are keyed the same way (`map_*` for the Sprawl, `cs_*` for
    /// Cradle Station), so one table serves both maps.
    static readonly (string suffix, string tex, float smooth)[] Table =
    {
        ("_ground", "gravel_floor", 0.05f),
        ("_road", "asphalt_02", 0.10f),
        ("_asphalt", "asphalt_02", 0.10f),
        ("_concrete_d", "concrete_wall_008", 0.06f),
        ("_concrete", "concrete_wall_008", 0.08f),
        ("_clean_floor", "floor_tiles_02", 0.35f),
        ("_lab_white", "anti_slip_concrete", 0.20f),
        ("_lab_trim", "anti_slip_concrete", 0.22f),
        ("_lab", "anti_slip_concrete", 0.20f),
        ("_steel_pale", "metal_plate", 0.55f),
        ("_metal", "metal_plate", 0.45f),
        ("_rust", "green_metal_rust", 0.12f),
        ("_cont_a", "container_side", 0.25f),
        ("_cont_b", "container_side", 0.25f),
        ("_container_a", "container_side", 0.25f),
        ("_container_b", "container_side", 0.25f),
        ("_quarter_brick", "brick_4", 0.06f),
        ("_barrack", "concrete_wall_008", 0.08f),
        ("_depot", "corrugated_iron", 0.30f),
        ("_motor", "corrugated_iron", 0.30f),
        ("_hq", "concrete_wall_008", 0.10f),
        ("_power", "corrugated_iron", 0.28f),
        ("_robot", "anti_slip_concrete", 0.18f),
        ("_wood", "wood_planks_dirt", 0.08f),
        ("_sand", "sandy_gravel", 0.04f),
        ("_earth", "gravel_floor", 0.04f),
        ("_olive", "corrugated_iron", 0.15f),
        ("_air_grey", "corrugated_iron", 0.25f),
        ("_med_white", "anti_slip_concrete", 0.20f),
    };

    /// Left flat on purpose: paint stripes, hazard chevrons, glass, every
    /// emissive lamp and signal, and the ground grime decals. A warning stripe
    /// with concrete grain on it stops reading as a warning stripe, and putting
    /// a photograph behind an emissive colour just dirties the light.
    static bool SkipFlat(string n) =>
        n.Contains("paint") || n.Contains("warn") || n.Contains("hazard") ||
        n.Contains("glass") || n.Contains("lamp") || n.Contains("signal") ||
        n.Contains("screen") || n.Contains("alien") || n.Contains("parasite") ||
        n.Contains("oil") || n.Contains("scorch") || n.Contains("tracks") ||
        n.Contains("spill") || n.Contains("canvas") || n.Contains("allied");

    public static int Apply(GameObject map)
    {
        if (map == null) return 0;
        EnsureFolder();
        var done = new Dictionary<string, Material>();
        int painted = 0;

        foreach (var r in map.GetComponentsInChildren<Renderer>())
        {
            var slots = r.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                var src = slots[i];
                if (src == null) continue;
                string n = src.name;
                int space = n.IndexOf(' ');
                if (space > 0) n = n.Substring(0, space);
                if (SkipFlat(n)) continue;

                string pick = null;
                float smooth = 0.1f;
                foreach (var t in Table)
                    if (n.EndsWith(t.suffix)) { pick = t.tex; smooth = t.smooth; break; }
                if (pick == null) continue;

                if (!done.TryGetValue(n, out var m))
                {
                    m = Build(n, pick, smooth, src.color);
                    done[n] = m;
                }
                slots[i] = m;
                painted++;
            }
            r.sharedMaterials = slots;
        }
        if (painted > 0) AssetDatabase.SaveAssets();
        Debug.Log($"G1: textured {done.Count} map materials across {painted} slots.");
        return done.Count;
    }

    static Material Build(string name, string tex, float smooth, Color tint)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }

        var diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/{tex}/{tex}_Diffuse.jpg");
        var nor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/{tex}/{tex}_nor_gl.jpg");
        if (diff == null)
        {
            Debug.LogWarning($"G1: no texture at {Tex}/{tex} — {name} stays flat.");
            return m;
        }

        m.mainTexture = diff;
        m.mainTextureScale = Vector2.one;      // the UVs are already in metres
        // Brighten the tint toward white: the photograph carries the value now,
        // so multiplying by the original dark colour would leave every wall
        // nearly black. Half way keeps the district hue and lets the surface
        // through.
        m.color = Color.Lerp(tint, Color.white, 0.55f);
        m.SetFloat("_Glossiness", smooth);
        if (nor != null)
        {
            SetNormal(nor);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_BumpMap", nor);
            m.SetFloat("_BumpScale", 1f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    /// A normal map imported as a colour texture is worse than none: Unity
    /// reads it through the wrong decode and every surface gets lit from a
    /// direction that does not exist.
    static void SetNormal(Texture2D t)
    {
        string p = AssetDatabase.GetAssetPath(t);
        var imp = AssetImporter.GetAtPath(p) as TextureImporter;
        if (imp == null || imp.textureType == TextureImporterType.NormalMap) return;
        imp.textureType = TextureImporterType.NormalMap;
        imp.SaveAndReimport();
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/G1/Materials"))
            AssetDatabase.CreateFolder("Assets/G1", "Materials");
        if (!AssetDatabase.IsValidFolder(MatDir))
            AssetDatabase.CreateFolder("Assets/G1/Materials", "Map");
    }
}
