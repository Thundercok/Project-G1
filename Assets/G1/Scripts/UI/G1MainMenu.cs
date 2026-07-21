using UnityEngine;
using UnityEngine.SceneManagement;

/// Retro terminal main menu: typewriter title, keyboard-navigated items,
/// ambient hum, flickering emergency light. All OnGUI (project convention).
public sealed class G1MainMenu : MonoBehaviour
{
    public Light flickerLight;

    static readonly Color Teal = new Color(0.16f, 0.75f, 0.75f);
    static readonly Color Dim = new Color(0.29f, 0.29f, 0.29f);

    string[] items;          // built at Start (Continue appears only with a save)
    int selected;
    float startTime;
    Font font;
    G1SettingsPanel settings;
    AudioSource hum;

    void Start()
    {
        startTime = Time.time;
        items = G1SaveSystem.HasSave
            ? new[] { "[ CONTINUE ]", "[ NEW GAME ]", "[ SETTINGS ]", "[ QUIT ]" }
            : new[] { "[ NEW GAME ]", "[ SETTINGS ]", "[ QUIT ]" };
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        settings = GetComponent<G1SettingsPanel>();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioListener.volume = PlayerPrefs.GetFloat("G1_MasterVolume", 0.8f);

        hum = gameObject.AddComponent<AudioSource>();
        hum.clip = Resources.Load<AudioClip>("Audio/ambient_hum");
        hum.loop = true;
        hum.volume = 0.3f;
        if (hum.clip)
            hum.Play();
    }

    void Update()
    {
        if (flickerLight)
            flickerLight.intensity = Random.value < 0.06f
                ? Random.Range(0.2f, 0.7f) : 1.4f;

        if (settings != null && settings.visible)
            return;
        if (Input.GetKeyDown(KeyCode.DownArrow))
            selected = (selected + 1) % items.Length;
        if (Input.GetKeyDown(KeyCode.UpArrow))
            selected = (selected + items.Length - 1) % items.Length;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Activate(items[selected]);
    }

    void Activate(string item)
    {
        switch (item)
        {
            case "[ CONTINUE ]": G1SaveSystem.Continue(); break;
            case "[ NEW GAME ]": SceneManager.LoadScene("TestScene"); break;
            case "[ SETTINGS ]": if (settings) settings.visible = true; break;
            case "[ QUIT ]": Application.Quit(); break;
        }
    }

    void OnGUI()
    {
        GUI.color = new Color(0.04f, 0.05f, 0.06f, 1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (settings != null && settings.visible)
            return;

        float cx = Screen.width / 2f;
        var title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48, alignment = TextAnchor.MiddleCenter,
        };
        if (font) title.font = font;
        title.normal.textColor = Teal;

        // typewriter reveal, 40 ms per character
        const string full = "PROJECT G1";
        int chars = Mathf.Min(full.Length, (int)((Time.time - startTime) / 0.04f));
        GUI.Label(new Rect(cx - 300, Screen.height * 0.25f, 600, 70),
                  full.Substring(0, chars), title);

        var item = new GUIStyle(title) { fontSize = 26 };
        for (int i = 0; i < items.Length; i++)
        {
            item.normal.textColor = i == selected ? Teal : Dim;
            var r = new Rect(cx - 200, Screen.height * 0.45f + i * 52, 400, 44);
            GUI.Label(r, items[i], item);
            if (r.Contains(Event.current.mousePosition))
            {
                selected = i;
                if (Event.current.type == EventType.MouseDown)
                    Activate(items[i]);
            }
        }
    }
}
