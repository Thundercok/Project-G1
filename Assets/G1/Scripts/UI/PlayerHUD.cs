using UnityEngine;

/// GoldSrc (Half-Life 1) style player HUD using OnGUI:
/// Green center crosshair, translucent amber health indicator on the bottom left,
/// and translucent amber active weapon ammo indicator on the bottom right.
public class PlayerHUD : MonoBehaviour
{
    [Header("Colors")]
    public Color hudColor = new Color(1f, 0.62f, 0.1f, 0.85f); // Translucent GoldSrc amber
    public Color crosshairColor = new Color(0.15f, 0.85f, 0.20f, 0.9f); // Translucent retro green

    [Header("Crosshair")]
    public bool drawCrosshair = true;
    public float crosshairSize = 12f;
    public float crosshairThickness = 2f;

    HealthSystem playerHealth;
    PlayerMovement playerMovement;
    WeaponSwitcher switcher;
    CameraEffects camFX;
    Texture2D vignetteTex;

    Font _hudFont;
    string _pickupWeaponName;
    float _pickupDisplayUntil;

    string _terminalLogMessage;
    string _displayedTerminalLog = "";
    float _terminalLogDisplayUntil;
    float _nextCharTime;
    int _charIndex;

    G1Flashlight flashlight;
    float _radFlashTime;

    float _objFlashTime;

    void Start()
    {
        playerHealth = GetComponent<HealthSystem>();
        playerMovement = GetComponent<PlayerMovement>();
        switcher = GetComponentInChildren<WeaponSwitcher>();
        camFX = GetComponentInChildren<CameraEffects>();
        vignetteTex = MakeVignette();
        flashlight = GetComponentInChildren<G1Flashlight>();

        // Ensure PlayerInventoryRestorer is present to apply campaign inventory carryover
        if (GetComponent<G1PlayerInventoryRestorer>() == null)
            gameObject.AddComponent<G1PlayerInventoryRestorer>();

        // Load Share Tech Mono
        var fontAsset = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        if (fontAsset != null) _hudFont = fontAsset;

        G1ObjectiveManager.OnObjectiveUpdated += HandleObjectiveUpdated;
    }

    void OnDestroy()
    {
        G1ObjectiveManager.OnObjectiveUpdated -= HandleObjectiveUpdated;
    }

    void HandleObjectiveUpdated(G1ObjectiveManager.Objective activeObj, bool isNewCompletion)
    {
        _objFlashTime = Time.time + 3f;
        if (isNewCompletion)
        {
            ShowTerminalLog("OBJECTIVE COMPLETED!");
            G1Audio.Play2D("pickup", 0.9f, 1.4f);
        }
        else if (activeObj != null)
        {
            ShowTerminalLog($"NEW OBJECTIVE: {activeObj.GetDisplayText().ToUpper()}");
            G1Audio.Play2D("pickup", 0.7f, 1.2f);
        }
    }

    public void ShowWeaponPickup(string weaponName)
    {
        _pickupWeaponName = weaponName.ToUpper();
        _pickupDisplayUntil = Time.time + 2f;
    }

    public void ShowTerminalLog(string msg)
    {
        _terminalLogMessage = msg;
        _displayedTerminalLog = "";
        _charIndex = 0;
        _nextCharTime = Time.time;
        _terminalLogDisplayUntil = Time.time + 6f;
    }

    private string _critText = "";
    private float _critDisplayUntil;
    private Color _critColor = Color.yellow;

    public void ShowCritFeedback(bool isHeadshot, bool isCrit, float damage)
    {
        if (isHeadshot)
        {
            _critText = $"💥 CRITICAL HEADSHOT! ({Mathf.CeilToInt(damage)})";
            _critColor = new Color(1f, 0.25f, 0.1f, 0.95f);
            _critDisplayUntil = Time.time + 1.2f;
            G1Audio.Play2D("pickup", 1.0f, 1.8f);
        }
        else if (isCrit)
        {
            _critText = $"⚡ CRITICAL HIT! ({Mathf.CeilToInt(damage)})";
            _critColor = new Color(1f, 0.85f, 0.1f, 0.95f);
            _critDisplayUntil = Time.time + 1.0f;
            G1Audio.Play2D("pickup", 0.8f, 1.5f);
        }
    }

    public void ShowRadWarning()
    {
        _radFlashTime = Time.time + 0.6f;
    }

    void OnGUI()
    {
        // During the opening story and the wake-up cutscene the screen is just
        // black with words — no health, no ammo, no crosshair, no gun.
        if (G1IntroStory.IsPlaying ||
            (G1CutsceneManager.Instance != null && G1CutsceneManager.Instance.isCutsceneActive))
            return;

        // 1. Draw Crosshair in center
        if (drawCrosshair && Cursor.lockState == CursorLockMode.Locked)
        {
            DrawCrosshair();
        }

        // Draw HUD elements with drop shadows for legibility
        DrawObjectiveHUD();
        DrawWaypointMarker();
        DrawHealthHUD();
        DrawAmmoHUD();
        DrawWeaponPickup();
        DrawTerminalLog();
        DrawCritFeedback();

        // Hit marker
        if (camFX && camFX.HitMarkerActive)
            DrawHitMarker();

        // Damage vignette
        if (camFX && camFX.DamageFlashAlpha > 0.01f)
            DrawDamageVignette(camFX.DamageFlashAlpha);
    }

    void DrawObjectiveHUD()
    {
        string text = G1ObjectiveManager.Instance != null 
            ? G1ObjectiveManager.Instance.GetActiveObjectiveText() 
            : null;

        if (string.IsNullOrEmpty(text)) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            font = _hudFont
        };

        Color textCol = Time.time < _objFlashTime 
            ? Color.Lerp(hudColor, new Color(0.2f, 1f, 0.8f), Mathf.PingPong(Time.time * 6f, 1f)) 
            : new Color(hudColor.r, hudColor.g, hudColor.b, 0.8f);

        string display = $"[!] OBJECTIVE: {text.ToUpper()}";

        // Shadow
        style.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(32, 28, 600, 35), display, style);

        // Main text
        style.normal.textColor = textCol;
        GUI.Label(new Rect(30, 26, 600, 35), display, style);
    }

    void DrawCrosshair()
    {
        float x = Screen.width / 2f;
        float y = Screen.height / 2f;
        Texture2D tex = Texture2D.whiteTexture;

        Color old = GUI.color;
        GUI.color = crosshairColor;

        // Horizontal line
        GUI.DrawTexture(new Rect(x - crosshairSize / 2f, y - crosshairThickness / 2f, crosshairSize, crosshairThickness), tex);
        // Vertical line
        GUI.DrawTexture(new Rect(x - crosshairThickness / 2f, y - crosshairSize / 2f, crosshairThickness, crosshairSize), tex);

        GUI.color = old;
    }

    void DrawHealthHUD()
    {
        if (playerHealth == null) return;
        int hp = Mathf.CeilToInt(playerHealth.CurrentHealth);
        if (hp < 0) hp = 0;
        string hpText = playerHealth.godMode 
            ? (playerMovement != null && playerMovement.IsFlying ? "+  GOD (FLY)" : "+  GOD") 
            : $"+  {hp}";

        // Pulse red when low HP
        Color hpColor = hp < 25
            ? Color.Lerp(hudColor, new Color(1f, 0.1f, 0.1f, 0.9f), Mathf.PingPong(Time.time * 3f, 1f))
            : hudColor;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerLeft,
            font = _hudFont
        };

        // Draw Toxic Hazard warning overlay if active
        if (Time.time < _radFlashTime)
        {
            var radStyle = new GUIStyle(style)
            {
                fontSize = 22,
                alignment = TextAnchor.LowerLeft
            };
            Color radColor = Color.Lerp(Color.green, Color.red, Mathf.PingPong(Time.time * 8f, 1f));
            
            // Shadow
            radStyle.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(42, Screen.height - 118, 200, 40), "[☢] TOXIC HAZARD", radStyle);
            // Main text
            radStyle.normal.textColor = radColor;
            GUI.Label(new Rect(40, Screen.height - 120, 200, 40), "[☢] TOXIC HAZARD", radStyle);
        }

        // Draw Health
        style.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(42, Screen.height - 78, 250, 60), hpText, style);
        style.normal.textColor = hpColor;
        GUI.Label(new Rect(40, Screen.height - 80, 250, 60), hpText, style);

        // Draw HEV armor (AP) meter to the right of health
        int ap = Mathf.CeilToInt(playerHealth.Armor);
        string apText = $"[|]  {ap}";
        var apStyle = new GUIStyle(style) { alignment = TextAnchor.LowerLeft };
        apStyle.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(342, Screen.height - 78, 250, 60), apText, apStyle);
        apStyle.normal.textColor = ap > 0
            ? new Color(0.3f, 0.7f, 1f, 0.9f) : new Color(0.4f, 0.5f, 0.6f, 0.5f);
        GUI.Label(new Rect(340, Screen.height - 80, 250, 60), apText, apStyle);

        // Draw Flashlight indicator if available
        if (flashlight != null)
        {
            string flText = $"FL  {Mathf.CeilToInt(flashlight.Battery)}%";
            Color flColor = flashlight.IsActive 
                ? (flashlight.Battery < 20f ? Color.red : hudColor)
                : new Color(hudColor.r, hudColor.g, hudColor.b, 0.4f);

            var flStyle = new GUIStyle(style)
            {
                fontSize = 20,
                alignment = TextAnchor.LowerLeft
            };
            
            // Shadow
            flStyle.normal.textColor = new Color(0f, 0f, 0f, 0.5f);
            GUI.Label(new Rect(182, Screen.height - 70, 180, 50), flText, flStyle);
            // Main text
            flStyle.normal.textColor = flColor;
            GUI.Label(new Rect(180, Screen.height - 72, 180, 50), flText, flStyle);
        }
    }

    void DrawAmmoHUD()
    {
        if (switcher == null || switcher.weapons == null) return;

        WeaponBase active = null;
        foreach (var w in switcher.weapons)
        {
            if (w != null && w.activeSelf)
            {
                active = w.GetComponent<WeaponBase>();
                break;
            }
        }
        if (active == null || !active.HasAmmo) return;

        string ammoText = active.IsReloading ? "RELOAD" : $"{active.Clip} | {active.Reserve}";

        // Red when low ammo
        Color ammoColor = (active.Clip <= 2 && !active.IsReloading)
            ? new Color(1f, 0.15f, 0.15f, 0.9f)
            : hudColor;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerRight,
            font = _hudFont
        };

        style.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(Screen.width - 298, Screen.height - 78, 250, 60), ammoText, style);
        style.normal.textColor = ammoColor;
        GUI.Label(new Rect(Screen.width - 300, Screen.height - 80, 250, 60), ammoText, style);
    }

    void DrawWeaponPickup()
    {
        if (string.IsNullOrEmpty(_pickupWeaponName)) return;
        float remaining = _pickupDisplayUntil - Time.time;
        if (remaining <= 0f) { _pickupWeaponName = null; return; }

        float alpha = Mathf.Clamp01(remaining);  // fade out last second
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            font = _hudFont
        };

        style.normal.textColor = new Color(0f, 0f, 0f, 0.6f * alpha);
        GUI.Label(new Rect(Screen.width/2f - 149, Screen.height/2f + 51, 300, 40),
                  $"PICKED UP {_pickupWeaponName}", style);
        style.normal.textColor = new Color(hudColor.r, hudColor.g, hudColor.b, alpha);
        GUI.Label(new Rect(Screen.width/2f - 150, Screen.height/2f + 50, 300, 40),
                  $"PICKED UP {_pickupWeaponName}", style);
    }

    void DrawHitMarker()
    {
        float x = Screen.width / 2f;
        float y = Screen.height / 2f;
        float gap = 6f, len = 8f, t = 2f;
        Texture2D tex = Texture2D.whiteTexture;

        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.9f);

        // 4 diagonal ticks forming an X
        // top-left
        GUI.DrawTexture(new Rect(x - gap - len, y - gap - t / 2f, len, t), tex);
        GUI.DrawTexture(new Rect(x - gap - t / 2f, y - gap - len, t, len), tex);
        // top-right
        GUI.DrawTexture(new Rect(x + gap, y - gap - t / 2f, len, t), tex);
        GUI.DrawTexture(new Rect(x + gap - t / 2f, y - gap - len, t, len), tex);
        // bottom-left
        GUI.DrawTexture(new Rect(x - gap - len, y + gap - t / 2f, len, t), tex);
        GUI.DrawTexture(new Rect(x - gap - t / 2f, y + gap, t, len), tex);
        // bottom-right
        GUI.DrawTexture(new Rect(x + gap, y + gap - t / 2f, len, t), tex);
        GUI.DrawTexture(new Rect(x + gap - t / 2f, y + gap, t, len), tex);

        GUI.color = old;
    }

    void DrawDamageVignette(float alpha)
    {
        if (vignetteTex == null) return;
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha * 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignetteTex);
        GUI.color = old;
    }

    /// Procedurally generate a red edge vignette texture.
    Texture2D MakeVignette()
    {
        int sz = 128;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float nx = (x / (float)sz) * 2f - 1f;
                float ny = (y / (float)sz) * 2f - 1f;
                float d = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                a *= a; // ease-in for softer edge
                tex.SetPixel(x, y, new Color(0.8f, 0.05f, 0.05f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    void DrawTerminalLog()
    {
        if (string.IsNullOrEmpty(_terminalLogMessage)) return;
        float remaining = _terminalLogDisplayUntil - Time.time;
        if (remaining <= 0f) { _terminalLogMessage = null; return; }

        // Typewriter character progression
        if (_charIndex < _terminalLogMessage.Length && Time.time >= _nextCharTime)
        {
            _displayedTerminalLog += _terminalLogMessage[_charIndex];
            _charIndex++;
            _nextCharTime = Time.time + 0.025f; // Typewriter speed
            // Very light digital beep/tick for each character printed
            G1Audio.Play2D("pickup", 0.03f, 1.8f);
        }

        float alpha = Mathf.Clamp01(remaining);
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            font = _hudFont,
            wordWrap = true
        };

        // Draw shadow
        style.normal.textColor = new Color(0f, 0f, 0f, 0.7f * alpha);
        GUI.Label(new Rect(Screen.width/2f - 300 + 1, Screen.height - 180 + 1, 600, 80), _displayedTerminalLog, style);
        // Draw text (retro green/cyan terminal look)
        style.normal.textColor = new Color(0.2f, 0.9f, 0.6f, alpha);
        GUI.Label(new Rect(Screen.width/2f - 300, Screen.height - 180, 600, 80), _displayedTerminalLog, style);
    }

    void DrawWaypointMarker()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        Vector3 targetPos = Vector3.zero;
        string label = "OBJECTIVE";
        bool hasTarget = false;

        var activeWp = G1ObjectiveManager.Instance != null ? G1ObjectiveManager.Instance.GetActiveWaypoint() : null;
        if (activeWp != null)
        {
            targetPos = activeWp.GetWorldPosition();
            label = activeWp.label;
            hasTarget = true;
        }
        else
        {
            // Fallback: look for G1LevelExitTrigger in scene
            var exit = Object.FindObjectOfType<G1LevelExitTrigger>();
            if (exit != null)
            {
                targetPos = exit.transform.position + Vector3.up * 1.2f;
                label = "EVAC EXIT";
                hasTarget = true;
            }
        }

        if (!hasTarget) return;

        float dist = Vector3.Distance(transform.position, targetPos);
        Vector3 screenPos = mainCam.WorldToScreenPoint(targetPos);

        // Position on GUI
        float guiX = screenPos.x;
        float guiY = Screen.height - screenPos.y;

        // If behind camera, clamp to bottom edge
        if (screenPos.z < 0)
        {
            guiX = Screen.width - guiX;
            guiY = Screen.height - 45f;
        }

        // Clamp to screen bounds with padding
        float pad = 50f;
        guiX = Mathf.Clamp(guiX, pad, Screen.width - pad);
        guiY = Mathf.Clamp(guiY, pad, Screen.height - pad);

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            font = _hudFont
        };

        string text = $"[◆ {label.ToUpper()} - {Mathf.CeilToInt(dist)}m]";

        // Drop shadow
        style.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
        GUI.Label(new Rect(guiX - 149, guiY - 14, 300, 30), text, style);

        // Marker color (glowing amber/gold)
        style.normal.textColor = new Color(1f, 0.75f, 0.2f, 0.95f);
        GUI.Label(new Rect(guiX - 150, guiY - 15, 300, 30), text, style);
    }

    void DrawCritFeedback()
    {
        if (string.IsNullOrEmpty(_critText)) return;
        float remaining = _critDisplayUntil - Time.time;
        if (remaining <= 0f) { _critText = null; return; }

        float alpha = Mathf.Clamp01(remaining * 2f);
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            font = _hudFont
        };

        // Shadow
        style.normal.textColor = new Color(0f, 0f, 0f, 0.7f * alpha);
        GUI.Label(new Rect(Screen.width / 2f - 249, Screen.height / 2f - 79, 500, 40), _critText, style);

        // Main colored text
        Color col = _critColor;
        col.a *= alpha;
        style.normal.textColor = col;
        GUI.Label(new Rect(Screen.width / 2f - 250, Screen.height / 2f - 80, 500, 40), _critText, style);
    }
}
