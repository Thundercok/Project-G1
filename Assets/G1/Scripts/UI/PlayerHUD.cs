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

    void Start()
    {
        playerHealth = GetComponent<HealthSystem>();
        switcher = GetComponentInChildren<WeaponSwitcher>();
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
}
