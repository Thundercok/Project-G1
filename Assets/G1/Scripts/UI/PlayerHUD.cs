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
    WeaponSwitcher switcher;
    CameraEffects camFX;
    Texture2D vignetteTex;

    Font _hudFont;
    string _pickupWeaponName;
    float _pickupDisplayUntil;

    string _terminalLogMessage;
    float _terminalLogDisplayUntil;

    void Start()
    {
        playerHealth = GetComponent<HealthSystem>();
        switcher = GetComponentInChildren<WeaponSwitcher>();
        camFX = GetComponentInChildren<CameraEffects>();
        vignetteTex = MakeVignette();

        // Load Share Tech Mono
        var fontAsset = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        if (fontAsset != null) _hudFont = fontAsset;
    }

    public void ShowWeaponPickup(string weaponName)
    {
        _pickupWeaponName = weaponName.ToUpper();
        _pickupDisplayUntil = Time.time + 2f;
    }

    public void ShowTerminalLog(string msg)
    {
        _terminalLogMessage = msg;
        _terminalLogDisplayUntil = Time.time + 5f;
    }

    void OnGUI()
    {
        // 1. Draw Crosshair in center
        if (drawCrosshair && Cursor.lockState == CursorLockMode.Locked)
        {
            DrawCrosshair();
        }

        // Draw HUD elements with drop shadows for legibility
        DrawHealthHUD();
        DrawAmmoHUD();
        DrawWeaponPickup();
        DrawTerminalLog();

        // Hit marker
        if (camFX && camFX.HitMarkerActive)
            DrawHitMarker();

        // Damage vignette
        if (camFX && camFX.DamageFlashAlpha > 0.01f)
            DrawDamageVignette(camFX.DamageFlashAlpha);
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
        string hpText = $"+  {hp}";

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

        style.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(42, Screen.height - 78, 250, 60), hpText, style);
        style.normal.textColor = hpColor;
        GUI.Label(new Rect(40, Screen.height - 80, 250, 60), hpText, style);
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
        GUI.Label(new Rect(Screen.width/2f - 300 + 1, Screen.height - 180 + 1, 600, 80), _terminalLogMessage, style);
        // Draw text (retro green/cyan terminal look)
        style.normal.textColor = new Color(0.2f, 0.9f, 0.6f, alpha);
        GUI.Label(new Rect(Screen.width/2f - 300, Screen.height - 180, 600, 80), _terminalLogMessage, style);
    }
}
