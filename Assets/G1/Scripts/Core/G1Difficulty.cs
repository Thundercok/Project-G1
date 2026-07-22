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

    /// 1, 2, or 3 based on the active scene.
    public static int Level
    {
        get
        {
            string n = SceneManager.GetActiveScene().name;
            if (n.Contains("Level3")) return 3;
            if (n.Contains("Level2")) return 2;
            return 1;   // TestScene / Level 1 and any sandbox
        }
    }

    // Per-level scalars. Level 1 = easiest, Level 3 = hardest.
    static float LvlDamageFactor => Level == 3 ? 1.6f : Level == 2 ? 1.3f : 1.0f;
    static float LvlHordeFactor  => Level == 3 ? 1.6f : Level == 2 ? 1.25f : 1.0f;

    /// Damage the player receives: crisp & dangerous, ramped up by level.
    public static float IncomingDamageMult =>
        (Casual ? 0.6f : Easy ? 0.8f : 1.0f) * LvlDamageFactor;

    /// Damage the player's weapons deal (high impact & crisp execution).
    public static float OutgoingDamageMult =>
        (Casual ? 2.0f : Easy ? 1.4f : 1.0f) * (Level == 3 ? 0.9f : Level == 2 ? 0.95f : 1.1f);

    /// ThreatDirector pacing: manageable hordes/soldier limits, eased on lower levels.
    public static float RelaxDurationMult =>
        (Casual ? 2.5f : Easy ? 1.8f : 1f) * (Level == 1 ? 1.4f : Level == 2 ? 1.0f : 0.7f);
    public static float HordeSizeMult => (Casual ? 0.5f : Easy ? 0.75f : 1f) * LvlHordeFactor;
    public static int MaxSoldiersDelta => (Casual ? -2 : Easy ? -1 : 0) + (Level == 3 ? 1 : 0);

    /// Health & Armor siphon granted directly on every enemy kill.
    public static float KillHealthReward => Casual ? 15f : Easy ? 10f : 5f;
    public static float KillArmorReward => Casual ? 10f : Easy ? 5f : 0f;

    /// Passive regen when out of combat — generous early, gone by Level 3.
    public static float RegenCeiling =>
        Level >= 3 ? 0f
      : Level == 2 ? (Casual ? 40f : Easy ? 25f : 0f)
      :              (Casual ? 50f : Easy ? 30f : 0f);

    public static string Name => Casual ? "CASUAL ACTION" : Easy ? "TACTICAL EASY" : "NORMAL";
}
