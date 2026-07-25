using UnityEngine;

/// <summary>
/// Retro-styled 6-slot radial Weapon Wheel with slow-motion time dilation.
/// Activated by holding TAB key. Replaces bottom weapon slot bar.
/// Uses mouse position + raw mouse deltas for smooth selection.
/// </summary>
public class G1WeaponWheel : MonoBehaviour
{
    public bool isOpen;

    static readonly string[] WeaponNames = {
        "CROWBAR", "PISTOL", "SHOTGUN", "SMG", "MAGNUM", "GRENADE"
    };

    WeaponSwitcher switcher;
    Font hudFont;
    int hoveredSlot = -1;
    int lastHoveredSlot = -1;
    Vector2 virtualMousePos = Vector2.zero;

    static readonly Color Teal = new Color(0.16f, 0.75f, 0.75f);
    static readonly Color DimTeal = new Color(0.16f, 0.75f, 0.75f, 0.35f);
    static readonly Color HighlightFill = new Color(0.16f, 0.75f, 0.75f, 0.45f);
    static readonly Color LockedColor = new Color(0.35f, 0.35f, 0.35f, 0.4f);

    WeaponSwitcher GetSwitcher()
    {
        if (switcher == null)
            switcher = GetComponentInChildren<WeaponSwitcher>();
        if (switcher == null)
            switcher = FindFirstObjectByType<WeaponSwitcher>();
        return switcher;
    }

    void Start()
    {
        GetSwitcher();
        var fontAsset = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        if (fontAsset != null) hudFont = fontAsset;
    }

    void Update()
    {
        if (G1CutsceneManager.Instance != null && G1CutsceneManager.Instance.isCutsceneActive)
        {
            CloseWheel();
            return;
        }

        bool tabHeld = Input.GetKey(KeyCode.Tab);

        if (tabHeld && !isOpen)
        {
            OpenWheel();
        }
        else if (!tabHeld && isOpen)
        {
            ConfirmSelectionAndClose();
        }
    }

    void OpenWheel()
    {
        isOpen = true;
        virtualMousePos = Vector2.zero;
        Time.timeScale = 0.15f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        G1Audio.Play2D("pickup", 0.3f, 1.4f);
    }

    void ConfirmSelectionAndClose()
    {
        var sw = GetSwitcher();
        if (sw != null && hoveredSlot >= 0 && hoveredSlot < sw.weapons.Length)
        {
            if (sw.IsUnlocked(hoveredSlot))
            {
                sw.Select(hoveredSlot);
                G1Audio.Play2D("pickup", 0.5f, 1.6f);
            }
        }

        CloseWheel();
    }

    void CloseWheel()
    {
        if (!isOpen) return;
        isOpen = false;
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        CloseWheel();
    }

    public void DrawWheel()
    {
        if (!isOpen) return;

        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;

        // Dark dim overlay
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        // Accumulate mouse deltas for ultra-smooth radial selection
        float dx = Input.GetAxisRaw("Mouse X");
        float dy = Input.GetAxisRaw("Mouse Y");
        if (Mathf.Abs(dx) > 0.01f || Mathf.Abs(dy) > 0.01f)
        {
            virtualMousePos += new Vector2(dx * 18f, -dy * 18f);
            virtualMousePos = Vector2.ClampMagnitude(virtualMousePos, 220f);
        }

        Vector2 dir = virtualMousePos;
        if (dir == Vector2.zero && Event.current != null)
        {
            Vector2 mp = Event.current.mousePosition;
            if (mp != Vector2.zero)
                dir = mp - new Vector2(cx, cy);
        }

        float dist = dir.magnitude;
        int count = 6;
        float radius = 160f;
        float innerRadius = 25f;

        if (dist > innerRadius)
        {
            float angle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            float segmentAngle = 360f / count;
            float shiftedAngle = (angle + segmentAngle / 2f) % 360f;
            hoveredSlot = Mathf.FloorToInt(shiftedAngle / segmentAngle);
        }
        else
        {
            hoveredSlot = -1;
        }

        var sw = GetSwitcher();

        if (hoveredSlot != lastHoveredSlot && hoveredSlot >= 0)
        {
            if (sw != null && sw.IsUnlocked(hoveredSlot))
            {
                G1Audio.Play2D("pickup", 0.15f, 1.9f);
            }
            lastHoveredSlot = hoveredSlot;
        }

        var centerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            font = hudFont
        };

        // Draw ring segments
        for (int i = 0; i < count; i++)
        {
            float segAngle = (i * 60f - 90f) * Mathf.Deg2Rad;
            float slotX = cx + Mathf.Cos(segAngle) * radius - 70f;
            float slotY = cy + Mathf.Sin(segAngle) * radius - 25f;

            Rect slotRect = new Rect(slotX, slotY, 140f, 50f);
            bool isSelected = (i == hoveredSlot);
            bool isUnlocked = sw != null && sw.IsUnlocked(i);
            bool isActive = false;
            if (sw != null && sw.weapons != null && i < sw.weapons.Length)
            {
                if (sw.weapons[i] != null && sw.weapons[i].activeSelf)
                    isActive = true;
            }

            Color fill = isSelected ? (isUnlocked ? HighlightFill : new Color(0.2f, 0.05f, 0.05f, 0.5f))
                       : isActive ? new Color(Teal.r, Teal.g, Teal.b, 0.25f)
                       : isUnlocked ? new Color(0.02f, 0.04f, 0.06f, 0.65f)
                       : new Color(0.01f, 0.02f, 0.03f, 0.4f);

            Color border = isSelected ? (isUnlocked ? Teal : Color.red)
                         : isActive ? Teal
                         : isUnlocked ? DimTeal
                         : LockedColor;

            DrawPanel(slotRect, fill, border, isSelected ? 2f : 1f);

            var itemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = isSelected ? 15 : 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                font = hudFont
            };

            if (isUnlocked)
            {
                itemStyle.normal.textColor = isSelected ? Color.white : (isActive ? Teal : new Color(0.8f, 0.9f, 0.95f, 0.8f));
                string text = $"[{i + 1}] {WeaponNames[i]}";
                GUI.Label(slotRect, text, itemStyle);
            }
            else
            {
                itemStyle.normal.textColor = LockedColor;
                GUI.Label(slotRect, $"[{i + 1}] LOCKED", itemStyle);
            }
        }

        // Draw Center Circle Title
        Rect centerRect = new Rect(cx - 70f, cy - 35f, 140f, 70f);
        DrawPanel(centerRect, new Color(0.02f, 0.04f, 0.06f, 0.85f), Teal, 1.5f);

        centerStyle.normal.textColor = Teal;
        if (hoveredSlot >= 0 && sw != null && sw.IsUnlocked(hoveredSlot))
        {
            GUI.Label(new Rect(cx - 70f, cy - 22f, 140f, 25f), WeaponNames[hoveredSlot], centerStyle);
            var subStyle = new GUIStyle(centerStyle) { fontSize = 12 };
            subStyle.normal.textColor = new Color(1f, 0.8f, 0.2f, 0.9f);
            GUI.Label(new Rect(cx - 70f, cy + 2f, 140f, 20f), "RELEASE TO SELECT", subStyle);
        }
        else
        {
            GUI.Label(new Rect(cx - 70f, cy - 18f, 140f, 25f), "WEAPONS", centerStyle);
            var subStyle = new GUIStyle(centerStyle) { fontSize = 11 };
            subStyle.normal.textColor = DimTeal;
            GUI.Label(new Rect(cx - 70f, cy + 6f, 140f, 20f), "[HOLD TAB]", subStyle);
        }
    }

    static void DrawPanel(Rect r, Color fill, Color border, float bw = 1f)
    {
        Texture2D t = Texture2D.whiteTexture;
        Color old = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(r, t);
        GUI.color = border;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, bw), t);
        GUI.DrawTexture(new Rect(r.x, r.yMax - bw, r.width, bw), t);
        GUI.DrawTexture(new Rect(r.x, r.y, bw, r.height), t);
        GUI.DrawTexture(new Rect(r.xMax - bw, r.y, bw, r.height), t);
        GUI.color = old;
    }
}
