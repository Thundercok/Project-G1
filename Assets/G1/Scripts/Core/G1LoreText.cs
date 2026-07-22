using UnityEngine;

/// Authored narrative text for the loop story, kept as plain data so builders
/// (or a designer) can drop it straight into levels. Grouped into three "loop
/// awareness" tiers that escalate from a normal disaster-escape into the
/// realization that this has all happened many times before.
public static class G1LoreText
{
    /// Spray-painted messages left by previous iterations of Chad, for
    /// G1Graffiti. They read as ambient vandalism early, then as messages the
    /// player slowly realizes were left for them, by them.
    public static readonly string[] GraffitiTier1 =
    {
        "IT FAILS AT 0600",
        "DON'T TRUST THE EXIT",
        "COUNT THE DOORS",
        "WE'VE BEEN HERE",
    };

    public static readonly string[] GraffitiTier2 =
    {
        "THE DOOR GOES BACKWARDS",
        "HE COUNTS US",
        "STOP WALKING THROUGH IT",
        "ITERATION ##6 — SAME AS ##5",
    };

    // The final tier tells the player, in plain words, how to actually win.
    public static readonly string[] GraffitiTier3 =
    {
        "DON'T STABILIZE IT",
        "BREAK THE RING, NOT THE GLASS",
        "THE CROWBAR. USE THE CROWBAR.",
        "YOU ARE THE ANCHOR — END IT",
    };

    /// Longer story lore-card bodies (for G1StoryCard-style pickups / terminals).
    /// title, then body.
    public static readonly (string title, string body)[] LoreCards =
    {
        ("PERSONNEL FILE — C. THUNDERCOCK",
         "Reassigned to the 0600 widening test by directive. No requesting officer on record. Suit calibration: nominal. Note: subject's neural signature matches the aperture's carrier frequency to 9 decimal places. This is not flagged as unusual."),

        ("MAINTENANCE LOG — SUB-LEVEL C",
         "Found another one of the orange suits at Console 3. Same face. Same handwriting in the notes. Third time this month I've logged it. Nobody upstairs will take the report."),

        ("RECOVERED AUDIO — THE AUDITOR",
         "\"A disaster that resolves pays out once. A disaster that loops is a renewable asset. Corvus has performed above expectations for a very long time. Keep him almost escaping.\""),

        ("CONCORDANCE MEMO — AUDIT STANDING",
         "Iteration ##7 proceeding within tolerance. Anchor stability: high. Recommend no intervention. The asset does not know it is an asset. This is the ideal state."),
    };

    /// Sweeper (HECU) radio chatter, degrading from professional to prophetic as
    /// the campaign proceeds. Consumed by G1SoldierBarks, indexed by scene tier.
    public static readonly string[] SweeperBarksTier1 =   // Level 1 — clean, procedural
    {
        "Contact — witness in the orange suit, moving to cut off.",
        "Cleanup sweep, this sector. No survivors, no residue.",
        "Flanking left. Keep it tidy.",
        "Standard containment. Just another one.",
    };

    public static readonly string[] SweeperBarksTier2 =   // Level 2 — uneasy
    {
        "This the same guy? He looks like the last one.",
        "Command, my counter's wrong. Says we've run this more times than it goes.",
        "Just shoot the suit and don't think about it.",
        "Anybody else feel like they've said this before?",
    };

    public static readonly string[] SweeperBarksTier3 =   // Level 3 — prophetic
    {
        "We never get to leave either. You know that, right?",
        "Don't let him reach the ring. Don't let him break it.",
        "I've killed him a hundred times. He keeps waking up.",
        "End it. Please. Somebody end it.",
    };

    /// A single clear word a Taken speaks before attacking — the human echo
    /// surfacing for a moment. Consumed by G1ZombieAI.
    public static readonly string[] TakenWords =
    {
        "...again.",
        "...help...",
        "...I'm sorry...",
        "...run...",
        "...not real...",
        "...remember me...",
    };

    static int barkIx, wordIx, graffitiIx;

    public static string NextBark(int tier)
    {
        string[] pool = tier <= 1 ? SweeperBarksTier1
                      : tier == 2 ? SweeperBarksTier2
                      :             SweeperBarksTier3;
        return pool[(barkIx++ % pool.Length + pool.Length) % pool.Length];
    }

    public static string NextTakenWord() =>
        TakenWords[(wordIx++ % TakenWords.Length + TakenWords.Length) % TakenWords.Length];

    public static string NextGraffiti(int tier)
    {
        string[] pool = tier <= 1 ? GraffitiTier1
                      : tier == 2 ? GraffitiTier2
                      :             GraffitiTier3;
        return pool[(graffitiIx++ % pool.Length + pool.Length) % pool.Length];
    }
}
