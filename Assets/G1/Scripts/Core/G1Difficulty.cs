using UnityEngine;
using UnityEngine.SceneManagement;

/// Global difficulty (PlayerPrefs "G1_Difficulty": 0 = Easy, 1 = Normal).
/// Default is Easy. All multipliers live here so tuning stays in one file.
///
/// On top of the Easy/Normal setting, difficulty ramps by CAMPAIGN LEVEL so the
/// game starts gentle and gets harder each level:
///   Level 1 (TestScene) — simplest: soft hits, generous regen, small hordes.
///   Level 2 (Quarantine) — tougher: harder hits, less regen, bigger hordes.
///   Level 3 (Threshold)  — hardest: full-strength hits, no regen, max pressure.
/// The level is inferred from the active scene name, so nothing needs wiring.
public static class G1Difficulty
{
    public static bool Easy => PlayerPrefs.GetInt("G1_Difficulty", 0) == 0;

    /// 1, 2, or 3 based on the active scene. Everything else keys off this.
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
    static float LvlDamageFactor => Level == 3 ? 1.9f : Level == 2 ? 1.4f : 1.0f;
    static float LvlHordeFactor  => Level == 3 ? 1.6f : Level == 2 ? 1.25f : 1.0f;

    /// Damage the player receives (soldiers, claws, hazards, explosions).
    public static float IncomingDamageMult => (Easy ? 0.4f : 1f) * LvlDamageFactor;

    /// Damage the player's weapons deal. Slightly stronger early so Level 1 is a
    /// clean power fantasy; tapers toward baseline by Level 3.
    public static float OutgoingDamageMult =>
        (Easy ? 1.5f : 1f) * (Level == 3 ? 0.85f : Level == 2 ? 0.95f : 1.1f);

    /// ThreatDirector pacing: longer calm phases, smaller hordes, fewer
    /// simultaneous soldiers on Easy — and smaller/slower on earlier levels.
    public static float RelaxDurationMult => (Easy ? 2f : 1f) * (Level == 1 ? 1.4f : Level == 2 ? 1.0f : 0.7f);
    public static float HordeSizeMult => (Easy ? 0.5f : 1f) * LvlHordeFactor;
    public static int MaxSoldiersDelta => (Easy ? -1 : 0) + (Level == 3 ? 1 : 0);

    /// Passive regen when out of combat — heals toward this HP. Generous on
    /// Level 1 so new players find their feet; gone by Level 3.
    public static float RegenCeiling =>
        Level == 1 ? (Easy ? 75f : 50f)
      : Level == 2 ? (Easy ? 50f : 25f)
      :              0f;
}
