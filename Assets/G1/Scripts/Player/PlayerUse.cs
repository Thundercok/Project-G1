using UnityEngine;

/// HL-style +use: press E while looking at something usable within reach.
///
/// Aim is deliberately forgiving. A pixel-perfect ray made talking to an NPC or
/// hitting a door panel fiddly, so this tries three widening passes and takes
/// the first hit: a centre ray, then a fat spherecast, then a short-range sweep
/// of everything usable inside a cone in front of the player. The cone pass is
/// what lets you press E at a person's chest, feet or shoulder and still talk.
public class PlayerUse : MonoBehaviour
{
    public Camera viewCamera;
    public float reach = 3.6f;
    public float castRadius = 0.4f;      // fat-ray forgiveness
    public float coneAngle = 62f;        // half-angle of the fallback sweep

    // OverlapSphereNonAlloc stops once the buffer is full and gives no warning
    // that it truncated. At 32 it filled with map chunks, barrier panels and
    // supply crates before it ever reached the person standing in front of you,
    // so E silently found nothing in exactly the crowded places you most want
    // to talk to someone. Sized for the worst corner of the sprawl.
    static readonly Collider[] buf = new Collider[192];

    GUIStyle _promptStyle;

    void OnGUI()
    {
        // Only query physics during repaint; OnGUI is called several times per frame.
        if (Event.current.type != EventType.Repaint || viewCamera == null)
            return;
        if (G1CutsceneManager.Instance != null && G1CutsceneManager.Instance.isCutsceneActive)
            return;
        if (G1MobSpawnerToolbox.IsOpen || FindUsable() == null)
            return;

        if (_promptStyle == null)
        {
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            var font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
            if (font != null) _promptStyle.font = font;
            _promptStyle.normal.textColor = Color.white;
        }

        const float width = 250f;
        const float height = 42f;
        var rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.66f, width, height);
        var oldColor = GUI.color;
        GUI.color = new Color(0.01f, 0.03f, 0.04f, 0.9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(0.16f, 0.75f, 0.75f, 0.9f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
        GUI.color = oldColor;
        GUI.Label(rect, "[ E ]  INTERACT", _promptStyle);
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E))
            return;
        IUsable usable = FindUsable();
        usable?.OnUse(gameObject);
    }

    /// Exposed so the self-test can assert what an E press would have hit.
    public IUsable FindUsable()
    {
        Vector3 eye = viewCamera.transform.position;
        Vector3 fwd = viewCamera.transform.forward;

        // 1. precise ray — always wins, so you can pick one of two adjacent
        //    terminals by looking straight at it
        if (Physics.Raycast(new Ray(eye, fwd), out RaycastHit hit, reach))
        {
            var direct = hit.collider.GetComponentInParent<IUsable>();
            if (direct != null) return direct;
        }

        // 2. fat ray — forgives being a few degrees off
        if (Physics.SphereCast(eye, castRadius, fwd, out RaycastHit fat, reach,
                               ~0, QueryTriggerInteraction.Ignore))
        {
            var near = fat.collider.GetComponentInParent<IUsable>();
            if (near != null) return near;
        }

        // 3. cone sweep — anything usable you're roughly facing and standing next to
        IUsable best = null;
        float bestScore = float.MaxValue;
        int n = Physics.OverlapSphereNonAlloc(transform.position, reach + 0.6f, buf,
                                              ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var candidate = buf[i].GetComponentInParent<IUsable>();
            if (candidate == null) continue;

            Vector3 to = buf[i].bounds.center - eye;
            float angle = Vector3.Angle(fwd, to);
            if (angle > coneAngle) continue;

            // don't let the generous radius reach through a wall or a shut door
            if (Physics.Raycast(eye, to.normalized, out RaycastHit block, to.magnitude,
                                ~0, QueryTriggerInteraction.Ignore) &&
                block.collider.GetComponentInParent<IUsable>() != candidate)
                continue;

            // prefer what's most centred, then what's closest
            float score = angle + to.magnitude * 4f;
            if (score < bestScore) { bestScore = score; best = candidate; }
        }
        return best;
    }
}
