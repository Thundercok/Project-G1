using UnityEngine;
using UnityEngine.SceneManagement;

/// Global difficulty (PlayerPrefs "G1_Difficulty": 0 = Casual Action, 1 = Easy, 2 = Normal).
/// High-octane balance: the player is NOT a bullet sponge; aggression is rewarded
/// with health & armor on kills.
///
/// On top of the mode setting, difficulty ramps by CAMPAIGN LEVEL so the game
/// starts gentle and gets harder each level (inferred from the active scene name,
/// so nothing needs wiring):
///   Level 1 (TestScene) — softest hits, generous regen, small hordes.
///   Level 2 (Quarantine) — harder hits, less regen, bigger hordes.
///   Level 3 (Threshold)  — full-strength hits, no regen, max pressure.
public static class G1Difficulty
{
    public static int Mode => PlayerPrefs.GetInt("G1_Difficulty", 0);

    public static bool Casual => Mode == 0;
    public static bool Easy => Mode == 1;

/// 1, 2, 3, or 4 based on the active scene.
    public static int Level
    {
        get
        {
            string n = SceneManager.GetActiveScene().name;
            if (n.Contains("HugeMap")) return 4;
            if (n.Contains("Level3")) return 3;
            if (n.Contains("Level2")) return 2;
            return 1;   // TestScene / Level 1 and any sandbox
        }
    }

    // Per-level scalars — demo-friendly: damage ramps gently so new players can reach the credits.
    static float LvlDamageFactor => Level >= 4 ? 1.00f : Level == 3 ? 0.90f : Level == 2 ? 0.80f : 0.70f;
    static float LvlHordeFactor  => Level >= 4 ? 1.25f : Level == 3 ? 1.1f  : Level == 2 ? 1.0f  : 0.9f;

    /// Damage the player receives: gentle enough that first-timers survive burst fire.
    public static float IncomingDamageMult =>
        (Casual ? 0.45f : Easy ? 0.55f : 0.65f) * LvlDamageFactor;

    /// Damage the player's weapons deal: punchy and satisfying — enemies die in 3-5 shots.
    public static float OutgoingDamageMult =>
        (Casual ? 2.4f : Easy ? 1.8f : 1.5f) * (Level >= 4 ? 1.25f : Level == 3 ? 1.2f : Level == 2 ? 1.15f : 1.25f);

    /// ThreatDirector pacing: generous relax windows so the player can breathe and explore.
    public static float RelaxDurationMult =>
        (Casual ? 3.0f : Easy ? 2.2f : 1.5f) * (Level == 1 ? 1.8f : Level == 2 ? 1.4f : Level == 3 ? 1.1f : 0.95f);
    public static float HordeSizeMult => (Casual ? 0.4f : Easy ? 0.6f : 0.8f) * LvlHordeFactor;
    public static int MaxSoldiersDelta => (Casual ? -3 : Easy ? -2 : -1) + (Level >= 4 ? 2 : Level == 3 ? 1 : 0);

    /// Health & Armor siphon on every kill — aggression keeps you alive.
    public static float KillHealthReward => Casual ? 25f : Easy ? 22f : 20f;
    public static float KillArmorReward  => Casual ? 18f : Easy ? 14f : 12f;

    /// Passive regen out of combat — prevents "stuck at 2 HP, no packs left" soft-locks.
    public static float RegenCeiling => Casual ? 70f : Easy ? 60f : 60f;

    public static string Name => Casual ? "CASUAL ACTION" : Easy ? "TACTICAL EASY" : "NORMAL";
}
