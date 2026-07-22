using UnityEngine;

/// Drop-in world-space graffiti: a spray-painted message from a previous loop,
/// rendered as a 3D TextMesh so it sits on a wall in the level. Set `text`
/// directly, or leave it empty and set `tier` to auto-pull the next line from
/// G1LoreText. Add it to an empty GameObject, position/rotate it against a wall,
/// and it just works.
public sealed class G1Graffiti : MonoBehaviour
{
    [Tooltip("Explicit message. If empty, a line is pulled from G1LoreText by tier.")]
    public string text = "";
    [Tooltip("1 = ambient vandalism, 2 = uneasy, 3 = it tells you how to win.")]
    [Range(1, 3)] public int tier = 1;
    public Color color = new Color(0.85f, 0.12f, 0.1f);   // spray-paint red
    public float characterSize = 0.14f;
    public int fontSize = 64;

    void Start()
    {
        if (string.IsNullOrEmpty(text))
            text = G1LoreText.NextGraffiti(tier);

        var tm = gameObject.AddComponent<TextMesh>();
        tm.text = text;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = characterSize;
        tm.fontSize = fontSize;
        tm.color = color;

        var font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        if (font != null)
        {
            tm.font = font;
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = font.material;
        }

        // Slight lift off the wall to avoid z-fighting with the surface behind it.
        transform.position += transform.forward * -0.02f;
    }
}
