using UnityEngine;

/// Global difficulty (PlayerPrefs "G1_Difficulty": 0 = Casual Story, 1 = Easy, 2 = Normal).
/// Default is Casual (0) for accessible high-octane play. All multipliers live here.
public static class G1Difficulty
{
    public static int Mode => PlayerPrefs.GetInt("G1_Difficulty", 0);

    public static bool Casual => Mode == 0;
    public static bool Easy => Mode == 1;

    /// Damage the player receives (soldiers, claws, hazards, explosions).
    public static float IncomingDamageMult => Casual ? 0.2f : (Easy ? 0.4f : 1f);

    /// Damage the player's weapons deal.
    public static float OutgoingDamageMult => Casual ? 2.5f : (Easy ? 1.5f : 1f);

    /// ThreatDirector pacing: longer calm phases, smaller hordes, fewer simultaneous soldiers.
    public static float RelaxDurationMult => Casual ? 3f : (Easy ? 2f : 1f);
    public static float HordeSizeMult => Casual ? 0.35f : (Easy ? 0.6f : 1f);
    public static int MaxSoldiersDelta => Casual ? -2 : (Easy ? -1 : 0);

    /// Passive regen: heals toward this HP when out of combat.
    public static float RegenCeiling => Casual ? 100f : (Easy ? 75f : 0f);

    public static string Name => Casual ? "CASUAL / STORY" : (Easy ? "EASY" : "NORMAL");
}
