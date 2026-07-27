using UnityEngine;

/// Turns the Corvus Sprawl from a battlefield with errands on it into a story
/// with a shape.
///
/// The contacts already introduce each other in a fixed order, and their
/// assignments already escalate — the dead, then the wounded, then the sky,
/// then the ground, then the signal, then the truth. So the spine doesn't add
/// a parallel quest line competing with them; it *is* them, reframed. Each
/// chapter watches an objective that already exists, puts a card on screen
/// when it opens, and has somebody speak when it closes.
///
/// Two voices carry it. The suit V.I. is what you hear; the Auditor is what
/// hears you. He gets the last word of most chapters, because he is the one
/// who already knows how this ends.
public sealed class G1StoryDirector : MonoBehaviour
{
    public enum Speaker { Vi, Auditor, Self }

    [System.Serializable]
    public class Beat
    {
        public Speaker who = Speaker.Vi;
        [TextArea] public string line;
    }

    [System.Serializable]
    public class Chapter
    {
        public string objectiveId;         // an objective that already exists
        public string title = "CHAPTER";
        public string subtitle = "";
        public Beat[] onOpen;
        public Beat[] onClose;
    }

    public Chapter[] chapters = new Chapter[0];
    public float lineHold = 5.5f;
    public float betweenLines = 0.6f;

    int index = -1;
    int beatIdx;
    Beat[] queue;
    float nextBeat;

    // the line currently being spoken, revealed a character at a time
    string speakerName = "", full = "", shown = "";
    int charIdx;
    float nextChar, lineUntil, charInterval = 0.024f;
    G1Voice vi, auditor, self;
    Font font;
    G1StoryCard card;

    public int Chapter_ => index;

    void Start()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        card = GetComponent<G1StoryCard>();

        // three voices out of the same six syllables — the difference is who
        // they are, not what they're made of
        vi = Make("ViVoice", 1.36f, 0.02f, 3);        // clipped, synthetic, flat
        auditor = Make("AuditorVoice", 0.62f, 0.03f, 6);  // slow, level, patient
        self = Make("SelfVoice", 0.92f, 0.08f, 4);    // you, breathing hard

        if (chapters.Length > 0) Open(0);
    }

    G1Voice Make(string name, float pitch, float wobble, int rate)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var v = go.AddComponent<G1Voice>();
        v.pitch = pitch; v.wobble = wobble; v.lettersPerBlip = rate;
        // narration is in your head (or your earpiece), not out in the world
        var src = go.GetComponent<AudioSource>();
        if (src != null) src.spatialBlend = 0f;
        v.blipVolume = 0.5f;
        return v;
    }

    void Open(int i)
    {
        index = i;
        var ch = chapters[i];
        if (card != null)
        {
            card.title = ch.title;
            card.subtitle = ch.subtitle;
            card.Show();
        }
        Queue(ch.onOpen);
    }

    void Queue(Beat[] beats)
    {
        queue = beats;
        beatIdx = 0;
        nextBeat = Time.time + 1.2f;      // let the card land first
    }

    void Update()
    {
        // reveal the current line
        if (charIdx < full.Length && Time.time >= nextChar)
        {
            char c = full[charIdx++];
            shown += c;
            Voice(speakerName)?.Letter(c);
            nextChar = Time.time + charInterval;
        }

        // next queued beat once the current line has had its time on screen
        if (queue != null && beatIdx < queue.Length &&
            Time.time >= nextBeat && Time.time >= lineUntil)
        {
            var b = queue[beatIdx++];
            Say(Name(b.who), b.line);
        }

        // has this chapter's objective finished?
        if (index >= 0 && index < chapters.Length && !closing)
        {
            var om = G1ObjectiveManager.Instance;
            var ch = chapters[index];
            var obj = om != null && !string.IsNullOrEmpty(ch.objectiveId)
                ? om.objectives.Find(o => o.id == ch.objectiveId) : null;
            if (obj != null && obj.isCompleted)
            {
                closing = true;
                Queue(ch.onClose);
            }
        }

        // chapter is over once its closing beats have all been spoken
        if (closing && queue != null && beatIdx >= queue.Length &&
            Time.time >= lineUntil && index + 1 < chapters.Length)
        {
            closing = false;
            Open(index + 1);
        }
    }

    bool closing;

    void Say(string who, string text)
    {
        speakerName = who;
        full = text; shown = ""; charIdx = 0;
        nextChar = Time.time;
        lineUntil = Time.time + lineHold + text.Length * 0.02f;

        var v = Voice(who);
        charInterval = 0.024f;
        if (v != null)
        {
            v.Begin(text);
            // hold the subtitle for as long as the line takes to say, and
            // reveal it at that speed rather than a fixed one
            if (v.SpokenLength > 0.05f && text.Length > 0)
            {
                charInterval = v.SpokenLength / text.Length;
                lineUntil = Time.time + v.SpokenLength + 1.4f;
            }
        }
        nextBeat = lineUntil + betweenLines;
    }

    static string Name(Speaker s) =>
        s == Speaker.Auditor ? "THE AUDITOR" : s == Speaker.Self ? "YOU" : "HEV V.I.";

    G1Voice Voice(string who) =>
        who == "THE AUDITOR" ? auditor : who == "YOU" ? self : vi;

    void OnGUI()
    {
        if (Time.time > lineUntil || string.IsNullOrEmpty(shown)) return;

        float w = 820f, h = 92f;
        float x = Screen.width / 2f - w / 2f, y = Screen.height - 210f;

        var box = new GUIStyle(GUI.skin.box);
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, box);
        GUI.color = prev;

        var name = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, fontStyle = FontStyle.Bold, font = font,
            alignment = TextAnchor.UpperLeft,
        };
        name.normal.textColor = speakerName == "THE AUDITOR"
            ? new Color(0.85f, 0.82f, 0.6f)          // dry, colourless authority
            : speakerName == "YOU"
                ? new Color(0.75f, 0.82f, 0.9f)
                : new Color(0.35f, 0.85f, 1f);       // suit cyan
        GUI.Label(new Rect(x + 14f, y + 8f, w - 28f, 22f), speakerName, name);

        var body = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17, font = font, wordWrap = true,
            alignment = TextAnchor.UpperLeft,
        };
        body.normal.textColor = new Color(0.92f, 0.92f, 0.9f, 0.96f);
        GUI.Label(new Rect(x + 14f, y + 30f, w - 28f, h - 38f), shown, body);
    }
}
