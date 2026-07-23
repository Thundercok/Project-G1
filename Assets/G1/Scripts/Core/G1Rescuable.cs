using UnityEngine;

/// A stranded teammate. Approach and press E to free them; each rescue counts
/// toward a mission objective. Shows a "PRESS E" prompt when the player is near,
/// carries a beacon light + objective waypoint so the tracker can point to it,
/// and stands the survivor up (then evacuates) on rescue.
public sealed class G1Rescuable : MonoBehaviour, IUsable
{
    public string objectiveId = "rescue";
    public float promptRange = 3.2f;

    bool rescued;
    Transform player;
    Light beacon;
    G1Waypoint waypoint;
    Font font;

    void Start()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        var p = GameObject.FindWithTag("Player");
        if (p) player = p.transform;

        // ensure something the +use ray can hit
        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.8f; col.radius = 0.4f; col.center = new Vector3(0, 0.9f, 0);
        }

        // teal distress beacon overhead
        var bgo = new GameObject("Beacon");
        bgo.transform.SetParent(transform, false);
        bgo.transform.localPosition = new Vector3(0, 2.4f, 0);
        beacon = bgo.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = new Color(0.2f, 0.9f, 0.9f);
        beacon.range = 10f; beacon.intensity = 2.2f;

        waypoint = gameObject.AddComponent<G1Waypoint>();
        waypoint.objectiveId = objectiveId;
        waypoint.label = "SURVIVOR";
        waypoint.offset = Vector3.up * 2.4f;
    }

    void Update()
    {
        if (rescued || beacon == null) return;
        beacon.intensity = 1.6f + Mathf.PingPong(Time.time * 2f, 1.2f);   // pulse
    }

    public void OnUse(GameObject user)
    {
        if (rescued) return;
        rescued = true;
        if (G1ObjectiveManager.Instance != null)
            G1ObjectiveManager.Instance.IncrementProgress(objectiveId);
        G1Audio.Play2D("pickup", 0.8f, 1.2f);
        if (beacon) beacon.enabled = false;
        if (waypoint) waypoint.enabled = false;   // unregisters from tracker
        // the freed survivor evacuates
        Destroy(gameObject, 2.5f);
    }

    void OnGUI()
    {
        if (rescued || player == null) return;
        if (Vector3.Distance(player.position, transform.position) > promptRange)
            return;
        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (font) style.font = font;
        style.normal.textColor = new Color(0.16f, 0.85f, 0.85f, 0.95f);
        GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height * 0.6f, 400, 30),
                  "[ PRESS E TO FREE SURVIVOR ]", style);
    }
}

/// Visual extraction gate: a ring that stays dim until every mandatory
/// objective is complete, then flares to signal it is active. The actual
/// scene-load is handled by a sibling G1LevelExitTrigger (which already gates
/// on objective completion).
public sealed class G1TeleportGate : MonoBehaviour
{
    public Renderer[] ringRenderers;
    bool online;

    void Update()
    {
        if (online) return;
        var om = G1ObjectiveManager.Instance;
        if (om == null || !om.IsLevelComplete()) return;
        online = true;
        foreach (var r in ringRenderers)
        {
            if (r == null) continue;
            var m = r.sharedMaterial;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.2f, 1f, 0.9f) * 2.5f);
        }
        G1Audio.Play("door_servo", transform.position, 0.9f, 1.3f);
    }
}
