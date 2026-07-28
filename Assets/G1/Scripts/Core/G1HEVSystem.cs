using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Complete retro HEV suit diagnostic and active support system.
/// Tracks health, armor, falling damage, and radiation hazards.
/// Automatically administers emergency morphine under 25 HP (gives HP + speed boost).
/// Draws diagnostic warnings in a typewriter amber console at the bottom of the screen.
public sealed class G1HEVSystem : MonoBehaviour
{
    public float morphineCooldown = 45f;
    public float morphineDuration = 6f;
    public float morphineHpGain = 20f;
    public float morphineSpeedMultiplier = 1.4f;

    private HealthSystem health;
    private PlayerMovement movement;

    private float nextMorphineTime = -1f;
    private float morphineEndTime = -1f;
    private float lastLowArmorAlertTime = -1f;
    private float lastRadAlertTime = -1f;
    private float lastArmorValue = 0f;

    // GUI/Text state
    private string fullAlertLine = "";
    private string shownAlertLine = "";
    private int charIdx;
    private float nextCharTime;
    private float alertUntil = -1f;
    private Font font;
    private GUIStyle consoleStyle;

    void Start()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        health = GetComponent<HealthSystem>();
        movement = GetComponent<PlayerMovement>();

        if (health != null)
        {
            lastArmorValue = health.Armor;
            health.OnHealthChanged += HandleHealthChanged;
            health.OnArmorChanged += HandleArmorChanged;
        }

        // Welcome alert on start
        Invoke(nameof(SayBootAlert), 2.5f);
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnArmorChanged -= HandleArmorChanged;
        }
    }

    void Update()
    {
        // Typewriter effect
        if (charIdx < fullAlertLine.Length && Time.time >= nextCharTime)
        {
            shownAlertLine += fullAlertLine[charIdx++];
            nextCharTime = Time.time + 0.02f;
        }

        // Handle morphine speed buff expiration
        if (morphineEndTime > 0f && Time.time >= morphineEndTime)
        {
            morphineEndTime = -1f;
            if (movement != null)
            {
                movement.speedModifier = 1.0f;
            }
            Say("Morphine auto-injection completed. Heart rate stabilizing.");
        }
    }

    private void SayBootAlert()
    {
        Say("HEV Suit diagnostics online. All systems nominal.");
    }

    public void Say(string message)
    {
        fullAlertLine = message;
        shownAlertLine = "";
        charIdx = 0;
        nextCharTime = Time.time;
        alertUntil = Time.time + 6.0f;

        // Play double-beep HEV suit chirp
        G1Audio.Play2D("pickup", 0.6f, 1.2f, 0f);
        Invoke(nameof(PlayChimePitch), 0.11f);
    }

    private void PlayChimePitch()
    {
        G1Audio.Play2D("pickup", 0.55f, 1.7f, 0f);
    }

    private void HandleHealthChanged(float cur, float max)
    {
        if (cur <= 0f) return;

        // Emergency Morphine Auto-injector trigger
        if (cur < 25f && Time.time >= nextMorphineTime && morphineEndTime < 0f)
        {
            nextMorphineTime = Time.time + morphineCooldown;
            morphineEndTime = Time.time + morphineDuration;

            if (health != null)
            {
                health.Heal(morphineHpGain);
            }
            if (movement != null)
            {
                movement.speedModifier = morphineSpeedMultiplier;
            }

            Say("WARNING: Vital signs critical! Morphine administered. Speed boosted.");
        }
    }

    private void HandleArmorChanged(float cur, float max)
    {
        if (cur < lastArmorValue)
        {
            // Took armor damage
            if (cur <= 0f && lastArmorValue > 0f)
            {
                Say("WARNING: HEV protective armor power depleted.");
            }
            else if (cur <= 15f && Time.time - lastLowArmorAlertTime > 25f)
            {
                lastLowArmorAlertTime = Time.time;
                Say("WARNING: Protective armor power critical. Recharge required.");
            }
        }
        lastArmorValue = cur;
    }

    public void TriggerFracture()
    {
        // Major bone fracture warning from hard landing
        Say("WARNING: Major bone fracture detected. Automated splint applied.");
        G1Audio.Play2D("alarm_siren", 0.5f, 1.8f, 0f);
    }

    public void TriggerRadiation()
    {
        // Radiation zone alert
        if (Time.time - lastRadAlertTime > 18f)
        {
            lastRadAlertTime = Time.time;
            Say("WARNING: Hazardous radiation level detected. Shielding active.");
        }
    }

    private bool IsCutsceneActive()
    {
        if (G1CutsceneManager.Instance != null && G1CutsceneManager.Instance.isCutsceneActive) return true;
        if (G1IntroStory.IsActive) return true;
        if (G1EndingCutscene.IsPlaying) return true;
        return false;
    }

    void OnGUI()
    {
        if (IsCutsceneActive() || Time.time >= alertUntil || string.IsNullOrEmpty(shownAlertLine))
            return;

        if (consoleStyle == null)
        {
            consoleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            if (font) consoleStyle.font = font;
        }

        float alpha = Mathf.Clamp01(alertUntil - Time.time);
        float w = 680;
        float cx = Screen.width / 2f - w / 2f;
        float y = Screen.height - 100; // Positioned below the main comms panel

        // Suit notification banner
        GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
        GUI.Box(new Rect(cx, y, w, 40), "", GUI.skin.box);
        GUI.color = Color.white;

        // Draw warning text in suit amber
        consoleStyle.normal.textColor = new Color(0.95f, 0.72f, 0.12f, alpha * 0.95f);
        GUI.Label(new Rect(cx + 10, y + 4, w - 20, 32), $"▲ HEV: {shownAlertLine}", consoleStyle);
    }
}
