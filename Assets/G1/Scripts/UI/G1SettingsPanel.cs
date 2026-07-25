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

    void DrawPanel(Rect r, Color fill, Color border, float borderWidth = 1f)
    {
        Color old = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(r, _pixelTex);
        GUI.color = border;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, borderWidth), _pixelTex);
        GUI.DrawTexture(new Rect(r.x, r.yMax - borderWidth, r.width, borderWidth), _pixelTex);
        GUI.DrawTexture(new Rect(r.x, r.y, borderWidth, r.height), _pixelTex);
        GUI.DrawTexture(new Rect(r.xMax - borderWidth, r.y, borderWidth, r.height), _pixelTex);
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
        DrawPanel(new Rect(panelX, panelY, panelW, panelH),
                  new Color(0.03f, 0.05f, 0.07f, 0.88f),
                  new Color(0.16f, 0.75f, 0.75f, 0.4f));

        float cx = Screen.width / 2f;
        float y = Screen.height * 0.3f;
        var label = new GUIStyle(GUI.skin.label) { fontSize = 20 };
        if (font) label.font = font;
        label.normal.textColor = Teal;

        GUI.Label(new Rect(cx - 220, y - 60, 440, 34), "──── SETTINGS ────", label);

        GUI.Label(new Rect(cx - 220, y, 260, 28),
                  $"MOUSE SENS   {sens:0.0}", label);
        sens = GUI.HorizontalSlider(new Rect(cx + 60, y + 8, 160, 20),
                                    sens, 0.1f, 5f);
        DrawHLine(cx - 220, y + 38, 440, new Color(0.16f, 0.75f, 0.75f, 0.12f));

        GUI.Label(new Rect(cx - 220, y + 44, 260, 28),
                  $"FOV          {fov:0}", label);
        fov = GUI.HorizontalSlider(new Rect(cx + 60, y + 52, 160, 20),
                                   fov, 70f, 110f);
        DrawHLine(cx - 220, y + 82, 440, new Color(0.16f, 0.75f, 0.75f, 0.12f));

        GUI.Label(new Rect(cx - 220, y + 88, 260, 28),
                  $"VOLUME       {vol:0.0}", label);
        vol = GUI.HorizontalSlider(new Rect(cx + 60, y + 96, 160, 20),
                                   vol, 0f, 1f);
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
