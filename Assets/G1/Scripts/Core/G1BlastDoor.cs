using System.Collections;
using UnityEngine;

/// A heavy twin-panel bunker door: two slabs that grind apart along their own
/// local X. Unlike <see cref="SlidingDoor"/> it can be sealed — a locked door
/// answers E with a refusal and a red lamp, and only opens once something in
/// the world (a mission being accepted, a terminal, a quest) unlocks it.
///
/// The status lamp is the whole readout, so a player can tell from across the
/// plaza whether a door is worth walking to:
///   red    sealed — no power, or you haven't earned it yet
///   amber  closed but yours to open
///   green  open
[DisallowMultipleComponent]
public sealed class G1BlastDoor : MonoBehaviour, IUsable
{
    [Header("Panels")]
    public Transform leftPanel;
    public Transform rightPanel;
    public float travel = 1.7f;          // how far each panel retracts
    public float moveTime = 1.9f;        // heavy doors are slow on purpose

    [Header("State")]
    public bool locked;
    public string doorLabel = "BLAST DOOR";
    public string lockedMessage = "SEALED — NO POWER";

    [Header("Behaviour")]
    public bool autoProximity;           // once unlocked, opens as you walk up
    public bool openOnce;                // never closes again after opening
    public float promptRange = 3.4f;

    [Header("Status lamp")]
    public Light statusLight;

    public bool IsOpen { get; private set; }

    Vector3 leftClosed, rightClosed;
    bool moving;
    Transform player;
    Font font;
    float denyUntil = -1f;

    void Start()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        var p = GameObject.FindWithTag("Player");
        if (p) player = p.transform;
        if (leftPanel) leftClosed = leftPanel.localPosition;
        if (rightPanel) rightClosed = rightPanel.localPosition;
        Relamp();
    }

    void Update()
    {
        if (autoProximity && !locked && !IsOpen && !moving && player != null &&
            Vector3.Distance(player.position, transform.position) <= promptRange)
            Open();
    }

    public void Unlock()
    {
        if (!locked) return;
        locked = false;
        Relamp();
        G1Audio.Play("door_servo", transform.position, 0.5f, 1.6f);
    }

    public void Lock()
    {
        locked = true;
        Relamp();
    }

    /// Idempotent open — safe to call from triggers every frame.
    public void Open()
    {
        if (locked || moving || IsOpen) return;
        StartCoroutine(Drive(true));
    }

    public void Close()
    {
        if (moving || !IsOpen || openOnce) return;
        StartCoroutine(Drive(false));
    }

    public void OnUse(GameObject user)
    {
        if (locked)
        {
            denyUntil = Time.time + 2.5f;
            G1Audio.Play("hit_thunk", transform.position, 0.5f, 0.7f);
            return;
        }
        if (moving) return;
        if (IsOpen) Close(); else Open();
    }

    IEnumerator Drive(bool opening)
    {
        moving = true;
        G1Audio.Play("door_servo", transform.position, 0.9f, 0.55f);

        Vector3 lFrom = leftPanel ? leftPanel.localPosition : Vector3.zero;
        Vector3 rFrom = rightPanel ? rightPanel.localPosition : Vector3.zero;
        Vector3 lTo = leftClosed + (opening ? Vector3.left * travel : Vector3.zero);
        Vector3 rTo = rightClosed + (opening ? Vector3.right * travel : Vector3.zero);

        float t = 0f;
        while (t < moveTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / moveTime);
            if (leftPanel) leftPanel.localPosition = Vector3.Lerp(lFrom, lTo, k);
            if (rightPanel) rightPanel.localPosition = Vector3.Lerp(rFrom, rTo, k);
            yield return null;
        }
        if (leftPanel) leftPanel.localPosition = lTo;
        if (rightPanel) rightPanel.localPosition = rTo;

        IsOpen = opening;
        moving = false;
        Relamp();
    }

    void Relamp()
    {
        if (statusLight == null) return;
        statusLight.color = locked ? new Color(1f, 0.2f, 0.15f)
                          : IsOpen ? new Color(0.3f, 1f, 0.4f)
                                   : new Color(1f, 0.72f, 0.15f);
        statusLight.intensity = locked ? 1.6f : 2.2f;
    }

    void OnGUI()
    {
        if (player == null) return;
        bool near = Vector3.Distance(player.position, transform.position) <= promptRange;
        bool denying = Time.time < denyUntil;
        if (!near && !denying) return;

        var style = new GUIStyle(GUI.skin.label)
        { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (font) style.font = font;

        string text;
        if (locked)
        {
            style.normal.textColor = new Color(1f, 0.3f, 0.25f, 0.95f);
            text = $"[ {doorLabel}: {lockedMessage} ]";
        }
        else
        {
            style.normal.textColor = new Color(1f, 0.75f, 0.2f, 0.95f);
            text = IsOpen ? $"[ PRESS E TO CLOSE {doorLabel} ]"
                          : $"[ PRESS E TO OPEN {doorLabel} ]";
            if (autoProximity || (openOnce && IsOpen)) return;
        }

        GUI.Label(new Rect(Screen.width / 2f - 260f, Screen.height * 0.63f, 520f, 30f),
                  text, style);
    }
}
