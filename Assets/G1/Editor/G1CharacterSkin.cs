using UnityEditor;
using UnityEngine;

/// Dresses a character instance: the baked dirt map, plus a faction tint.
///
/// The wear lives in a Blender node network, which an FBX cannot carry, so
/// rig_character.py bakes it to `Assets/G1/Textures/&lt;Char&gt;Dirt.png` — as a
/// white-to-grime *mask*, not a finished albedo. That choice is what lets this
/// exist: the Standard shader already computes `_MainTex * _Color`, so a
/// grayscale mask multiplies cleanly against any tint. Baking the albedo would
/// have fused hazard orange into the texture and a blue security tint over it
/// would come out mud.
///
/// It also fixes a quieter bug. The builders assigned `renderer.sharedMaterial`
/// (singular), which writes slot 0 and silently drops the other eleven — so
/// only one material on the model was ever tinted and the rest kept whatever
/// the FBX shipped. This walks `sharedMaterials` and decides per slot, keyed
/// off the Blender material name that survives the export.
public static class G1CharacterSkin
{
    const string TexDir = "Assets/G1/Textures";
    const string MatDir = "Assets/G1/Materials/Skin";

    /// Every dressed material has to be an *asset* on disk.
    ///
    /// This is what the magenta NPCs were. A `new Material(...)` lives only in
    /// memory; a saved scene can serialise it inline, so the arena looked
    /// right, but `SaveAsPrefabAsset` cannot — it writes `fileID: 0` for every
    /// slot and the prefab ships with null materials. The huge map spawns its
    /// HECU from exactly those prefabs, which is why the enemies out in the
    /// districts rendered in Unity's missing-material pink while the same
    /// character in the arena looked fine.
    ///
    /// Keying the asset by character, source material and tint means two NPCs
    /// wearing the same colours share one asset instead of accumulating a file
    /// per person per rebuild.
    static Material Persist(Material m, string character, string slotName, Color suit, Color trim)
    {
        if (!AssetDatabase.IsValidFolder("Assets/G1/Materials"))
            AssetDatabase.CreateFolder("Assets/G1", "Materials");
        if (!AssetDatabase.IsValidFolder(MatDir))
            AssetDatabase.CreateFolder("Assets/G1/Materials", "Skin");

        string safe = slotName.Replace(' ', '_').Replace('/', '_');
        string key = $"{character}_{safe}_{ColorUtility.ToHtmlStringRGB(suit)}" +
                     $"{ColorUtility.ToHtmlStringRGB(trim)}";
        string path = $"{MatDir}/{key}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(m, existing);   // keep the tint current
            return existing;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    /// Materials that carry the wearer's identity and take the tint. Everything
    /// else — steel, aluminium, glass, lamps, webbing — is equipment, and
    /// equipment looks the same whoever was issued it.
    static bool IsSuit(string name) =>
        name.Contains("suit_shell") || name.Contains("suit_worn");

    static bool IsUnder(string name) =>
        name.Contains("suit_under") || name.Contains("rubber");

    public static void Apply(GameObject go, string character, Color suit, Color trim)
    {
        var dirt = AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"{TexDir}/{character}Dirt.png");
        if (dirt == null)
            Debug.LogWarning($"G1: no baked dirt map for {character} — run " +
                             "rig_character.py to bake one; falling back to flat colour.");

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var slots = r.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                var src = slots[i];
                string n = src != null ? src.name : "";
                var m = src != null ? new Material(src)
                                    : new Material(Shader.Find("Standard"));

                // keep the model's own material variety; only re-colour the
                // parts that say who this person is
                if (IsSuit(n)) m.color = suit;
                else if (IsUnder(n)) m.color = trim;

                if (dirt != null && m.HasProperty("_MainTex"))
                    m.mainTexture = dirt;

                slots[i] = Persist(m, character, string.IsNullOrEmpty(n) ? $"slot{i}" : n,
                                   suit, trim);
            }
            r.sharedMaterials = slots;      // plural: sharedMaterial drops slots 1..n
        }
        AssetDatabase.SaveAssets();
    }

    /// Recolour a whole character one flat colour — a zombie's dead green, an
    /// alien's neon violet. Same asset-backed materials as Apply, for the same
    /// reason: these characters get saved as prefabs, and a prefab cannot hold
    /// a material that isn't on disk.
    public static void Recolor(GameObject go, string variant, Color color, Color? emission = null)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var slots = r.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                var src = slots[i];
                var m = src != null ? new Material(src)
                                    : new Material(Shader.Find("Standard"));
                m.color = color;
                if (emission.HasValue)
                {
                    m.SetColor("_EmissionColor", emission.Value);
                    m.EnableKeyword("_EMISSION");
                }
                slots[i] = Persist(m, variant, src != null ? src.name : $"slot{i}",
                                   color, emission ?? Color.black);
            }
            r.sharedMaterials = slots;
        }
        AssetDatabase.SaveAssets();
    }
}
