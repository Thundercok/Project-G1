using UnityEngine;

/// The surviving personnel of the Corvus Facility — the cast the quest system
/// draws from. Each role fixes a suit tint, a beacon colour and a job title, so
/// the player can read "who does what" from across the plaza before ever
/// pressing E: orange is science, blue is security, white is medical, and the
/// pale drowned cyan is an Echo — someone the loop already ate.
public enum G1NpcRole
{
    Engineer,        // repairs: power, doors, the comms array
    Medic,           // triage: reach the wounded, recover supplies
    SecurityChief,   // combat: clear a district, put a target down
    Researcher,      // retrieval: samples, data cores, the truth
    SignalTech,      // recon: reach a place, plant a relay
    Quartermaster,   // supply: pays in ordnance
    Echo,            // a time-folded survivor from a previous iteration
}

/// Look-and-voice data for one role. Pure data — no scene dependencies, so the
/// editor builders and the runtime HUD read the exact same table.
public struct G1NpcProfile
{
    public string title;        // shown under the name on the contact log
    public Color suit;          // primary renderer tint
    public Color trim;          // secondary renderer tint
    public Color beacon;        // marker + beacon light colour
    public float maxHealth;
    public bool armed;          // true = defends itself, false = flees

    // Voice: the syllable bank is shared, so a character is distinguished by
    // how they use it. Pitch is who they are, wobble is how steady they are,
    // and letters-per-blip is how fast they talk — a chief barks, an Echo
    // drags every word out of a body that stopped being theirs.
    public float voicePitch;
    public float voiceWobble;
    public int voiceRate;       // letters between blips

    public G1NpcProfile(string title, Color suit, Color trim, Color beacon,
                        float maxHealth, bool armed,
                        float voicePitch = 1f, float voiceWobble = 0.07f,
                        int voiceRate = 4)
    {
        this.title = title; this.suit = suit; this.trim = trim;
        this.beacon = beacon; this.maxHealth = maxHealth; this.armed = armed;
        this.voicePitch = voicePitch; this.voiceWobble = voiceWobble;
        this.voiceRate = voiceRate;
    }
}

public static class G1NpcRoster
{
    public static G1NpcProfile GetProfile(G1NpcRole role)
    {
        switch (role)
        {
            case G1NpcRole.Engineer:
                return new G1NpcProfile("FACILITY ENGINEER",
                    new Color(0.95f, 0.72f, 0.12f), new Color(0.42f, 0.32f, 0.06f),
                    new Color(1f, 0.78f, 0.2f), 90f, false,
                    voicePitch: 0.98f, voiceWobble: 0.06f, voiceRate: 4);

            case G1NpcRole.Medic:
                return new G1NpcProfile("FIELD MEDIC",
                    new Color(0.92f, 0.92f, 0.9f), new Color(0.75f, 0.16f, 0.14f),
                    new Color(1f, 0.45f, 0.42f), 85f, false,
                    voicePitch: 1.18f, voiceWobble: 0.05f, voiceRate: 3);

            case G1NpcRole.SecurityChief:
                return new G1NpcProfile("SECURITY CHIEF",
                    new Color(0.2f, 0.38f, 0.68f), new Color(0.1f, 0.18f, 0.34f),
                    new Color(0.4f, 0.66f, 1f), 160f, true,
                    voicePitch: 0.80f, voiceWobble: 0.04f, voiceRate: 5);

            case G1NpcRole.Researcher:
                return new G1NpcProfile("RESEARCH LEAD",
                    new Color(0.86f, 0.44f, 0.08f), new Color(0.4f, 0.2f, 0.04f),
                    new Color(1f, 0.6f, 0.16f), 75f, false,
                    voicePitch: 1.08f, voiceWobble: 0.09f, voiceRate: 4);

            case G1NpcRole.SignalTech:
                return new G1NpcProfile("SIGNALS TECH",
                    new Color(0.22f, 0.6f, 0.34f), new Color(0.1f, 0.28f, 0.16f),
                    new Color(0.4f, 0.95f, 0.5f), 90f, false,
                    voicePitch: 1.28f, voiceWobble: 0.10f, voiceRate: 3);

            case G1NpcRole.Quartermaster:
                return new G1NpcProfile("QUARTERMASTER",
                    new Color(0.42f, 0.42f, 0.3f), new Color(0.2f, 0.2f, 0.14f),
                    new Color(0.85f, 0.8f, 0.5f), 120f, true,
                    voicePitch: 0.74f, voiceWobble: 0.05f, voiceRate: 5);

            case G1NpcRole.Echo:
            default:
                return new G1NpcProfile("ECHO — PRIOR ITERATION",
                    new Color(0.55f, 0.78f, 0.82f), new Color(0.24f, 0.38f, 0.42f),
                    new Color(0.5f, 0.95f, 1f), 60f, false,
                    voicePitch: 0.60f, voiceWobble: 0.22f, voiceRate: 7);
        }
    }
}
