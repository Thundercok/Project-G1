using UnityEngine;

/// A trapped NPC who, once freed (press E), thanks you and HANDS YOU A NEW
/// QUEST — registering a fresh objective, pointing a waypoint at its target,
/// and letting the AI assistant announce it. The freed NPC then evacuates.
///
/// Set questTarget to a world position to drop a guiding waypoint (pair it with
/// a G1QuestZone there for a "reach the location" quest, or a G1ObjectiveOnDeath
/// on a target for a "destroy it" quest).
public sealed class G1QuestGiver : MonoBehaviour, IUsable
{
    [Header("NPC")]
    public string npcName = "SURVIVOR";
    public float promptRange = 3.2f;

    [Header("Rescue")]
    public string rescueObjectiveId = "rescue";   // incremented on free (optional)

    [Header("Quest handed out on rescue")]
    public string questId = "quest";
    public string questDescription = "Complete the assignment";
    public bool questMandatory = true;
    public int questCount = 1;
    public Vector3 questTarget;                    // waypoint location (optional)
    [TextArea] public string dialogue =
        "You came back for me! Listen — there's still work to do. Get moving.";

    bool freed;
    float dialogueUntil = -1f;
    string shown = "";
    int charIdx;
    float nextChar;
    Transform player;
    Light beacon;
    G1Waypoint beaconWp;
    Font font;

    void Start()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        var p = GameObject.FindWithTag("Player");
        if (p) player = p.transform;

        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.8f; col.radius = 0.4f; col.center = new Vector3(0, 0.9f, 0);
        }

        var bgo = new GameObject("Beacon");
        bgo.transform.SetParent(transform, false);
        bgo.transform.localPosition = new Vector3(0, 2.4f, 0);
        beacon = bgo.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = new Color(0.95f, 0.8f, 0.2f);   // amber = quest-giver
        beacon.range = 10f; beacon.intensity = 2.2f;

        beaconWp = gameObject.AddComponent<G1Waypoint>();
        beaconWp.objectiveId = rescueObjectiveId;
        beaconWp.label = "SURVIVOR";
        beaconWp.offset = Vector3.up * 2.4f;
    }

    void Update()
    {
        if (!freed && beacon != null)
            beacon.intensity = 1.6f + Mathf.PingPong(Time.time * 2f, 1.2f);

        if (charIdx < shown.Length && Time.time >= nextChar) { /* handled below */ }
        if (dialogueUntil > 0f && charIdx < dialogue.Length && Time.time >= nextChar)
        {
            shown += dialogue[charIdx++];
            nextChar = Time.time + 0.02f;
        }
    }

    public void OnUse(GameObject user)
    {
        if (freed) return;
        freed = true;

        var om = G1ObjectiveManager.Instance;
        if (om != null && !string.IsNullOrEmpty(rescueObjectiveId))
            om.IncrementProgress(rescueObjectiveId);

        // hand out the new quest
        if (om != null && !string.IsNullOrEmpty(questId))
        {
            om.AddObjective(questId, questDescription, questMandatory, Mathf.Max(1, questCount));
            if (questTarget != Vector3.zero)
            {
                var wpGo = new GameObject("QuestWaypoint_" + questId);
                wpGo.transform.position = questTarget;
                var wp = wpGo.AddComponent<G1Waypoint>();
                wp.objectiveId = questId;
                wp.label = questDescription.ToUpper();
                wp.offset = Vector3.up * 2f;
            }
        }

        // start the dialogue + voice
        shown = ""; charIdx = 0; nextChar = Time.time;
        dialogueUntil = Time.time + 7f;
        G1Audio.Play2D("pickup", 0.8f, 1.2f);

        if (beacon) beacon.enabled = false;
        if (beaconWp) beaconWp.enabled = false;
        Destroy(gameObject, 7f);   // evacuate after speaking
    }

    void OnGUI()
    {
        // "PRESS E" prompt while trapped
        if (!freed && player != null &&
            Vector3.Distance(player.position, transform.position) <= promptRange)
        {
            var ps = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (font) ps.font = font;
            ps.normal.textColor = new Color(0.95f, 0.8f, 0.2f, 0.95f);
            GUI.Label(new Rect(Screen.width / 2f - 220, Screen.height * 0.6f, 440, 30),
                      "[ PRESS E TO FREE SURVIVOR ]", ps);
        }

        // dialogue speech panel after being freed
        if (Time.time < dialogueUntil && !string.IsNullOrEmpty(shown))
        {
            float alpha = Mathf.Clamp01(dialogueUntil - Time.time);
            var box = new GUIStyle(GUI.skin.label)
            { fontSize = 17, alignment = TextAnchor.UpperLeft, wordWrap = true };
            if (font) box.font = font;
            float w = 760, x = Screen.width / 2f - w / 2f, y = Screen.height - 210;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(new Rect(x, y, w, 60), Texture2D.whiteTexture);
            GUI.color = Color.white;
            box.normal.textColor = new Color(0.95f, 0.82f, 0.3f, alpha);
            GUI.Label(new Rect(x + 12, y + 6, w - 24, 50), $"{npcName}:  {shown}", box);
        }
    }
}

/// A "reach this place" quest step: when the player enters, it completes (or
/// advances) the given objective. Harmless before the objective exists —
/// IncrementProgress on an unknown id is a no-op.
[RequireComponent(typeof(BoxCollider))]
public sealed class G1QuestZone : MonoBehaviour
{
    public string objectiveId;
    public int amount = 1;
    bool done;

    void Reset() { GetComponent<BoxCollider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (done || !other.CompareTag("Player")) return;
        var om = G1ObjectiveManager.Instance;
        if (om == null) return;
        // only fire once the objective actually exists (quest was handed out)
        if (om.objectives.Find(o => o.id == objectiveId) == null) return;
        done = true;
        om.IncrementProgress(objectiveId, amount);
    }
}
