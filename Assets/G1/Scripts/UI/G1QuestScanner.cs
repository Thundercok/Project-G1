using System.Collections.Generic;
using UnityEngine;

/// The suit's bio-signal scanner — how you FIND the people who hand out work.
///
///   [Q]  scan pulse. An expanding ring sweeps the district; every quest contact
///        inside the radius is logged, named, and pinned to the compass. Finds
///        nothing? It still reports the bearing of the nearest unknown signal,
///        so you are never stranded without a lead.
///   [J]  contact log — every contact found so far, with role, district,
///        distance and whether they're waiting on you.
///
/// Between scans the compass strip along the top edge keeps every known contact
/// on a bearing, and a glyph floats over each one out in the world:
///   [!] has work   [·] in progress   [?] report back   [✓] finished
///
/// Drop this on the player. It reads <see cref="G1QuestNpc"/>'s static registry,
/// so it needs no wiring at all.
public sealed class G1QuestScanner : MonoBehaviour
{
    [Header("Scan")]
    public KeyCode scanKey = KeyCode.Q;
    public KeyCode logKey = KeyCode.J;
    public float scanRadius = 150f;
    public float scanCooldown = 5f;
    public float pulseSpeed = 90f;          // metres/second the ring travels

    [Header("Compass")]
    public float compassSpan = 140f;        // degrees of bearing shown
    public float compassWidth = 560f;

    float nextScan;
    float pulseStart = -99f;
    string banner = "";
    float bannerUntil = -1f;
    G1QuestNpc weakSignal;
    float weakSignalUntil = -1f;
    bool logOpen;

    LineRenderer ring;
    Font font;
    Camera cam;

    static readonly Vector3[] ringPts = new Vector3[49];

    void Start()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        cam = Camera.main;

        var go = new GameObject("ScanPulse");
        go.transform.SetParent(transform, false);
        ring = go.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = ringPts.Length;
        ring.widthMultiplier = 0.35f;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.enabled = false;
    }

    void Update()
    {
        if (cam == null) cam = Camera.main;

        if (Input.GetKeyDown(logKey)) logOpen = !logOpen;
        if (Input.GetKeyDown(scanKey) && Time.time >= nextScan) Scan();

        AnimatePulse();
    }

    /// Public so the self-test can fire a sweep without injecting a keypress.
    public void Scan()
    {
        nextScan = Time.time + scanCooldown;
        pulseStart = Time.time;
        G1Audio.Play2D("door_servo", 0.45f, 1.7f);

        int found = 0;
        string firstName = "";
        foreach (var npc in G1QuestNpc.All)
        {
            if (npc == null || npc.discovered) continue;
            if (Vector3.Distance(transform.position, npc.transform.position) > scanRadius) continue;
            if (npc.Discover(true))          // one chirp for the sweep, not per contact
            {
                if (found == 0) firstName = npc.npcName;
                found++;
            }
        }

        if (found > 0) G1Audio.Play2D("pickup", 0.5f, 1.9f);
        if (found == 1) Banner($"CONTACT ACQUIRED — {firstName}");
        else if (found > 1) Banner($"{found} CONTACTS ACQUIRED");
        else
        {
            // nothing new in range: give them the bearing of the nearest unknown
            weakSignal = NearestUndiscovered(out float d);
            if (weakSignal != null)
            {
                weakSignalUntil = Time.time + 14f;
                Banner($"WEAK BIO-SIGNAL — {Mathf.RoundToInt(d)}m {Cardinal(weakSignal.transform.position)}");
            }
            else
            {
                Banner(AnyPending() ? "NO NEW CONTACTS — CHECK THE LOG [J]"
                                    : "NO BIO-SIGNALS IN RANGE");
            }
        }
    }

    bool AnyPending()
    {
        foreach (var n in G1QuestNpc.All)
            if (n != null && n.discovered && n.stage != G1QuestNpc.Stage.Done) return true;
        return false;
    }

    G1QuestNpc NearestUndiscovered(out float dist)
    {
        dist = float.MaxValue;
        G1QuestNpc best = null;
        foreach (var n in G1QuestNpc.All)
        {
            if (n == null || n.discovered) continue;
            float d = Vector3.Distance(transform.position, n.transform.position);
            if (d < dist) { dist = d; best = n; }
        }
        return best;
    }

    void AnimatePulse()
    {
        float age = Time.time - pulseStart;
        float travel = age * pulseSpeed;
        if (travel > scanRadius) { if (ring.enabled) ring.enabled = false; return; }

        ring.enabled = true;
        float fade = 1f - travel / scanRadius;
        var c = new Color(0.3f, 0.95f, 0.95f, fade * 0.85f);
        ring.startColor = c; ring.endColor = c;
        ring.widthMultiplier = 0.25f + travel * 0.008f;

        Vector3 centre = transform.position + Vector3.up * 0.4f;
        for (int i = 0; i < ringPts.Length; i++)
        {
            float a = i / (float)(ringPts.Length - 1) * Mathf.PI * 2f;
            ringPts[i] = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * travel;
        }
        ring.SetPositions(ringPts);
    }

    void Banner(string text)
    {
        banner = text;
        bannerUntil = Time.time + 4.5f;
    }

    /// Compass-rose letters for a world position, relative to the player.
    string Cardinal(Vector3 worldPos)
    {
        Vector3 to = worldPos - transform.position; to.y = 0f;
        float bearing = Mathf.Repeat(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 360f);
        string[] pts = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        return pts[Mathf.RoundToInt(bearing / 45f) % 8];
    }

    // ------------------------------------------------------------------ HUD
    void OnGUI()
    {
        if (cam == null) return;
        DrawCompass();
        DrawWorldMarkers();
        DrawBanner();
        DrawScanReadout();
        if (logOpen) DrawContactLog();
    }

    void DrawCompass()
    {
        float cx = Screen.width / 2f, top = 10f;
        var tick = new GUIStyle(GUI.skin.label)
        { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (font) tick.font = font;

        // baseline
        GUI.color = new Color(0.3f, 0.95f, 0.95f, 0.18f);
        GUI.DrawTexture(new Rect(cx - compassWidth / 2f, top + 26f, compassWidth, 1f),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;

        Vector3 fwd = cam.transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) return;

        foreach (var npc in G1QuestNpc.All)
        {
            if (npc == null || !npc.discovered) continue;
            DrawCompassTick(npc.transform.position, npc.MarkerGlyph(), npc.MarkerColor(),
                            npc.npcName, cx, top, fwd, tick);

            // where the accepted work actually is
            if (npc.stage == G1QuestNpc.Stage.Active && npc.hasQuestTarget)
                DrawCompassTick(npc.questTarget, "◆", new Color(1f, 0.75f, 0.2f),
                                npc.targetLabel, cx, top, fwd, tick);
        }

        // the unresolved lead from the last scan, dim and nameless
        if (weakSignal != null && !weakSignal.discovered && Time.time < weakSignalUntil)
            DrawCompassTick(weakSignal.transform.position, "?",
                            new Color(0.55f, 0.6f, 0.62f), "UNKNOWN", cx, top, fwd, tick);
    }

    void DrawCompassTick(Vector3 worldPos, string glyph, Color col, string label,
                         float cx, float top, Vector3 fwd, GUIStyle tick)
    {
        Vector3 to = worldPos - transform.position; to.y = 0f;
        float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
        if (Mathf.Abs(angle) > compassSpan / 2f) return;

        float x = cx + angle / (compassSpan / 2f) * (compassWidth / 2f);
        tick.normal.textColor = col;
        GUI.Label(new Rect(x - 20f, top, 40f, 24f), glyph, tick);

        // only the one you're looking straight at gets named
        if (Mathf.Abs(angle) < 9f)
        {
            var name = new GUIStyle(tick) { fontSize = 13, fontStyle = FontStyle.Normal };
            name.normal.textColor = new Color(col.r, col.g, col.b, 0.85f);
            int d = Mathf.RoundToInt(Vector3.Distance(transform.position, worldPos));
            GUI.Label(new Rect(x - 110f, top + 28f, 220f, 20f), $"{label} · {d}m", name);
        }
    }

    void DrawWorldMarkers()
    {
        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (font) style.font = font;

        foreach (var npc in G1QuestNpc.All)
        {
            if (npc == null || !npc.discovered) continue;
            if (npc.stage == G1QuestNpc.Stage.Done) continue;   // stop nagging once paid

            if (npc.stage == G1QuestNpc.Stage.Active && npc.hasQuestTarget)
                DrawMarker(npc.questTarget + Vector3.up * 2f, "◆", npc.targetLabel,
                           new Color(1f, 0.75f, 0.2f), style);

            DrawMarker(npc.transform.position + Vector3.up * 2.5f, npc.MarkerGlyph(),
                       npc.npcName, npc.MarkerColor(), style);
        }
    }

    /// One screen-clamped world marker: glyph, and a name/distance caption once
    /// you're close enough for it to be worth reading.
    void DrawMarker(Vector3 worldPos, string glyph, string label, Color col, GUIStyle style)
    {
        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        if (sp.z < 0f) return;                          // behind you: the compass has it

        float dist = Vector3.Distance(transform.position, worldPos);
        float gx = Mathf.Clamp(sp.x, 40f, Screen.width - 40f);
        float gy = Mathf.Clamp(Screen.height - sp.y, 60f, Screen.height - 60f);

        // fade with distance so a far district doesn't clutter the screen
        float alpha = Mathf.Lerp(0.95f, 0.35f, Mathf.InverseLerp(20f, 220f, dist));
        var c = col; c.a = alpha;

        style.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.7f);
        GUI.Label(new Rect(gx - 99f, gy - 13f, 200f, 26f), glyph, style);
        style.normal.textColor = c;
        GUI.Label(new Rect(gx - 100f, gy - 14f, 200f, 26f), glyph, style);

        if (dist < 90f)
        {
            var sub = new GUIStyle(style) { fontSize = 13, fontStyle = FontStyle.Normal };
            sub.normal.textColor = c;
            GUI.Label(new Rect(gx - 110f, gy + 8f, 220f, 20f),
                      $"{label} · {Mathf.RoundToInt(dist)}m", sub);
        }
    }

    void DrawBanner()
    {
        if (Time.time > bannerUntil || string.IsNullOrEmpty(banner)) return;
        float alpha = Mathf.Clamp01(bannerUntil - Time.time);
        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (font) style.font = font;
        style.normal.textColor = new Color(0.3f, 0.95f, 0.95f, alpha);
        GUI.Label(new Rect(Screen.width / 2f - 300f, Screen.height * 0.24f, 600f, 28f),
                  banner, style);
    }

    void DrawScanReadout()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        if (font) style.font = font;
        float cd = nextScan - Time.time;
        bool ready = cd <= 0f;
        style.normal.textColor = ready
            ? new Color(0.3f, 0.95f, 0.95f, 0.8f)
            : new Color(0.5f, 0.55f, 0.58f, 0.7f);
        string text = ready ? "[Q] BIO-SCAN   [J] CONTACTS"
                            : $"[Q] RECHARGING {cd:0.0}s   [J] CONTACTS";
        GUI.Label(new Rect(40f, Screen.height - 150f, 320f, 20f), text, style);
    }

    void DrawContactLog()
    {
        float w = 640f, x = Screen.width / 2f - w / 2f, y = 90f;
        var rows = new List<G1QuestNpc>();
        int unknown = 0;
        foreach (var n in G1QuestNpc.All)
        {
            if (n == null) continue;
            if (n.discovered) rows.Add(n); else unknown++;
        }

        float h = 78f + rows.Count * 46f + 28f;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var head = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        if (font) head.font = font;
        head.normal.textColor = new Color(0.3f, 0.95f, 0.95f);
        GUI.Label(new Rect(x + 18f, y + 12f, w - 36f, 26f), "◈ CONTACT LOG", head);

        var line = new GUIStyle(GUI.skin.label) { fontSize = 15 };
        if (font) line.font = font;
        float ry = y + 48f;

        if (rows.Count == 0)
        {
            line.normal.textColor = new Color(0.7f, 0.72f, 0.74f);
            GUI.Label(new Rect(x + 18f, ry, w - 36f, 22f),
                      "No contacts logged. Run a bio-scan [Q] to sweep the district.", line);
            ry += 30f;
        }

        rows.Sort((a, b) =>
            Vector3.Distance(transform.position, a.transform.position)
            .CompareTo(Vector3.Distance(transform.position, b.transform.position)));

        foreach (var n in rows)
        {
            int d = Mathf.RoundToInt(Vector3.Distance(transform.position, n.transform.position));
            line.normal.textColor = n.MarkerColor();
            GUI.Label(new Rect(x + 18f, ry, w - 36f, 22f),
                      $"[{n.MarkerGlyph()}] {n.npcName}  —  {n.profile.title}", line);
            var sub = new GUIStyle(line) { fontSize = 13 };
            sub.normal.textColor = new Color(0.72f, 0.74f, 0.76f, 0.9f);
            GUI.Label(new Rect(x + 40f, ry + 20f, w - 58f, 20f),
                      $"{n.district} · {d}m · {n.StageText()} · {n.questTitle}", sub);
            ry += 46f;
        }

        var foot = new GUIStyle(line) { fontSize = 14 };
        foot.normal.textColor = new Color(0.55f, 0.6f, 0.62f);
        GUI.Label(new Rect(x + 18f, y + h - 26f, w - 36f, 20f),
                  unknown > 0
                    ? $"{unknown} unresolved bio-signal(s) — scan again from somewhere new."
                    : "All bio-signals resolved.", foot);
    }
}
