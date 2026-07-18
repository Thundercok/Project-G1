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

    void Start()
    {
        playerHealth = GetComponent<HealthSystem>();
        switcher = GetComponentInChildren<WeaponSwitcher>();
        camFX = GetComponentInChildren<CameraEffects>();
        vignetteTex = MakeVignette();
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
        if (playerHealth == null)
            return;

        int hp = Mathf.CeilToInt(playerHealth.CurrentHealth);
        if (hp < 0) hp = 0;

        string hpText = $"+  {hp}"; // HL1-style icon representation '+' followed by value

        // Setup style
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerLeft
        };

        // Draw shadow
        style.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(42, Screen.height - 78, 250, 60), hpText, style);

        // Draw text
        style.normal.textColor = hudColor;
        GUI.Label(new Rect(40, Screen.height - 80, 250, 60), hpText, style);
    }

    void DrawAmmoHUD()
    {
        if (switcher == null || switcher.weapons == null)
            return;

        // Find active weapon
        WeaponBase active = null;
        foreach (var w in switcher.weapons)
        {
            if (w != null && w.activeSelf)
            {
                active = w.GetComponent<WeaponBase>();
                break;
            }
        }

        if (active == null || !active.HasAmmo)
            return;

        string ammoText = active.IsReloading ? "RELOAD" : $"{active.Clip} | {active.Reserve}";

        // Setup style
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerRight
        };

        // Draw shadow
        style.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(Screen.width - 298, Screen.height - 78, 250, 60), ammoText, style);

        // Draw text
        style.normal.textColor = hudColor;
        GUI.Label(new Rect(Screen.width - 300, Screen.height - 80, 250, 60), ammoText, style);
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
}
