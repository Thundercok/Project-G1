using System.Collections;
using UnityEngine;

/// Opening narrative cinematic (the "begin story"), in the spirit of a Half-Life
/// cold-open. Renders a sequence of prose beats over black with a typewriter
/// reveal, a persistent SKIP prompt, and an atmospheric audio bed. When the
/// sequence finishes — or the player presses Space/Enter/Esc to skip — it hands
/// straight off to the existing floor wake-up cutscene.
///
/// Self-contained: draws its own full-screen GUI and disables player control for
/// the duration. A static guard makes it fire exactly once per play session even
/// if two intro triggers exist on the player.
public sealed class G1IntroStory : MonoBehaviour
{
    /// Set the instant a story is requested so duplicate triggers can't double it.
    public static bool RequestedOrPlayed { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => RequestedOrPlayed = false;

    // One beat of the cold open: a large headline (typed out) + a small subline.
    struct Beat
    {
        public string big;
        public string small;
        public float hold;      // seconds to hold after fully typed
        public Beat(string b, string s, float h) { big = b; small = s; hold = h; }
    }

    // The origin story — the unique "it's a time loop and you are its anchor" premise.
    static readonly Beat[] Beats =
    {
        new Beat("CORVUS DEEP RESEARCH ANNEX", "Sub-Level C  —  0559 hours", 2.0f),
        new Beat("They called it the Threshold.", "A hole in the world one micron wide, held open by a machine the size of a city.", 3.0f),
        new Beat("You are a test engineer.", "Too senior to refuse the morning's experiment. Wrong seniority. Right suit.", 3.0f),
        new Beat("Today the experiment fails.", "It always does.", 2.6f),
        new Beat("This is not the first time.", "The counter above the door reads   ITERATION ##7.", 3.0f),
        new Beat("The things coming through are not aliens.", "They are what is left of everyone who came before you — folded back, again and again.", 3.6f),
        new Beat("You keep waking up because you are the Anchor.", "The disaster cannot exist without you alive inside it.", 3.4f),
        new Beat("The man in the suit is not here to save you.", "A catastrophe that never ends is the most profitable thing there is.", 3.6f),
        new Beat("So he watches. He writes it all down.", "And he waits for you to almost — almost — escape.", 3.4f),
        new Beat("Wake up, Chad.", "Run. Like you always do.", 2.4f),
    };

    // Chained wake-up parameters, supplied by the intro trigger.
    string chapter, subLocation, subjectName, status, directive;

    MouseLook mouseLook;
    PlayerMovement playerMove;
    Texture2D blackTex;
    Font font;

    bool active;
    bool skipped;
    float bgAlpha;
    float lineAlpha;
    string bigTyped = "";
    string smallShown = "";
    float promptPulse;

    GUIStyle bigStyle, smallStyle, skipStyle;

    /// Launch the cinematic, then chain into the floor wake-up cutscene.
    public void Begin(string chapterTitle, string location, string subject,
                      string statusLine, string directiveLine)
    {
        if (RequestedOrPlayed)
        {
            Destroy(this);
            return;
        }
        RequestedOrPlayed = true;

        chapter = chapterTitle;
        subLocation = location;
        subjectName = subject;
        status = statusLine;
        directive = directiveLine;

        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");

        mouseLook = FindFirstObjectByType<MouseLook>();
        playerMove = FindFirstObjectByType<PlayerMovement>();
        if (mouseLook != null) mouseLook.enabled = false;
        if (playerMove != null) playerMove.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        active = true;
        bgAlpha = 1f;
        StartCoroutine(RoutinePlay());
    }

    void Update()
    {
        if (!active) return;
        promptPulse += Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Escape))
            skipped = true;
    }

    IEnumerator RoutinePlay()
    {
        // Soft atmospheric bed for the cold open.
        G1Audio.Play2D("ambient_alien", 0.35f, 0.7f);
        yield return new WaitForSeconds(0.6f);

        for (int i = 0; i < Beats.Length && !skipped; i++)
        {
            var beat = Beats[i];
            bigTyped = "";
            smallShown = "";
            lineAlpha = 1f;

            // Faint stinger as the loop truth lands (beat index 4 = "not the first time").
            if (i == 4) G1Audio.Play2D("door_servo", 0.4f, 0.5f, 0f);

            // Typewriter the headline.
            float typed = 0f;
            const float charsPerSec = 34f;
            while (typed < beat.big.Length && !skipped)
            {
                typed += Time.deltaTime * charsPerSec;
                bigTyped = beat.big.Substring(0, Mathf.Min(beat.big.Length, (int)typed));
                yield return null;
            }
            bigTyped = beat.big;

            // Reveal the subline, then hold.
            smallShown = beat.small;
            float t = 0f;
            while (t < beat.hold && !skipped)
            {
                t += Time.deltaTime;
                yield return null;
            }

            // Fade this beat out before the next (skip cuts straight through).
            float f = 0f;
            while (f < 0.5f && !skipped)
            {
                f += Time.deltaTime;
                lineAlpha = 1f - f / 0.5f;
                yield return null;
            }
        }

        // Hand off to the floor wake-up. It re-asserts its own black eyelid the
        // same frame, so there is no flash of the level between the two.
        if (G1CutsceneManager.Instance != null)
            G1CutsceneManager.Instance.PlayWakeUpIntroCutscene(
                chapter, subLocation, subjectName, status, directive);
        else
        {
            // No cutscene manager present — just restore control.
            if (mouseLook != null) mouseLook.enabled = true;
            if (playerMove != null) playerMove.enabled = true;
        }

        active = false;
        Destroy(this);
    }

    void OnGUI()
    {
        if (!active) return;
        EnsureStyles();

        GUI.color = new Color(0f, 0f, 0f, bgAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);

        float cy = Screen.height * 0.42f;

        if (!string.IsNullOrEmpty(bigTyped))
        {
            GUI.color = new Color(1f, 0.75f, 0.12f, lineAlpha);          // GoldSrc amber
            GUI.Label(new Rect(0, cy, Screen.width, 60), bigTyped, bigStyle);
        }
        if (!string.IsNullOrEmpty(smallShown))
        {
            GUI.color = new Color(0.82f, 0.85f, 0.86f, lineAlpha * 0.92f);
            GUI.Label(new Rect(Screen.width * 0.12f, cy + 62, Screen.width * 0.76f, 60),
                      smallShown, smallStyle);
        }

        // Persistent, gently pulsing skip prompt.
        float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(promptPulse * 2f));
        GUI.color = new Color(0.16f, 0.75f, 0.75f, pulse);              // teal
        GUI.Label(new Rect(0, Screen.height - 52, Screen.width - 40, 30),
                  "PRESS  [SPACE]  TO SKIP  ▸", skipStyle);

        GUI.color = Color.white;
    }

    void EnsureStyles()
    {
        if (bigStyle != null) return;
        bigStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 30, fontStyle = FontStyle.Bold,
            wordWrap = true,
        };
        smallStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter, fontSize = 18, fontStyle = FontStyle.Italic,
            wordWrap = true,
        };
        skipStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight, fontSize = 15, fontStyle = FontStyle.Bold,
        };
        if (font != null)
        {
            bigStyle.font = font; smallStyle.font = font; skipStyle.font = font;
        }
    }
}
