using System.Collections.Generic;
using UnityEngine;

/// A survivor who hands out work, and takes it back when it's done.
///
/// The full loop is FIND → RECEIVE → DO → RETURN:
///   Available      the contact has an assignment; marker shows [!]
///   Offering       you pressed E — the brief is on screen, E accepts, X walks away
///   Active         the objective is registered and a waypoint marks the work
///   ReadyToTurnIn  the objective completed; marker flips to [?], come collect
///   Done           paid out; the contact may point you at the next one
///
/// Finding them is the other half, and lives in <see cref="G1QuestScanner"/> —
/// this component only publishes itself to the static registry and keeps its
/// state readable from a distance (beacon light + marker glyph).
public sealed class G1QuestNpc : MonoBehaviour, IUsable
{
    public enum Stage { Available, Active, ReadyToTurnIn, Done }

    [Header("Identity")]
    public string npcName = "SURVIVOR";
    public G1NpcRole role = G1NpcRole.Engineer;
    public string district = "UNKNOWN SECTOR";     // shown in the contact log

    [Header("Quest")]
    public string questId = "";
    public string questTitle = "Unnamed assignment";
    public bool mandatory = true;
    public int requiredCount = 1;
    public bool hasQuestTarget = true;             // false = no guiding waypoint
    public Vector3 questTarget;                    // where the work is
    public string targetLabel = "OBJECTIVE";

    [Header("Dialogue")]
    [TextArea] public string offerLine = "There's work that needs doing. You in?";
    [TextArea] public string acceptLine = "Good. Move fast — it won't stay quiet.";
    [TextArea] public string nagLine = "Still waiting on you. It isn't done yet.";
    [TextArea] public string turnInLine = "You actually did it. Take this — you've earned it.";
    [TextArea] public string doneLine = "Nothing more from me. Keep breathing.";

    [Header("Reward on turn-in")]
    public float rewardHealth = 25f;
    public float rewardArmor = 25f;
    public bool rewardAmmo = true;
    public int rewardWeaponIndex = -1;             // -1 = no weapon unlock

    [Header("Contact chain")]
    public string introducesContact = "";          // auto-discovered on turn-in

    [Header("Discovery")]
    public float autoDiscoverRange = 22f;          // walking past is enough
    public float talkRange = 3.2f;                 // matches PlayerUse's cone sweep

    [Header("On accept — the mission kicks the level off")]
    public GameObject[] activateOnAccept;          // dormant squads, spawners, hazards
    public G1BlastDoor[] openOnAccept;             // gates that grind open on the word
    public bool alarmOnAccept;

    /// Every quest NPC alive in the scene — the scanner's search space.
    public static readonly List<G1QuestNpc> All = new List<G1QuestNpc>();

    public Stage stage { get; private set; } = Stage.Available;
    public bool discovered { get; private set; }
    public G1NpcProfile profile { get; private set; }

    bool offering;
    string fullLine = "", shownLine = "";
    int charIdx;
    float nextChar, lineUntil = -1f;
    float charInterval = 0.018f;
    float nextPoll;
    Transform player;
    Light beacon;
    GameObject targetWaypoint;
    Font font;
    G1Voice voice;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        profile = G1NpcRoster.GetProfile(role);
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        var p = GameObject.FindWithTag("Player");
        if (p) player = p.transform;

        // one voice per contact, coloured by their role — self-installing so a
        // hand-placed NPC in any scene speaks without extra wiring
        voice = GetComponent<G1Voice>();
        if (voice == null) voice = gameObject.AddComponent<G1Voice>();
        voice.pitch = profile.voicePitch;
        voice.wobble = profile.voiceWobble;
        voice.lettersPerBlip = profile.voiceRate;

        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.8f; col.radius = 0.4f; col.center = new Vector3(0f, 0.9f, 0f);
        }

        var bgo = new GameObject("Beacon");
        bgo.transform.SetParent(transform, false);
        bgo.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        beacon = bgo.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = profile.beacon;
        beacon.range = 9f;
    }

    void Update()
    {
        // beacon breathes while there's something to collect, steadies otherwise
        if (beacon != null)
        {
            bool wants = stage == Stage.Available || stage == Stage.ReadyToTurnIn;
            beacon.color = MarkerColor();
            beacon.intensity = wants ? 1.5f + Mathf.PingPong(Time.time * 2f, 1.1f) : 0.7f;
        }

        if (!discovered && player != null &&
            Vector3.Distance(player.position, transform.position) <= autoDiscoverRange)
            Discover(true);

        if (charIdx < fullLine.Length && Time.time >= nextChar)
        {
            char c = fullLine[charIdx++];
            shownLine += c;
            if (voice != null) voice.Letter(c);   // they speak as the text lands
            nextChar = Time.time + charInterval;
        }

        // has the assignment finished out in the world?
        if (stage == Stage.Active && Time.time >= nextPoll)
        {
            nextPoll = Time.time + 0.25f;
            var om = G1ObjectiveManager.Instance;
            var obj = om != null ? om.objectives.Find(o => o.id == questId) : null;
            if (obj != null && obj.isCompleted)
            {
                stage = Stage.ReadyToTurnIn;
                if (targetWaypoint) Destroy(targetWaypoint);
                G1Audio.Play2D("pickup", 0.5f, 1.6f);
            }
        }

        // Walking away closes the brief — but only properly away. At 1.6x the
        // talk range a single step back cancelled the offer mid-read, which
        // felt like the dialogue breaking rather than the player leaving.
        if (offering && (player == null ||
            Vector3.Distance(player.position, transform.position) > talkRange * 3f))
            offering = false;

        if (offering && Input.GetKeyDown(KeyCode.X))
        {
            offering = false;
            Say("Suit yourself. Offer stands if you change your mind.");
        }
    }

    /// Called by the scanner (or proximity) the first time this contact is
    /// picked up. Returns true only on the transition, so callers can chirp.
    public bool Discover(bool silent = false)
    {
        if (discovered) return false;
        discovered = true;
        if (!silent) G1Audio.Play2D("pickup", 0.35f, 1.9f);
        return true;
    }

    public void OnUse(GameObject user)
    {
        switch (stage)
        {
            case Stage.Available:
                Discover(true);
                if (!offering) { offering = true; Say(offerLine); }
                else Accept(user);
                break;

            case Stage.Active:
                Say(nagLine);
                break;

            case Stage.ReadyToTurnIn:
                TurnIn(user);
                break;

            case Stage.Done:
                Say(doneLine);
                break;
        }
    }

    void Accept(GameObject user)
    {
        offering = false;
        stage = Stage.Active;

        var om = G1ObjectiveManager.Instance;
        if (om != null && !string.IsNullOrEmpty(questId) &&
            om.objectives.Find(o => o.id == questId) == null)
            om.AddObjective(questId, questTitle, mandatory, Mathf.Max(1, requiredCount));

        if (hasQuestTarget)
        {
            targetWaypoint = new GameObject("QuestWaypoint_" + questId);
            targetWaypoint.transform.position = questTarget;
            var wp = targetWaypoint.AddComponent<G1Waypoint>();
            wp.objectiveId = questId;
            wp.label = targetLabel.ToUpper();
            wp.offset = Vector3.up * 2f;
        }

        Say(acceptLine);
        G1Audio.Play2D("pickup", 0.8f, 1.2f);

        // the word goes out: gates open, dormant squads wake, the level starts
        if (openOnAccept != null)
            foreach (var door in openOnAccept)
                if (door != null) { door.Unlock(); door.Open(); }

        if (activateOnAccept != null)
            foreach (var go in activateOnAccept)
                if (go != null) go.SetActive(true);

        if (alarmOnAccept)
            G1Audio.Play("alarm_siren", transform.position, 0.85f, 1f);
    }

    void TurnIn(GameObject user)
    {
        stage = Stage.Done;
        Say(turnInLine);
        G1Audio.Play2D("pickup", 0.9f, 0.9f);

        var hs = user != null ? user.GetComponent<HealthSystem>() : null;
        if (hs != null)
        {
            if (rewardHealth > 0f) hs.Heal(rewardHealth);
            if (rewardArmor > 0f) hs.AddArmor(rewardArmor);
        }

        var switcher = user != null ? user.GetComponentInChildren<WeaponSwitcher>(true) : null;
        if (switcher != null)
        {
            if (rewardWeaponIndex >= 0) switcher.Unlock(rewardWeaponIndex);
            if (rewardAmmo) RefillReserves(switcher);
        }

        // hand the player their next lead — the chain is what keeps them moving
        if (!string.IsNullOrEmpty(introducesContact))
        {
            var next = All.Find(n => n != null && n.npcName == introducesContact);
            if (next != null && next.Discover(true))
                Say(turnInLine + "  Find " + next.npcName + " — " + next.district + ".");
        }
    }

    static void RefillReserves(WeaponSwitcher switcher)
    {
        if (switcher.weapons == null) return;
        foreach (var go in switcher.weapons)
        {
            if (go == null) continue;
            var w = go.GetComponent<WeaponBase>();
            if (w is G1Pistol pistol) pistol.reserve = Mathf.Min(pistol.reserve + 34, 68);
            else if (w is G1Smg smg) smg.reserve = Mathf.Min(smg.reserve + 100, 150);
            else if (w is G1Shotgun shotgun) shotgun.reserve = Mathf.Min(shotgun.reserve + 16, 24);
            else if (w is G1Magnum magnum) magnum.reserve = Mathf.Min(magnum.reserve + 12, 18);
        }
    }

    void Say(string text)
    {
        fullLine = text; shownLine = ""; charIdx = 0;
        nextChar = Time.time;
        lineUntil = Time.time + (offering ? 30f : 7f);

        charInterval = 0.018f;
        if (voice != null)
        {
            voice.Begin(text);
            // When the line is actually spoken, the text has to land at the
            // speed they say it — a typewriter running at its own fixed rate
            // finishes half a sentence early and reads as badly dubbed.
            if (voice.SpokenLength > 0.05f && text.Length > 0)
            {
                charInterval = voice.SpokenLength / text.Length;
                lineUntil = Time.time + voice.SpokenLength + 2.5f;
            }
        }
    }

    /// The glyph the scanner paints over this contact's head.
    public string MarkerGlyph()
    {
        switch (stage)
        {
            case Stage.Available: return "!";
            case Stage.Active: return "·";
            case Stage.ReadyToTurnIn: return "?";
            default: return "✓";
        }
    }

    public Color MarkerColor()
    {
        switch (stage)
        {
            case Stage.Available: return profile.beacon;
            case Stage.Active: return new Color(0.6f, 0.6f, 0.62f);
            case Stage.ReadyToTurnIn: return new Color(0.3f, 0.95f, 0.95f);
            default: return new Color(0.4f, 0.8f, 0.45f);
        }
    }

    public string StageText()
    {
        switch (stage)
        {
            case Stage.Available: return "HAS WORK";
            case Stage.Active: return "IN PROGRESS";
            case Stage.ReadyToTurnIn: return "REPORT BACK";
            default: return "COMPLETE";
        }
    }

    void OnGUI()
    {
        if (player == null) return;
        float dist = Vector3.Distance(player.position, transform.position);

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (font) style.font = font;

        // "PRESS E" prompt while standing with them
        if (dist <= talkRange && !offering)
        {
            string verb = stage == Stage.ReadyToTurnIn ? "REPORT TO" :
                          stage == Stage.Done ? "TALK TO" : "SPEAK WITH";
            style.normal.textColor = new Color(MarkerColor().r, MarkerColor().g, MarkerColor().b, 0.95f);
            GUI.Label(new Rect(Screen.width / 2f - 250f, Screen.height * 0.6f, 500f, 30f),
                      $"[ PRESS E TO {verb} {npcName} ]", style);
        }

        // the brief: accept or walk away
        if (offering)
        {
            var body = new GUIStyle(GUI.skin.label)
            { fontSize = 17, alignment = TextAnchor.UpperLeft, wordWrap = true };
            if (font) body.font = font;
            // tall enough for the whole brief, and clear of the V.I. comms line
            // that the mission assistant draws at Screen.height - 150
            float w = 780f, h = 210f;
            float x = Screen.width / 2f - w / 2f, y = Screen.height - 400f;

            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            body.normal.textColor = MarkerColor();
            GUI.Label(new Rect(x + 14f, y + 8f, w - 28f, 24f),
                      $"{npcName}  —  {profile.title}", body);
            body.normal.textColor = new Color(0.92f, 0.9f, 0.85f);
            GUI.Label(new Rect(x + 14f, y + 36f, w - 28f, 130f), shownLine, body);

            var foot = new GUIStyle(body) { fontSize = 16 };
            foot.normal.textColor = new Color(1f, 0.75f, 0.2f);
            GUI.Label(new Rect(x + 14f, y + h - 30f, w - 28f, 24f),
                      $"ASSIGNMENT: {questTitle}      [E] ACCEPT      [X] DECLINE", foot);
        }
        // ordinary spoken line
        else if (Time.time < lineUntil && !string.IsNullOrEmpty(shownLine))
        {
            float alpha = Mathf.Clamp01(lineUntil - Time.time);
            var body = new GUIStyle(GUI.skin.label)
            { fontSize = 17, alignment = TextAnchor.UpperLeft, wordWrap = true };
            if (font) body.font = font;
            float w = 780f, x = Screen.width / 2f - w / 2f, y = Screen.height - 220f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(new Rect(x, y, w, 62f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var c = MarkerColor(); c.a = alpha;
            body.normal.textColor = c;
            GUI.Label(new Rect(x + 12f, y + 6f, w - 24f, 52f), $"{npcName}:  {shownLine}", body);
        }
    }
}
