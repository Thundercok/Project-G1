using UnityEngine;

/// Global difficulty (PlayerPrefs "G1_Difficulty": 0 = Easy, 1 = Normal).
/// Default is Easy. All multipliers live here so tuning stays in one file.
public static class G1Difficulty
{
    public static bool Easy => PlayerPrefs.GetInt("G1_Difficulty", 0) == 0;

    /// Damage the player receives (soldiers, claws, hazards, explosions).
    public static float IncomingDamageMult => Easy ? 0.4f : 1f;

    /// Damage the player's weapons deal.
    public static float OutgoingDamageMult => Easy ? 1.5f : 1f;

    /// ThreatDirector pacing: longer calm phases, smaller hordes, fewer
    /// simultaneous soldiers on Easy.
    public static float RelaxDurationMult => Easy ? 2f : 1f;
    public static float HordeSizeMult => Easy ? 0.5f : 1f;
    public static int MaxSoldiersDelta => Easy ? -1 : 0;

    /// Passive regen on Easy: heals toward this HP when out of combat.
    public static float RegenCeiling => Easy ? 60f : 0f;
}
