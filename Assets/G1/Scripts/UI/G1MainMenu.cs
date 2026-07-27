using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Retro terminal main menu: typewriter title, keyboard-navigated items,
/// campaign continue & level select, ambient hum, flickering emergency light. All OnGUI (project convention).
public sealed class G1MainMenu : MonoBehaviour
{
    public Light flickerLight;

    static readonly Color Teal = new Color(0.16f, 0.75f, 0.75f);
    static readonly Color Dim = new Color(0.29f, 0.29f, 0.29f);

    int selected;
    float startTime;
    Font font;
    G1SettingsPanel settings;
    AudioSource hum;
    Texture2D _pixelTex;

    bool inLevelSelect;
    List<string> currentMenuItems = new List<string>();

    struct Ember
    {
        public float x, y, speed, alpha, size;
    }
    Ember[] _embers;

    void Start()
    {
        _pixelTex = Texture2D.whiteTexture;
        startTime = Time.time;
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        settings = GetComponent<G1SettingsPanel>();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioListener.volume = PlayerPrefs.GetFloat("G1_MasterVolume", 0.8f);

        // Init background ember particles
        _embers = new Ember[20];
        for (int i = 0; i < _embers.Length; i++)
        {
            _embers[i] = new Ember
            {
                x = Random.Range(0f, 1920f),
                y = Random.Range(0f, 1080f),
                speed = Random.Range(15f, 45f),
                alpha = Random.Range(0.08f, 0.25f),
                size = Random.Range(2f, 5f)
            };
        }

        hum = gameObject.AddComponent<AudioSource>();
        hum.clip = Resources.Load<AudioClip>("Audio/ambient_hum");
        hum.loop = true;
        hum.volume = 0.3f;
        if (hum.clip)
            hum.Play();

        RefreshMenu();
    }

    void RefreshMenu()
    {
        currentMenuItems.Clear();
        if (inLevelSelect)
        {
            var data = G1SaveSystem.Load();
            currentMenuItems.Add("[ LEVEL 1: SUB-SURFACE ]");
            if (data.maxUnlockedLevelIndex >= 2) currentMenuItems.Add("[ LEVEL 2: QUARANTINE ]");
            if (data.maxUnlockedLevelIndex >= 3) currentMenuItems.Add("[ LEVEL 3: THRESHOLD ]");
            currentMenuItems.Add("[ BACK ]");
        }
        else
        {
            if (G1SaveSystem.HasSave)
            {
                currentMenuItems.Add("[ CONTINUE CAMPAIGN ]");
            }
            currentMenuItems.Add("[ NEW GAME ]");
            currentMenuItems.Add("[ LEVEL SELECT ]");
            currentMenuItems.Add("[ BATTLEFIELD ]");
            currentMenuItems.Add("[ WEAPON RANGE ]");
            currentMenuItems.Add("[ SETTINGS ]");
            currentMenuItems.Add("[ QUIT ]");
        }
        selected = 0;
    }

    void Update()
    {
        if (flickerLight)
            flickerLight.intensity = Random.value < 0.06f
                ? Random.Range(0.2f, 0.7f) : 1.4f;

        if (settings != null && settings.visible)
            return;

        int count = currentMenuItems.Count;
        if (count == 0) return;

        if (Input.GetKeyDown(KeyCode.DownArrow))
            selected = (selected + 1) % count;
        if (Input.GetKeyDown(KeyCode.UpArrow))
            selected = (selected + count - 1) % count;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Activate(selected);
    }

    void Activate(int index)
    {
        if (index < 0 || index >= currentMenuItems.Count) return;
        string chosen = currentMenuItems[index];

        if (inLevelSelect)
        {
            if (chosen.Contains("BACK"))
            {
                inLevelSelect = false;
                RefreshMenu();
                return;
            }
            if (chosen.Contains("LEVEL 1")) SceneManager.LoadScene("TestScene");
            else if (chosen.Contains("LEVEL 2")) SceneManager.LoadScene("Level2");
            else if (chosen.Contains("LEVEL 3")) SceneManager.LoadScene("Level3");
            return;
        }

        if (chosen.Contains("CONTINUE"))
        {
            G1SaveSystem.Continue();
        }
        else if (chosen.Contains("NEW GAME"))
        {
            G1SaveSystem.ClearSave();
            SceneManager.LoadScene("TestScene");
        }
        else if (chosen.Contains("BATTLEFIELD"))
        {
            SceneManager.LoadScene("HugeMap");
        }
        else if (chosen.Contains("WEAPON RANGE"))
        {
            G1SaveSystem.ClearSave();
            SceneManager.LoadScene("TestScene");
        }
        else if (chosen.Contains("LEVEL SELECT"))
        {
            inLevelSelect = true;
            RefreshMenu();
        }
        else if (chosen.Contains("SETTINGS"))
        {
            if (settings) settings.visible = true;
        }
        else if (chosen.Contains("QUIT"))
        {
            Application.Quit();
        }
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
        GUI.color = new Color(0.04f, 0.05f, 0.06f, 1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                        Texture2D.whiteTexture);

        // Draw animated background embers
        if (_embers != null)
        {
            Color oldCol = GUI.color;
            for (int i = 0; i < _embers.Length; i++)
            {
                _embers[i].y -= _embers[i].speed * Time.deltaTime;
                if (_embers[i].y < -10f)
                {
                    _embers[i].y = Screen.height + 10f;
                    _embers[i].x = Random.Range(0f, Screen.width);
                }
                GUI.color = new Color(0.16f, 0.75f, 0.75f, _embers[i].alpha);
                GUI.DrawTexture(new Rect(_embers[i].x, _embers[i].y, _embers[i].size, _embers[i].size), _pixelTex);
            }
            GUI.color = oldCol;
        }

        GUI.color = Color.white;

        if (settings != null && settings.visible)
            return;

        float cx = Screen.width / 2f;

        // Panel behind menu content
        float panelW = 540f;
        float panelH = inLevelSelect ? 320f : 430f;
        float panelX = cx - panelW / 2f;
        float panelY = Screen.height * 0.15f;
        DrawModernPanel(new Rect(panelX, panelY, panelW, panelH),
                        new Color(0.02f, 0.04f, 0.06f, 0.7f),
                        new Color(0.16f, 0.75f, 0.75f, 0.6f), 3f);

        // Animated scanline
        float scanlineY = panelY + Mathf.PingPong(Time.time * 30f, panelH);
        DrawHLine(panelX, scanlineY, panelW, new Color(0.16f, 0.75f, 0.75f, 0.06f));

        var title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48, alignment = TextAnchor.MiddleCenter,
        };
        if (font) title.font = font;
        title.normal.textColor = Teal;

        // typewriter reveal, 40 ms per character
        const string full = "THE CORVEX";
        int chars = Mathf.Min(full.Length, (int)((Time.time - startTime) / 0.04f));
        GUI.Label(new Rect(cx - 300, Screen.height * 0.18f, 600, 60),
                  full.Substring(0, chars), title);

        var subTag = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic
        };
        if (font) subTag.font = font;
        subTag.normal.textColor = new Color(0.16f, 0.75f, 0.75f, 0.8f);
        GUI.Label(new Rect(cx - 300, Screen.height * 0.18f + 52f, 600, 24), "THE CORVUS EXPERIMENT  ·  DEMO EDITION", subTag);

        DrawHLine(cx - 200, Screen.height * 0.18f + 80f, 400, new Color(0.16f, 0.75f, 0.75f, 0.4f));

        var subtitleStyle = new GUIStyle(title) { fontSize = 18 };
        subtitleStyle.normal.textColor = Dim;
        if (inLevelSelect)
        {
            GUI.Label(new Rect(cx - 300, Screen.height * 0.32f, 600, 30), "-- SELECT CAMPAIGN LEVEL --", subtitleStyle);
        }

        var item = new GUIStyle(title) { fontSize = 23 };
        for (int i = 0; i < currentMenuItems.Count; i++)
        {
            item.normal.textColor = i == selected ? Teal : Dim;
            var r = new Rect(cx - 250, Screen.height * (inLevelSelect ? 0.42f : 0.35f) + i * 44, 500, 38);

            if (i == selected)
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(0.16f, 0.75f, 0.75f, 0.12f);
                GUI.DrawTexture(new Rect(cx - 240, r.y + (r.height - 34) / 2f, 480, 34), _pixelTex);
                GUI.color = oldColor;
            }

            GUI.Label(r, (i == selected ? "▸ " : "   ") + currentMenuItems[i], item);
            if (r.Contains(Event.current.mousePosition))
            {
                selected = i;
                if (Event.current.type == EventType.MouseDown)
                    Activate(i);
            }
        }

        var verStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.LowerRight
        };
        if (font) verStyle.font = font;
        verStyle.normal.textColor = new Color(0.3f, 0.75f, 0.75f, 0.85f);
        GUI.Label(new Rect(Screen.width - 340, Screen.height - 40, 330, 30),
                  "v1.0.0 — THE CORVEX DEMO", verStyle);
    }
}
