using UnityEditor;
using UnityEngine;

/// Populates a scene with the Corvus survivor network: seven quest contacts
/// scattered far enough apart that a single bio-scan can never see them all,
/// which is the point — the player has to move, re-scan, and follow the chain
/// of introductions from one district to the next.
///
/// Chain: ECHO → SORENSEN → RIGGS → VANCE → OKAFOR → HALLORAN → PARK.
/// Menu: G1 → Build Quest NPC Network (works on the currently open scene).
public static class G1QuestNpcBuilder
{
    const string Models = "Assets/G1/Models";

    [MenuItem("G1/Build Quest NPC Network")]
    public static void BuildStandalone()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building the NPC network.");
            return;
        }
        PopulateSprawl();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("G1: quest NPC network built — press Q in game to bio-scan for contacts.");
    }

    /// Spawns the contact network laid out for the Corvus Sprawl (HugeMap) and
    /// makes sure the player is carrying a scanner to find them with.
    public static void PopulateSprawl()
    {
        G1Rig.EnsureAvatars($"{Models}/Protagonist.fbx");
        EnsureScanner();

        // 1 ── the first thing you meet: something that used to be a person.
        //      Just past the trench line, so the opening scan always lands on
        //      it once Kane's gate is open. Must match G1DoorKitBuilder's
        //      FirstContact, which is what Kane's waypoint points at.
        Contact(new Vector3(-26f, 0.1f, -286f), 20f, "ITERATION 41", G1NpcRole.Echo,
                "SOUTH GATE — BREACH EDGE",
                "echo-witness", "Stand where Iteration 41 fell",
                new Vector3(-96f, 0f, -244f), "THE FALL SITE",
                offer:
                "You're the Anchor. I was too, forty loops ago... I still can't " +
                "leave the spot where it took me. Go and stand there. Someone " +
                "should see it who isn't already dead.",
                accept: "West of the ruins. You'll know it. It's colder there.",
                nag: "You haven't gone yet. It's still waiting. It's always waiting.",
                turnIn: "You saw. Then some part of it is finally over. Sorensen's " +
                        "aid post is east of the plaza — the living need you more.",
                done: "Go on. I'm not going anywhere. That's rather the problem.",
                introduces: "MEDIC SORENSEN",
                health: 0f, armor: 40f, ammo: false);

        // 2 ── field medic: supplies are stranded in the ruins the Taken hold.
        Contact(new Vector3(46f, 0.1f, -34f), 190f, "MEDIC SORENSEN", G1NpcRole.Medic,
                "AID POST — PLAZA EAST",
                "medical-cache", "Recover the medical cache from the breach ruins",
                new Vector3(4f, 0f, -168f), "MEDICAL CACHE",
                offer:
                "I'm out of everything. Our resupply is sitting in the southern " +
                "ruins with about thirty Taken walking circles around it. Get to " +
                "the cache and I can keep people breathing.",
                accept: "Straight south into the ruins. Don't stop moving in there.",
                nag: "No cache, no patients — that's the arithmetic. Please hurry.",
                turnIn: "That's blood and bandages for a week. Patched you up too. " +
                        "Riggs is holding the motor pool — he pays better than I do.",
                done: "Stay whole. I'd rather not see you on this table.",
                introduces: "QUARTERMASTER RIGGS",
                health: 60f, armor: 25f, ammo: true);

        // 3 ── quartermaster: the gunship. A kill quest with one unmistakable
        //      target, so it can't be dead-ended by clearing the map first.
        Contact(new Vector3(-30f, 0.1f, -58f), 200f, "QUARTERMASTER RIGGS", G1NpcRole.Quartermaster,
                "MOTOR POOL — SOUTH PLAZA",
                "gunship", "Destroy the HECU gunship",
                new Vector3(0f, 14f, 0f), "GUNSHIP",
                offer:
                "That gunship has strafed my yard four times. Every crate I move, " +
                "it burns. Put it in the dirt and I'll open the good lockers for you.",
                accept: "Aim for the rotor. It flies over the plaza — you can't miss it.",
                nag: "I can still hear rotors. That means you're not finished.",
                turnIn: "Beautiful. Take everything you can carry. Chief Vance runs " +
                        "the base out west — she's been asking for a spare gun.",
                done: "Lockers are open. Help yourself, quietly.",
                introduces: "CHIEF VANCE",
                health: 25f, armor: 50f, ammo: true, mandatory: false);

        // 4 ── security chief: push the line back to the contested plaza.
        Contact(new Vector3(-152f, 0.1f, 10f), 210f, "CHIEF VANCE", G1NpcRole.SecurityChief,
                "ALLIED BASE — WEST",
                "hold-plaza", "Push through to the central plaza",
                new Vector3(0f, 0f, 6f), "CENTRAL PLAZA",
                offer:
                "My line has been stuck sixty metres short of the plaza for two " +
                "days. I don't need a hero, I need someone the Sweepers have to " +
                "look at. Reach the plaza and my people follow you in.",
                accept: "Then go. We move when you move.",
                nag: "The plaza's still theirs. Nothing's changed while you stood here.",
                turnIn: "The line's moving. First ground we've taken all week. " +
                        "Okafor's up at the labs — his comms problem is yours now.",
                done: "You've got a squad behind you now. Use them.",
                introduces: "ENGINEER OKAFOR",
                health: 30f, armor: 40f, ammo: true);

        // 5 ── engineer: the original comms quest, now part of the chain.
        Contact(new Vector3(10f, 0.1f, 150f), 220f, "ENGINEER OKAFOR", G1NpcRole.Engineer,
                "LAB COMPLEX — NORTH",
                "restore-comms", "Restore the signal at the comms array",
                new Vector3(155f, 0f, -150f), "COMMS ARRAY",
                offer:
                "The Sweepers cut our comms, but the southeast dish is still " +
                "standing. Reach it and bring the array back up, or nobody topside " +
                "ever learns we were here at all.",
                accept: "Southeast corner. Get the dish talking and get out.",
                nag: "Still dead air. The array won't fix itself.",
                turnIn: "We're on the air. Whatever that's worth in a loop. " +
                        "Dr. Halloran is in the northwest quarters — she'll want you.",
                done: "Signal's holding. Thank you for that.",
                introduces: "DR. HALLORAN",
                health: 25f, armor: 50f, ammo: true);

        // 6 ── research lead: the data core, deep in the hostile northeast.
        Contact(new Vector3(-140f, 0.1f, 148f), 240f, "DR. HALLORAN", G1NpcRole.Researcher,
                "LIVING QUARTERS — NORTHWEST",
                "recover-core", "Recover the data core from the northeast warehouse",
                new Vector3(148f, 0f, 146f), "DATA CORE",
                offer:
                "Everything we recorded about the Threshold is on one core in the " +
                "northeast warehouse — including the iteration count. I need to " +
                "know what number we're on. Bring it to me.",
                accept: "Northeast warehouse. And doctor — read nothing on the way back.",
                nag: "Without that core we're guessing, and I'm tired of guessing.",
                turnIn: "Iteration two hundred and six. Two hundred and six. " +
                        "...Park is out on the east ridge. Tell him I said run.",
                done: "Two hundred and six. I keep saying it. It doesn't help.",
                introduces: "SIGNALS TECH PARK",
                health: 30f, armor: 40f, ammo: true);

        // 7 ── signals tech: the last one, deepest into enemy ground.
        Contact(new Vector3(122f, 0.1f, 62f), 240f, "SIGNALS TECH PARK", G1NpcRole.SignalTech,
                "EAST RIDGE — RELAY POST",
                "tower-relay", "Plant the relay at the command tower",
                new Vector3(0f, 0f, 26f), "COMMAND TOWER",
                offer:
                "There's a man in a suit standing on top of the command tower and " +
                "he has not moved in three days. I want a relay at the base of that " +
                "tower so we can hear what he's transmitting. Do it and get clear.",
                accept: "Base of the tower, dead centre of the plaza. Don't look up.",
                nag: "No relay, no idea what he's saying. Or to whom.",
                turnIn: "It's a carrier signal. Outbound. He's reporting on you — " +
                        "and something out there is answering. Get to extraction.",
                done: "Whatever's listening already knows your name. Go.",
                introduces: "",
                health: 40f, armor: 60f, ammo: true);
    }

    // --------------------------------------------------------------- helpers
    static void EnsureScanner()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("G1: no Player in scene — quest scanner not installed.");
            return;
        }
        if (player.GetComponent<G1QuestScanner>() == null)
            player.AddComponent<G1QuestScanner>();
    }

    public static G1QuestNpc Contact(Vector3 pos, float yaw, string name, G1NpcRole role,
                        string district,
                        string questId, string questTitle, Vector3 target, string targetLabel,
                        string offer, string accept, string nag, string turnIn, string done,
                        string introduces, float health, float armor, bool ammo,
                        // side work: never gate extraction behind a contact's errand
                        bool mandatory = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Protagonist.fbx");
        if (prefab == null)
        {
            Debug.LogWarning("G1: Protagonist.fbx missing — build Level 1 first.");
            return null;
        }

        // Hand-picked coordinates on a 600m map land inside buildings and on
        // roofs. Every contact has to stand somewhere the player can actually
        // walk up to, so the desired spot is checked and nudged if it isn't.
        pos = G1Placement.FindStandingSpot(pos, name);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "Contact_" + name.Replace(' ', '_');
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        G1Rig.Setup(go, $"{Models}/Protagonist.fbx", "Assets/G1/Anim/Protagonist.controller");

        var profile = G1NpcRoster.GetProfile(role);
        G1CharacterSkin.Apply(go, "Protagonist", profile.suit, profile.trim);

        var col = go.AddComponent<CapsuleCollider>();
        col.height = 1.8f; col.radius = 0.4f; col.center = new Vector3(0f, 0.9f, 0f);

        // contacts are plot-critical: a stray grenade must not dead-end a chain
        var hp = go.AddComponent<HealthSystem>();
        hp.maxHealth = profile.maxHealth;
        hp.godMode = true;

        var npc = go.AddComponent<G1QuestNpc>();
        npc.npcName = name;
        npc.role = role;
        npc.district = district;
        npc.questId = questId;
        npc.questTitle = questTitle;
        npc.mandatory = mandatory;
        npc.requiredCount = 1;
        npc.hasQuestTarget = true;
        npc.questTarget = target;
        npc.targetLabel = targetLabel;
        npc.offerLine = offer;
        npc.acceptLine = accept;
        npc.nagLine = nag;
        npc.turnInLine = turnIn;
        npc.doneLine = done;
        npc.introducesContact = introduces;
        npc.rewardHealth = health;
        npc.rewardArmor = armor;
        npc.rewardAmmo = ammo;

        // destination trigger — kill quests wire their own completion instead
        if (questId != "gunship")
            QuestZone(questId, target, 12f);

        return npc;
    }

    static void QuestZone(string objectiveId, Vector3 pos, float size)
    {
        var go = new GameObject("QuestZone_" + objectiveId);
        go.transform.position = pos + Vector3.up * 2f;
        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(size, 6f, size);
        go.AddComponent<G1QuestZone>().objectiveId = objectiveId;
    }
}
