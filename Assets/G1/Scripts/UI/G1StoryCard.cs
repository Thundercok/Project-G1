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

    void Start()
    {
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
        GUI.Label(new Rect(60, y, 800, 44), title.Substring(0, chars), style);

        var sub = new GUIStyle(style) { fontSize = 20 };
        sub.normal.textColor = new Color(0.83f, 0.85f, 0.86f, alpha * 0.9f);
        int subChars = Mathf.Clamp((int)((t - 0.8f) / 0.04f), 0, subtitle.Length);
        GUI.Label(new Rect(60, y + 46, 800, 30),
                  subtitle.Substring(0, subChars), sub);
    }
}
