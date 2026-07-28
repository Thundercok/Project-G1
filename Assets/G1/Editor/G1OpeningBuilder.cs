using UnityEngine;

/// Writes the cold open: which shots, in which order, with which lines.
///
/// The rule the script below is written to is Half-Life's, and it is a rule
/// about restraint rather than about cameras: **show the place, and say only
/// what looking at it cannot tell you.** The aerial pass over the breach
/// explains the situation better than any sentence could, so no sentence
/// describes it. What the lines carry instead is the one thing no camera can
/// show: that the army outside is not here to rescue anybody.
///
/// The story used to be a time loop — two hundred and six iterations, an
/// "Anchor", a ring that reset you. It was the wrong story for this game.
/// A loop asks the player to hold a rule in their head before anything means
/// anything, and every line then has to spend itself re-explaining that rule
/// instead of doing its job. What is on screen is a supply base, an outbreak
/// and a company that would rather this were tidy — and that needs no rule at
/// all. Same missions, same triggers, same objective ids. Plain words.
///
/// Order of what the player learns, which is also the order they would ask:
///
///   1. where am I      an aerial over eight hundred metres of it
///   2. what happened   the breach at the centre, still lit
///   3. who is watching  a man on a tower roof who does not need to be there
///   4. what do I want   the gate, the wall, and people alive behind it
///   5. what is that     Cradle Station on the horizon, unexplained on purpose
///
/// The last one is deliberately left hanging. It is where the second half of
/// the game is, and a question the player asks themselves is worth more than
/// an answer they were given in the first ninety seconds.
///
/// Speaker tags match the story director's, so the same three voices carry the
/// opening and the campaign; `Tools/audio/generate_voice.py` scans this file
/// for `B(VI|AU|ME, "...")` and bakes the audio.
public static class G1OpeningBuilder
{
    const string VI = "HEV V.I.";
    const string AU = "THE AUDITOR";
    const string ME = "YOU";

    /// Marker so the voice generator can find these lines. It never runs at
    /// build time — the strings below are the payload.
    static string B(string who, string line) => line;

    public static G1OpeningSequence.Shot[] Shots()
    {
        // Unity coordinates in the World scene. The Sprawl fills x,z in ±400;
        // the command tower is at the origin; the breach ruins sit at z ≈ +165;
        // the south gate the player spawns behind is at z ≈ -352.
        return new[]
        {
            // 1 — the establishing pass. High, slow, and long enough to feel
            // the size of the place; nothing is said over the first four
            // seconds on purpose.
            new G1OpeningSequence.Shot {
                from = new Vector3(-520f, 210f, -430f),
                to = new Vector3(-250f, 150f, -230f),
                lookFrom = new Vector3(0f, 20f, 0f),
                lookTo = new Vector3(0f, 12f, 0f),
                seconds = 9f,
                title = "THE CORVEX",
                subtitle = "ARMY SUPPLY BASE — DAY THREE",
                speaker = VI,
                caption = B(VI, "Suit online. Air is bad but breathable. " +
                                "This base went quiet three days ago."),
            },

            // 2 — the breach. Drop out of the sky onto the thing that did this.
            new G1OpeningSequence.Shot {
                from = new Vector3(40f, 120f, 250f),
                to = new Vector3(14f, 26f, 205f),
                lookFrom = new Vector3(0f, 6f, 165f),
                lookTo = new Vector3(0f, 3f, 165f),
                seconds = 8f,
                speaker = ME,
                caption = B(ME, "Something got out of the research station east of " +
                                "here. It came down this road. Whatever it is, it " +
                                "does not stay in one body."),
            },

            // 3 — the tower, and the man on it. He is a silhouette and stays
            // one: the player should notice him before they are told anything.
            new G1OpeningSequence.Shot {
                from = new Vector3(-62f, 30f, -58f),
                to = new Vector3(-30f, 40f, -30f),
                lookFrom = new Vector3(0f, 34f, 0f),
                lookTo = new Vector3(0f, 38f, 0f),
                seconds = 7.5f,
                speaker = AU,
                caption = B(AU, "Corvus sent me to write up what happened here. " +
                                "I have written up four of these. They read " +
                                "very much alike."),
            },

            // 4 — the wall, from outside. This is the shot that states the
            // objective without stating it.
            new G1OpeningSequence.Shot {
                from = new Vector3(-140f, 34f, -430f),
                to = new Vector3(-20f, 14f, -404f),
                lookFrom = new Vector3(0f, 8f, -352f),
                lookTo = new Vector3(0f, 5f, -352f),
                seconds = 7.5f,
                speaker = VI,
                caption = B(VI, "Warning. The army has sealed this valley. They are " +
                                "firing on anyone leaving it. Forty-one people are " +
                                "still alive inside the wire."),
            },

            // 5 — east, to the horizon, where the second half of the game is.
            new G1OpeningSequence.Shot {
                from = new Vector3(430f, 120f, -60f),
                to = new Vector3(660f, 96f, -20f),
                lookFrom = new Vector3(1100f, 20f, 0f),
                lookTo = new Vector3(1100f, 10f, 0f),
                seconds = 8f,
                speaker = AU,
                caption = B(AU, "The leak is out there, past the far ridge. " +
                                "Shut it off and I can go home. " +
                                "Do try to keep the survivors alive. It reads better."),
            },

            // 6 — settle onto the road the player is about to be standing on.
            new G1OpeningSequence.Shot {
                from = new Vector3(-26f, 18f, -420f),
                to = new Vector3(0f, 2.2f, -386f),
                lookFrom = new Vector3(0f, 6f, -352f),
                lookTo = new Vector3(0f, 2.4f, -360f),
                seconds = 6f,
                title = "PROLOGUE",
                subtitle = "THE GATE",
            },
        };
    }

    /// Hangs the sequence off the player, which is where it can find the
    /// camera and the controls it has to borrow.
    public static void Install(GameObject player)
    {
        if (player == null)
        {
            Debug.LogWarning("G1: no player to install the opening on.");
            return;
        }
        var seq = player.GetComponent<G1OpeningSequence>();
        if (seq == null) seq = player.AddComponent<G1OpeningSequence>();
        seq.shots = Shots();
        seq.playOnStart = true;

        // The story director's own PROLOGUE card would land on top of the
        // opening's last one. The sequence ends by naming the chapter, so the
        // card that used to do it is redundant.
        var card = player.GetComponent<G1StoryCard>();
        if (card != null) card.showOnStart = false;
    }
}
