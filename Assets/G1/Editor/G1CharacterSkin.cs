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

                slots[i] = m;
            }
            r.sharedMaterials = slots;      // plural: sharedMaterial drops slots 1..n
        }
    }
}
