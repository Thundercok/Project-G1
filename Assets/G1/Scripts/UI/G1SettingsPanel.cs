using UnityEngine;

/// Retro settings panel: sensitivity / FOV / master volume, PlayerPrefs-backed.
/// Read by MouseLook (sensitivity) and G1SettingsApplier (FOV, volume).
public sealed class G1SettingsPanel : MonoBehaviour
{
    public bool visible;

    static readonly Color Teal = new Color(0.16f, 0.75f, 0.75f);
    Font font;

    float sens, fov, vol;
    Texture2D _pixelTex;

    void Start()
    {
        _pixelTex = Texture2D.whiteTexture;
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        sens = PlayerPrefs.GetFloat("G1_Sensitivity", 2.2f);
        fov = PlayerPrefs.GetFloat("G1_FOV", 75f);
        vol = PlayerPrefs.GetFloat("G1_MasterVolume", 0.8f);
    }

    void DrawModernPanel(Rect r, Color fill, Color accent, float accentWidth = 2f)
    {
        Texture2D t = _pixelTex;
        Color old = GUI.color;
        // Outer shadow
        GUI.color = new Color(0f, 0f, 0f, fill.a * 0.3f);
        GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), t);
        // Main fill
        GUI.color = fill;
        GUI.DrawTexture(r, t);
        // Bottom gradient strip for depth
        GUI.color = new Color(fill.r + 0.03f, fill.g + 0.04f, fill.b + 0.05f, fill.a * 0.5f);
        GUI.DrawTexture(new Rect(r.x, r.yMax - r.height * 0.25f, r.width, r.height * 0.25f), t);
        // Left accent bar
        GUI.color = accent;
        GUI.DrawTexture(new Rect(r.x, r.y, accentWidth, r.height), t);
        // Top highlight
        GUI.color = new Color(1f, 1f, 1f, 0.04f);
        GUI.DrawTexture(new Rect(r.x + accentWidth, r.y, r.width - accentWidth, 1f), t);
        GUI.color = old;
    }

    void DrawHLine(float x, float y, float width, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, width, 1f), _pixelTex);
        GUI.color = old;
    }

    void OnGUI()
    {
        if (!visible)
            return;
        float panelW = 500f;
        float panelH = 300f;
        float panelX = Screen.width / 2f - panelW / 2f;
        float panelY = Screen.height * 0.3f - 80f;
        DrawModernPanel(new Rect(panelX, panelY, panelW, panelH),
                        new Color(0.03f, 0.05f, 0.07f, 0.88f),
                        new Color(0.16f, 0.75f, 0.75f, 0.6f), 3f);

        float cx = Screen.width / 2f;
        float y = Screen.height * 0.3f;
        var label = new GUIStyle(GUI.skin.label) { fontSize = 20 };
        if (font) label.font = font;
        label.normal.textColor = Teal;

        var valLabel = new GUIStyle(label) { fontSize = 14 };

        GUI.Label(new Rect(cx - 220, y - 60, 440, 34), "──── SETTINGS ────", label);

        GUI.Label(new Rect(cx - 220, y, 260, 28), "MOUSE SENS", label);
        sens = GUI.HorizontalSlider(new Rect(cx + 60, y + 8, 160, 20), sens, 0.1f, 5f);
        GUI.Label(new Rect(cx + 230, y + 6, 60, 20), $"{sens:0.0}", valLabel);
        DrawHLine(cx - 220, y + 38, 440, new Color(0.16f, 0.75f, 0.75f, 0.12f));

        GUI.Label(new Rect(cx - 220, y + 44, 260, 28), "FOV", label);
        fov = GUI.HorizontalSlider(new Rect(cx + 60, y + 52, 160, 20), fov, 70f, 110f);
        GUI.Label(new Rect(cx + 230, y + 50, 60, 20), $"{fov:0}", valLabel);
        DrawHLine(cx - 220, y + 82, 440, new Color(0.16f, 0.75f, 0.75f, 0.12f));

        GUI.Label(new Rect(cx - 220, y + 88, 260, 28), "VOLUME", label);
        vol = GUI.HorizontalSlider(new Rect(cx + 60, y + 96, 160, 20), vol, 0f, 1f);
        GUI.Label(new Rect(cx + 230, y + 94, 60, 20), $"{vol * 100f:0}%", valLabel);
        DrawHLine(cx - 220, y + 126, 440, new Color(0.16f, 0.75f, 0.75f, 0.12f));

        int curDiff = PlayerPrefs.GetInt("G1_Difficulty", 0);
        var diffRect = new Rect(cx - 220, y + 132, 440, 28);
        GUI.Label(diffRect, $"DIFFICULTY   < {G1Difficulty.Name} >", label);
        if (diffRect.Contains(Event.current.mousePosition)
            && Event.current.type == EventType.MouseDown)
        {
            int nextDiff = (curDiff + 1) % 3;
            PlayerPrefs.SetInt("G1_Difficulty", nextDiff);
            G1Audio.Play2D("pickup", 0.6f);
        }

        var back = new GUIStyle(label) { fontSize = 24 };
        var r = new Rect(cx - 80, y + 190, 160, 40);
        GUI.Label(r, "[ BACK ]", back);
        bool clickBack = r.Contains(Event.current.mousePosition)
                         && Event.current.type == EventType.MouseDown;
        if (clickBack || Input.GetKeyDown(KeyCode.Escape))
        {
            PlayerPrefs.SetFloat("G1_Sensitivity", sens);
            PlayerPrefs.SetFloat("G1_FOV", fov);
            PlayerPrefs.SetFloat("G1_MasterVolume", vol);
            PlayerPrefs.Save();
            AudioListener.volume = vol;
            visible = false;
        }
        AudioListener.volume = vol;      // live preview while sliding
    }
}
