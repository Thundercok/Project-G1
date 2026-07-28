using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// The cold open: a camera flight over the Sprawl with narration, before the
/// player is handed the controls.
///
/// The huge map has never had one. You spawn on a road outside a gate, with a
/// hazard suit, six weapons and no idea what any of it is for — and the story
/// director's first card only appears once you have already walked somewhere.
/// Every question a new player has ("where am I, who am I, why is this place
/// like this, what do I want") was answerable only by talking to an NPC they
/// had no reason to look for.
///
/// The shape is Half-Life's, and so is the discipline behind it: the camera
/// shows you the place, a voice tells you the least it can get away with, and
/// nothing is explained that the level could explain by being looked at. The
/// aerial pass over the breach is doing more work than any line in it.
///
/// It is skippable from the first frame, holds no state, and hands back
/// everything it borrowed.
public sealed class G1OpeningSequence : MonoBehaviour
{
    [System.Serializable]
    public class Shot
    {
        public Vector3 from;
        public Vector3 to;
        public Vector3 lookFrom;
        public Vector3 lookTo;
        public float seconds = 6f;

        [TextArea] public string caption;      // what is said over this shot
        public string speaker;                 // "HEV V.I." / "THE AUDITOR" / ""
        public string title;                   // big card, if any
        public string subtitle;
    }

    public Shot[] shots = new Shot[0];
    public float fadeIn = 1.6f;
    public float fadeOut = 1.4f;
    public bool playOnStart = true;

    static bool playedThisSession;

    Camera cam;
    Transform camT;
    GameObject player;
    readonly List<Behaviour> suspended = new List<Behaviour>();
    readonly List<Renderer> hidden = new List<Renderer>();
    Transform camParent;
    Vector3 camLocalPos;
    Quaternion camLocalRot;

    G1Voice vi, auditor, self;
    Font font;

    // what is on screen right now
    string capSpeaker = "", capFull = "", capShown = "";
    int capChar;
    float nextChar, charInterval = 0.03f;
    string cardTitle = "", cardSub = "";
    float cardUntil = -1f, cardFrom;
    float black = 1f;          // 1 = fully black
    bool running;

    IEnumerator Start()
    {
        if (!playOnStart || playedThisSession || shots.Length == 0) yield break;
        playedThisSession = true;
        yield return Play();
    }

    public IEnumerator Play()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        player = GameObject.FindWithTag("Player");
        if (player == null) yield break;
        cam = player.GetComponentInChildren<Camera>(true);
        if (cam == null) yield break;
        camT = cam.transform;

        Borrow();
        running = true;

        vi = MakeVoice("OpeningVi", 1.36f, 0.02f, 3);
        auditor = MakeVoice("OpeningAuditor", 0.62f, 0.03f, 6);
        self = MakeVoice("OpeningSelf", 0.92f, 0.08f, 4);

        // fade up out of black rather than cutting: the first frame of a cold
        // open is the one that decides whether the player is watching or
        // waiting for it to end
        for (float t = 0f; t < fadeIn && !Skipped(); t += Time.unscaledDeltaTime)
        {
            black = 1f - t / fadeIn;
            yield return null;
        }
        black = 0f;

        foreach (var s in shots)
        {
            if (Skipped()) break;
            if (!string.IsNullOrEmpty(s.title)) ShowCard(s.title, s.subtitle);
            float spoken = 0f;
            if (!string.IsNullOrEmpty(s.caption)) spoken = Say(s.speaker, s.caption);

            // Never cut away mid-sentence. The shot lengths were written by eye
            // and the recordings are whatever length eSpeak makes them, so the
            // two disagree — and when the shot was shorter, the next line
            // started on top of the one still playing.
            float dur = Mathf.Max(Mathf.Max(0.2f, s.seconds), spoken + 0.7f);
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                // ease both ends: a camera that starts and stops dead reads as
                // a slideshow, and a constant-speed dolly reads as a flythrough
                // video rather than as a shot
                float e = k * k * (3f - 2f * k);
                camT.position = Vector3.Lerp(s.from, s.to, e);
                camT.rotation = Quaternion.LookRotation(
                    Vector3.Lerp(s.lookFrom, s.lookTo, e) - camT.position, Vector3.up);
                Reveal();
                if (Skipped()) break;
                yield return null;
            }
        }

        for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
        {
            black = t / fadeOut;
            yield return null;
        }
        black = 1f;
        capShown = capFull = "";
        cardUntil = -1f;

        Return();

        for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
        {
            black = 1f - t / 0.9f;
            yield return null;
        }
        black = 0f;
        running = false;
    }

    // ------------------------------------------------------------- borrow
    /// Take the camera and switch off everything that would fight for it, then
    /// give all of it back. Anything missed here is a control the player never
    /// gets back, so the list is explicit rather than a blanket disable of the
    /// player object — which would also stop the things that have to keep
    /// running, like the objective manager reading its own state.
    void Borrow()
    {
        suspended.Clear();
        foreach (var b in new Behaviour[]
                 {
                     player.GetComponent<PlayerMovement>(),
                     player.GetComponentInChildren<MouseLook>(true),
                     player.GetComponentInChildren<PlayerUse>(true),
                     cam.GetComponent<CameraEffects>(),
                     // The story director opens its first chapter in Start(),
                     // which is the same moment this begins — so the prologue
                     // narration and the opening narration were both talking,
                     // each cutting the other off mid-word. Two narrators is
                     // not a mix problem, it is a bug.
                     Object.FindObjectOfType<G1StoryDirector>(),
                 })
        {
            if (b == null || !b.enabled) continue;
            b.enabled = false;
            suspended.Add(b);
        }

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        hidden.Clear();
        foreach (var r in camT.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled) continue;
            r.enabled = false;
            hidden.Add(r);
        }

        camParent = camT.parent;
        camLocalPos = camT.localPosition;
        camLocalRot = camT.localRotation;
        camT.SetParent(null, true);
    }

    void Return()
    {
        camT.SetParent(camParent, false);
        camT.localPosition = camLocalPos;
        camT.localRotation = camLocalRot;

        foreach (var r in hidden) if (r != null) r.enabled = true;
        hidden.Clear();
        foreach (var b in suspended) if (b != null) b.enabled = true;
        suspended.Clear();

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = true;
    }

    // -------------------------------------------------------------- speech
    G1Voice MakeVoice(string name, float pitch, float wobble, int rate)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var v = go.AddComponent<G1Voice>();
        v.pitch = pitch; v.wobble = wobble; v.lettersPerBlip = rate;
        var src = go.GetComponent<AudioSource>();
        if (src != null) src.spatialBlend = 0f;      // narration is not in the world
        v.blipVolume = 0.5f;
        // the plot does not wait for a radio bark to finish
        v.interruptible = false;
        return v;
    }

    G1Voice VoiceFor(string who) =>
        who == "THE AUDITOR" ? auditor : who == "YOU" ? self : vi;

    float Say(string who, string text)
    {
        capSpeaker = string.IsNullOrEmpty(who) ? "HEV V.I." : who;
        capFull = text; capShown = ""; capChar = 0;
        nextChar = Time.unscaledTime;
        charInterval = 0.03f;

        var v = VoiceFor(capSpeaker);
        if (v != null)
        {
            v.Begin(text);
            if (v.SpokenLength > 0.05f && text.Length > 0)
                charInterval = v.SpokenLength / text.Length;
            return v.SpokenLength;
        }
        return 0f;
    }

    void Reveal()
    {
        if (capChar >= capFull.Length || Time.unscaledTime < nextChar) return;
        char c = capFull[capChar++];
        capShown += c;
        VoiceFor(capSpeaker)?.Letter(c);
        nextChar = Time.unscaledTime + charInterval;
    }

    void ShowCard(string title, string sub)
    {
        cardTitle = title; cardSub = sub;
        cardFrom = Time.unscaledTime;
        cardUntil = cardFrom + 4.2f;
    }

    bool skipped;
    bool Skipped()
    {
        if (skipped) return true;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Escape))
            skipped = true;
        return skipped;
    }

    // ----------------------------------------------------------------- HUD
    void OnGUI()
    {
        if (!running && black <= 0f) return;

        if (black > 0f)
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, black);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
        if (!running) return;

        // letterbox: the cheapest possible signal that control is not yours yet
        float bar = Screen.height * 0.11f;
        var c = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, bar), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, Screen.height - bar, Screen.width, bar), Texture2D.whiteTexture);
        GUI.color = c;

        if (cardUntil > 0f && Time.unscaledTime < cardUntil)
        {
            float a = Mathf.Clamp01(Mathf.Min(Time.unscaledTime - cardFrom,
                                              cardUntil - Time.unscaledTime) / 0.8f);
            var big = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46, font = font, alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            big.normal.textColor = new Color(0.93f, 0.92f, 0.88f, a);
            GUI.Label(new Rect(0, Screen.height * 0.36f, Screen.width, 60), cardTitle, big);

            var small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, font = font, alignment = TextAnchor.MiddleCenter,
            };
            small.normal.textColor = new Color(0.72f, 0.70f, 0.62f, a);
            GUI.Label(new Rect(0, Screen.height * 0.36f + 58f, Screen.width, 30), cardSub, small);
        }

        if (!string.IsNullOrEmpty(capShown))
        {
            float w = Mathf.Min(880f, Screen.width - 80f);
            float x = Screen.width / 2f - w / 2f;
            float y = Screen.height - bar - 104f;

            var name = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, font = font, fontStyle = FontStyle.Bold,
            };
            name.normal.textColor = capSpeaker == "THE AUDITOR"
                ? new Color(0.85f, 0.82f, 0.6f)
                : capSpeaker == "YOU" ? new Color(0.75f, 0.82f, 0.9f)
                                      : new Color(0.35f, 0.85f, 1f);
            GUI.Label(new Rect(x, y, w, 20), capSpeaker, name);

            var body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, font = font, wordWrap = true,
            };
            body.normal.textColor = new Color(0.94f, 0.94f, 0.92f);
            GUI.Label(new Rect(x, y + 20f, w, 76f), capShown, body);
        }

        var skip = new GUIStyle(GUI.skin.label) { fontSize = 13, font = font };
        skip.normal.textColor = new Color(0.7f, 0.7f, 0.68f, 0.65f);
        GUI.Label(new Rect(Screen.width - 190f, Screen.height - bar - 26f, 180f, 20f),
                  "SPACE — SKIP", skip);
    }
}
