using System.Collections.Generic;
using UnityEngine;

/// V.I. — the mission AI assistant. Watches G1ObjectiveManager and:
///  - announces progress and completions in a retro comms panel (typewriter),
///  - reminds the player of the active objective periodically,
///  - declares the extraction gate online when all mandatory objectives finish,
///  - draws a persistent MISSION tracker listing every objective + progress.
/// Drop one on the player (or any scene object); it finds the objective manager.
public sealed class G1MissionAssistant : MonoBehaviour
{
    public string assistantName = "V.I.";
    public float reminderInterval = 32f;

    struct Snap { public int count; public bool done; }
    readonly Dictionary<string, Snap> snap = new Dictionary<string, Snap>();

    // comms line being shown
    string fullLine = "";
    string shownLine = "";
    int charIdx;
    float nextChar;
    float lineUntil = -1f;
    float nextReminder;
    bool announcedComplete;

    Font font;

    void Start()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        G1ObjectiveManager.OnObjectiveUpdated += OnUpdated;
        // greet + prime the snapshot after the level has registered its objectives
        Invoke(nameof(Intro), 1.2f);
        nextReminder = Time.time + reminderInterval;
    }

    void OnDestroy() { G1ObjectiveManager.OnObjectiveUpdated -= OnUpdated; }

    void Intro()
    {
        Prime();
        var om = G1ObjectiveManager.Instance;
        if (om != null && om.objectives.Count > 0)
            Say("Mission online. " + om.GetActiveObjectiveText());
    }

    void Prime()
    {
        var om = G1ObjectiveManager.Instance;
        if (om == null) return;
        foreach (var o in om.objectives)
            snap[o.id] = new Snap { count = o.currentCount, done = o.isCompleted };
    }

    void OnUpdated(G1ObjectiveManager.Objective active, bool isNewCompletion)
    {
        var om = G1ObjectiveManager.Instance;
        if (om == null) return;

        foreach (var o in om.objectives)
        {
            snap.TryGetValue(o.id, out Snap prev);
            bool known = snap.ContainsKey(o.id);
            if (o.isCompleted && (!known || !prev.done))
                Say("Objective complete — " + o.description + ".");
            else if (known && o.currentCount > prev.count && !o.isCompleted)
                Say(o.description + ": " + o.currentCount + " of " + o.requiredCount + ".");
            snap[o.id] = new Snap { count = o.currentCount, done = o.isCompleted };
        }

        if (!announcedComplete && om.objectives.Count > 0 && om.IsLevelComplete())
        {
            announcedComplete = true;
            Say("All objectives complete. Extraction gate is online — move to the beacon.");
        }
        nextReminder = Time.time + reminderInterval;
    }

    void Update()
    {
        // typewriter
        if (charIdx < fullLine.Length && Time.time >= nextChar)
        {
            shownLine += fullLine[charIdx++];
            nextChar = Time.time + 0.02f;
        }
        // periodic reminder of the active objective
        var om = G1ObjectiveManager.Instance;
        if (om != null && om.objectives.Count > 0 && !om.IsLevelComplete()
            && Time.time >= nextReminder)
        {
            nextReminder = Time.time + reminderInterval;
            Say("Reminder — " + om.GetActiveObjectiveText());
        }
    }

    void Say(string text)
    {
        fullLine = text;
        shownLine = "";
        charIdx = 0;
        nextChar = Time.time;
        lineUntil = Time.time + 6.5f;
        G1Audio.Play2D("pickup", 0.4f, 1.5f, 0f);   // assistant chirp
    }

    void OnGUI()
    {
        var om = G1ObjectiveManager.Instance;
        if (om == null) return;

        // --- persistent MISSION tracker (top-right)
        var head = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        if (font) head.font = font;
        head.normal.textColor = new Color(0.16f, 0.75f, 0.75f, 0.95f);
        float x = Screen.width - 330, y = 16;
        GUI.Label(new Rect(x, y, 320, 24), "◈ MISSION", head);
        y += 26;

        var line = new GUIStyle(GUI.skin.label) { fontSize = 15 };
        if (font) line.font = font;
        foreach (var o in om.objectives)
        {
            string box = o.isCompleted ? "[✓] " : "[ ] ";
            string cnt = o.requiredCount > 1 ? $"  ({o.currentCount}/{o.requiredCount})" : "";
            string tag = o.isMandatory ? "" : "  (optional)";
            line.normal.textColor = o.isCompleted
                ? new Color(0.4f, 0.85f, 0.4f, 0.9f)
                : new Color(1f, 0.62f, 0.1f, 0.92f);
            GUI.Label(new Rect(x, y, 320, 22), box + o.description + cnt + tag, line);
            y += 22;
        }

        // --- assistant comms panel (bottom-center) while a line is active
        if (Time.time < lineUntil && !string.IsNullOrEmpty(shownLine))
        {
            float alpha = Mathf.Clamp01(lineUntil - Time.time);
            var comms = new GUIStyle(GUI.skin.label)
            { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            if (font) comms.font = font;
            float w = 720, cx = Screen.width / 2f - w / 2f, cy = Screen.height - 150;
            GUI.color = new Color(0f, 0f, 0f, 0.45f * alpha);
            GUI.DrawTexture(new Rect(cx, cy, w, 46), Texture2D.whiteTexture);
            GUI.color = Color.white;
            comms.normal.textColor = new Color(0.16f, 0.85f, 0.85f, alpha);
            GUI.Label(new Rect(cx + 10, cy, w - 20, 46),
                      $"◈ {assistantName}:  {shownLine}", comms);
        }
    }
}
