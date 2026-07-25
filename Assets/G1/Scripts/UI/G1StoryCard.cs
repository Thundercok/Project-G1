using UnityEngine;

/// HL1-style chapter title card: typewriter reveal, holds, fades.
/// showOnStart=true → plays on level start; false → plays when the player
/// enters a trigger collider on the same GameObject.
public sealed class G1StoryCard : MonoBehaviour
{
    public string title = "CHAPTER ONE";
    public string subtitle = "COLD START";
    public bool showOnStart = true;
    public float holdTime = 4.5f;

    float shownAt = -1f;
    bool played;
    Font font;
    Texture2D _pixelTex;

    void Start()
    {
        _pixelTex = Texture2D.whiteTexture;
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        if (showOnStart)
            Show();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!showOnStart && !played && other.CompareTag("Player"))
            Show();
    }

    public void Show()
    {
        played = true;
        shownAt = Time.time;
    }

    void OnGUI()
    {
        if (shownAt < 0f)
            return;
        float t = Time.time - shownAt;
        float alpha = t < 0.4f ? t / 0.4f
                    : t > holdTime ? Mathf.Max(0f, 1f - (t - holdTime) / 1f)
                    : 1f;
        if (alpha <= 0f)
        {
            shownAt = -1f;
            return;
        }

        var teal = new Color(0.16f, 0.75f, 0.75f, alpha);
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34, alignment = TextAnchor.MiddleLeft,
        };
        if (font) style.font = font;
        style.normal.textColor = teal;

        int chars = Mathf.Min(title.Length, (int)(t / 0.05f));
        float y = Screen.height * 0.72f;

        // Dark gradient background behind text for legibility
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
        GUI.DrawTexture(new Rect(0, y - 10, Screen.width * 0.55f, 90), _pixelTex);
        GUI.color = old;

        GUI.Label(new Rect(60, y, 800, 44), title.Substring(0, chars), style);

        // Teal glow bar under chapter title
        old = GUI.color;
        float barProgress = Mathf.Clamp01(t / 1.5f);
        GUI.color = new Color(0.16f, 0.75f, 0.75f, 0.7f * alpha);
        GUI.DrawTexture(new Rect(60, y + 42, 300 * barProgress, 2f), _pixelTex);
        GUI.color = old;

        var sub = new GUIStyle(style) { fontSize = 20 };
        sub.normal.textColor = new Color(0.83f, 0.85f, 0.86f, alpha * 0.9f);
        int subChars = Mathf.Clamp((int)((t - 0.8f) / 0.04f), 0, subtitle.Length);
        GUI.Label(new Rect(60, y + 46, 800, 30),
                  subtitle.Substring(0, subChars), sub);
    }
}
