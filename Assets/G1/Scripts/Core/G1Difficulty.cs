using UnityEngine;

/// Global difficulty (PlayerPrefs "G1_Difficulty": 0 = Casual Action, 1 = Easy, 2 = Normal).
/// High-octane balance: Player is NOT a bullet sponge; tension stays high while aggression rewards health & armor!
public static class G1Difficulty
{
    public static int Mode => PlayerPrefs.GetInt("G1_Difficulty", 0);

    public static bool Casual => Mode == 0;
    public static bool Easy => Mode == 1;

    /// Damage the player receives: keep crisp & dangerous (0.6x on Casual) so combat has real tension!
    public static float IncomingDamageMult => Casual ? 0.6f : (Easy ? 0.8f : 1.0f);

    /// Damage the player's weapons deal (high impact & crisp execution).
    public static float OutgoingDamageMult => Casual ? 2.0f : (Easy ? 1.4f : 1.0f);

    /// ThreatDirector pacing: manageable horde sizes and soldier limits.
    public static float RelaxDurationMult => Casual ? 2.5f : (Easy ? 1.8f : 1f);
    public static float HordeSizeMult => Casual ? 0.5f : (Easy ? 0.75f : 1f);
    public static int MaxSoldiersDelta => Casual ? -2 : (Easy ? -1 : 0);

    /// Health & Armor siphon granted directly on every enemy kill!
    public static float KillHealthReward => Casual ? 15f : (Easy ? 10f : 5f);
    public static float KillArmorReward => Casual ? 10f : (Easy ? 5f : 0f);

    /// Passive regen when out of combat.
    public static float RegenCeiling => Casual ? 50f : (Easy ? 30f : 0f);

    public static string Name => Casual ? "CASUAL ACTION" : (Easy ? "TACTICAL EASY" : "NORMAL");
}
