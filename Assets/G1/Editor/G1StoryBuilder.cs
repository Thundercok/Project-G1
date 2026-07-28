using UnityEditor;
using UnityEngine;

/// Installs the main storyline on the Corvus Sprawl, and builds the breach
/// ring the whole thing points at.
///
/// The chapters deliberately hang off objectives the contacts already hand
/// out. The alternative — a second, parallel quest line — would have the
/// player doing errands for Sorensen while a story happened somewhere else.
/// This way the errands *are* the story: the order the contacts introduce each
/// other in is already an escalation, and all that was missing was somebody
/// saying so.
///
/// Menu: G1 → Build Story (works on the currently open scene).
public static class G1StoryBuilder
{
    const string EmitterObjective = "emitters";

    [MenuItem("G1/Build Story")]
    public static void BuildStandalone()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building the story.");
            return;
        }
        Build();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("G1: story installed.");
    }

    public static void Build()
    {
        Threshold(new Vector3(0f, 0f, 165f));
        InstallDirector();
    }

    // ------------------------------------------------------------- the script
    static G1StoryDirector.Beat B(G1StoryDirector.Speaker who, string line) =>
        new G1StoryDirector.Beat { who = who, line = line };

    static void InstallDirector()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("G1: no Player in scene — story director not installed.");
            return;
        }

        var go = new GameObject("StoryDirector");
        var card = go.AddComponent<G1StoryCard>();
        card.showOnStart = false;                 // the director drives it
        var dir = go.AddComponent<G1StoryDirector>();

        const G1StoryDirector.Speaker VI = G1StoryDirector.Speaker.Vi;
        const G1StoryDirector.Speaker AU = G1StoryDirector.Speaker.Auditor;
        const G1StoryDirector.Speaker ME = G1StoryDirector.Speaker.Self;

        dir.chapters = new[]
        {
            new G1StoryDirector.Chapter {
                objectiveId = "first-contact",
                title = "PROLOGUE", subtitle = "THE GATE",
                onOpen = new[] {
                    B(VI, "You are at the south gate of the Corvus Sprawl. " +
                          "There are people alive past this wall, and the army " +
                          "outside has orders not to let anyone through it."),
                },
                onClose = new[] {
                    B(AU, "You found one of the survivors. Good. " +
                          "Keep a count for me. I have to put a number in the report."),
                },
            },

            new G1StoryDirector.Chapter {
                objectiveId = "echo-witness",
                title = "CHAPTER ONE", subtitle = "UNIT FORTY-ONE",
                onOpen = new[] {
                    B(ME, "The machine by the gate still talks. It uses the name " +
                          "of the man it was working beside when this started."),
                },
                onClose = new[] {
                    B(AU, "Forty-one was a maintenance unit. Whatever is riding it " +
                          "kept the parts of him it found useful. Speech, mostly."),
                },
            },

            new G1StoryDirector.Chapter {
                objectiveId = "medical-cache",
                title = "CHAPTER TWO", subtitle = "THE LIVING",
                onOpen = new[] {
                    B(VI, "Medical stores located in the southern ruins. " +
                          "Casualties inside the Sprawl are still rising."),
                },
                onClose = new[] {
                    B(ME, "Sorensen thinks a relief column is coming. " +
                          "I have seen what the army has parked on the ridge. " +
                          "It is not a relief column."),
                },
            },

            new G1StoryDirector.Chapter {
                objectiveId = "recover-core",
                title = "CHAPTER THREE", subtitle = "THE REPORT",
                onOpen = new[] {
                    B(ME, "Halloran wants the site log off a data core in the " +
                          "northeast warehouse. She wants to know who signed off " +
                          "on keeping the thing alive."),
                },
                onClose = new[] {
                    B(AU, "Corvus authorised it. I countersigned it. " +
                          "I would sign it again. It was a very good contract."),
                },
            },

            new G1StoryDirector.Chapter {
                objectiveId = "tower-relay",
                title = "CHAPTER FOUR", subtitle = "WHAT HE IS SAYING",
                onOpen = new[] {
                    B(VI, "Encrypted transmission from the command tower. Outbound. " +
                          "Running continuously for seventy-one hours."),
                },
                onClose = new[] {
                    B(AU, "Yes, I have been sending. No, not for help. " +
                          "Corvus wants the specimen recovered before the army " +
                          "burns this valley flat. Both of those are still on the table."),
                },
            },

            new G1StoryDirector.Chapter {
                objectiveId = EmitterObjective,
                title = "FINALE", subtitle = "THE BREACH",
                onOpen = new[] {
                    B(AU, "The containment field would have sealed that breach on " +
                          "day one. My people put three pylons around it to hold the " +
                          "field down, because a dead specimen is worth nothing. " +
                          "Leave them standing and walk to the helicopter with me."),
                    B(VI, "Three field pylons detected at the breach. Destructible."),
                    B(ME, "He keeps calling it an offer. He has not once called it a " +
                          "way to fix this."),
                },
                onClose = new[] {
                    B(ME, "The field came back up. Whatever is left down there is " +
                          "sealed in with it, and forty-one people get to walk out."),
                    B(AU, "You have cost my employer a great deal of money. " +
                          "I will write that down exactly as it happened. " +
                          "It is the one part of this that anybody will believe."),
                },
            },
        };
    }

    // ----------------------------------------------------------- the Threshold
    /// The ring, and the three emitters holding it open. Destroying all three
    /// is the last objective in the game.
    static void Threshold(Vector3 at)
    {
        at = G1Placement.FindClearFootprint(at, new Vector2(14f, 14f), "Threshold");

        var root = new GameObject("Threshold");
        root.transform.position = at;

        var ringMat = Emissive(new Color(0.15f, 0.75f, 0.78f), 2.2f);
        for (int i = 0; i < 24; i++)
        {
            float a = i / 24f * Mathf.PI * 2f;
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(seg.GetComponent<Collider>());
            seg.name = "Ring_" + i;
            seg.transform.SetParent(root.transform, false);
            seg.transform.localPosition =
                new Vector3(Mathf.Cos(a) * 6.5f, 6.8f + Mathf.Sin(a) * 6.5f, 0f);
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            seg.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            seg.GetComponent<Renderer>().sharedMaterial = ringMat;
        }

        var glow = new GameObject("ThresholdGlow");
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0f, 6.8f, 0f);
        var gl = glow.AddComponent<Light>();
        gl.type = LightType.Point;
        gl.color = new Color(0.3f, 0.9f, 0.95f);
        gl.range = 34f; gl.intensity = 3.2f;

        // three emitters on a wide triangle, far enough apart that you have to
        // break contact and reposition between them rather than stand still
        var body = Emissive(new Color(0.8f, 0.55f, 0.1f), 1.4f);
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f + Mathf.PI / 6f;
            var spot = at + new Vector3(Mathf.Cos(a) * 16f, 0f, Mathf.Sin(a) * 16f);
            spot = G1Placement.FindStandingSpot(spot, "Emitter " + i, 12f);

            var em = GameObject.CreatePrimitive(PrimitiveType.Cube);
            em.name = "ResonanceEmitter_" + i;
            em.transform.position = spot + Vector3.up * 1.6f;
            em.transform.localScale = new Vector3(1.5f, 3.2f, 1.5f);
            em.GetComponent<Renderer>().sharedMaterial = body;

            var hp = em.AddComponent<HealthSystem>();
            hp.maxHealth = 220f;
            em.AddComponent<G1ObjectiveOnDeath>().objectiveId = EmitterObjective;
            var bar = em.AddComponent<WorldSpaceHealthBar>();
            bar.heightOffset = 2.4f;

            var lamp = new GameObject("EmitterLamp");
            lamp.transform.SetParent(em.transform, false);
            var l = lamp.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.7f, 0.2f);
            l.range = 16f; l.intensity = 2.4f;

            var wp = em.AddComponent<G1Waypoint>();
            wp.objectiveId = EmitterObjective;
            wp.label = "RESONANCE EMITTER";
        }

        // the objective itself — three of them, and it ends the game
        var mgr = Object.FindObjectOfType<G1ObjectiveManager>();
        if (mgr != null)
        {
            var setup = mgr.GetComponent<G1MissionSetup>();
            if (setup != null)
            {
                var list = new System.Collections.Generic.List<G1MissionSetup.Def>(
                    setup.objectives ?? new G1MissionSetup.Def[0]);
                list.Add(new G1MissionSetup.Def {
                    id = EmitterObjective,
                    description = "Seal the breach — destroy the three field pylons",
                    mandatory = true, count = 3,
                });
                setup.objectives = list.ToArray();
            }
        }
    }

    static Material Emissive(Color c, float strength)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * strength);
        return m;
    }
}
