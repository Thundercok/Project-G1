using UnityEngine;

/// Retro settings panel: sensitivity / FOV / master volume, PlayerPrefs-backed.
/// Read by MouseLook (sensitivity) and G1SettingsApplier (FOV, volume).
public sealed class G1SettingsPanel : MonoBehaviour
{
    public bool visible;

    static readonly Color Teal = new Color(0.16f, 0.75f, 0.75f);
    Font font;

    float sens, fov, vol;

    void Start()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        sens = PlayerPrefs.GetFloat("G1_Sensitivity", 2.2f);
        fov = PlayerPrefs.GetFloat("G1_FOV", 75f);
        vol = PlayerPrefs.GetFloat("G1_MasterVolume", 0.8f);
    }

    void OnGUI()
    {
        if (!visible)
            return;
        float cx = Screen.width / 2f;
        float y = Screen.height * 0.3f;
        var label = new GUIStyle(GUI.skin.label) { fontSize = 20 };
        if (font) label.font = font;
        label.normal.textColor = Teal;

        GUI.Label(new Rect(cx - 220, y - 60, 440, 34), "== SETTINGS ==", label);

        GUI.Label(new Rect(cx - 220, y, 260, 28),
                  $"MOUSE SENS   {sens:0.0}", label);
        sens = GUI.HorizontalSlider(new Rect(cx + 60, y + 8, 160, 20),
                                    sens, 0.1f, 5f);

        GUI.Label(new Rect(cx - 220, y + 44, 260, 28),
                  $"FOV          {fov:0}", label);
        fov = GUI.HorizontalSlider(new Rect(cx + 60, y + 52, 160, 20),
                                   fov, 70f, 110f);

        GUI.Label(new Rect(cx - 220, y + 88, 260, 28),
                  $"VOLUME       {vol:0.0}", label);
        vol = GUI.HorizontalSlider(new Rect(cx + 60, y + 96, 160, 20),
                                   vol, 0f, 1f);

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
