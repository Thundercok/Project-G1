using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// Gives the Corvus Sprawl its architecture: a sealed south gate you have to be
/// let through, hardened bunkers worth detouring into, and walled compounds
/// around the places quests send you — so "go to the comms array" means opening
/// a door rather than walking over a patch of dirt.
///
/// It also stages the opening beat. SGT. KANE waits at the gate with the first
/// mission; accepting it sounds the alarm, grinds the blast doors apart and
/// wakes the HECU picket dug in behind them. The fight starts on your word.
///
/// Menu: G1 → Build Door &amp; Bunker Kit (works on the currently open scene).
public static class G1DoorKitBuilder
{
    const string HECU = "Assets/G1/Prefabs/HECUSoldier.prefab";

    // the gate sits between the player's spawn (z ≈ -378) and the first contact,
    // in the break the trench line leaves where the highway runs through it
    const float GateZPreferred = -352f;
    static readonly Vector3 FirstContact = new Vector3(-26f, 0f, -286f);

    [MenuItem("G1/Build Door & Bunker Kit")]
    public static void BuildStandalone()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building the door kit.");
            return;
        }
        Build();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("G1: door kit built — south gate sealed, bunkers stocked. " +
                  "Rebake the NavMesh if you ran this standalone.");
    }

    public static void Build()
    {
        // the contact network built structures immediately before this; their
        // colliders are invisible to raycasts until physics is resynced
        Physics.SyncTransforms();

        int enemyLayer = G1HugeMapBuilder.EnsureLayer("Enemy");
        SouthGate(enemyLayer);

        // hardened caches worth the detour, spread along the routes between
        // districts so there's always one roughly on the way
        Bunker("Bunker_WestApproach", new Vector3(-96f, 0f, -34f), 20f);
        Bunker("Bunker_LabPerimeter", new Vector3(38f, 0f, 118f), 195f);
        Bunker("Bunker_EastRidge", new Vector3(104f, 0f, 34f), -70f);
        Bunker("Bunker_SouthRuins", new Vector3(-42f, 0f, -178f), 145f);

        // quest destinations become places you have to get into
        Compound("Compound_CommsArray", new Vector3(150f, 0f, 150f), 180f,
                 "ARRAY ACCESS");
        Compound("Compound_Warehouse", new Vector3(160f, 0f, -160f), 0f,
                 "WAREHOUSE SHUTTER");
    }

    // ------------------------------------------------------- the opening beat
    static void SouthGate(int enemyLayer)
    {
        // The FBX is a handful of merged meshes, so a guard mast standing on the
        // road can't be hidden object-by-object — the gate has to move instead.
        // Probe the approach and take the clearest slot near the preferred z.
        float gz = ClearestGateZ();
        var GatePos = new Vector3(0f, 0f, gz);

        var concrete = Mat(new Color(0.34f, 0.34f, 0.36f));
        var root = new GameObject("SouthGate");
        root.transform.position = GatePos;

        // perimeter wall with a gap in the middle for the door. Long enough
        // (≈290m) that going around it is a decision, not an accident.
        Slab("GateWall_W", new Vector3(-74f, 2.5f, 0f), new Vector3(140f, 5f, 1.2f),
             concrete, root.transform);
        Slab("GateWall_E", new Vector3(74f, 2.5f, 0f), new Vector3(140f, 5f, 1.2f),
             concrete, root.transform);
        Slab("GateTower_W", new Vector3(-4.6f, 3.5f, 0f), new Vector3(1.6f, 7f, 2.4f),
             concrete, root.transform);
        Slab("GateTower_E", new Vector3(4.6f, 3.5f, 0f), new Vector3(1.6f, 7f, 2.4f),
             concrete, root.transform);
        Slab("GateLintel", new Vector3(0f, 5.4f, 0f), new Vector3(9.2f, 1.2f, 1.6f),
             concrete, root.transform);

        var gate = BlastDoorway("GateDoor", GatePos, 0f, 7.6f, 4.8f,
                                "SPRAWL GATE", locked: true,
                                lockedMessage: "SEALED — AWAITING CLEARANCE",
                                parent: root.transform);
        gate.moveTime = 3.2f;          // huge and slow: it reads as an event
        gate.openOnce = true;

        // The picket behind the gate, asleep until the mission is given. Three,
        // not a squad, and set back far enough that the player clears the
        // doorway and finds cover before the first shot lands — this is the
        // opening fight, it should teach rather than execute.
        var picket = new GameObject("GatePicket");
        picket.transform.position = new Vector3(0f, 0f, gz + 30f);
        foreach (var p in new[]
        {
            new Vector3(-11f, 0f, gz + 28f),
            new Vector3(10f, 0f, gz + 32f),
            new Vector3(-2f, 0f, gz + 40f),
        })
        {
            var s = G1HugeMapBuilder.SpawnEnemy(HECU, G1Placement.FindStandingSpot(p, "picket"),
                                                enemyLayer, 180f);
            if (s != null) s.transform.SetParent(picket.transform, true);
        }
        picket.SetActive(false);       // G1QuestNpc wakes it on accept

        // Kane's two guards push through with the player. Two friendly rifles
        // turn the opening from a gauntlet into a firefight you can win.
        foreach (var p in new[] { new Vector3(-5f, 0f, gz - 8f), new Vector3(7f, 0f, gz - 9f) })
            G1HugeMapBuilder.SpawnAlly(G1Placement.FindStandingSpot(p, "gate escort"),
                                       true, new Color(0.22f, 0.4f, 0.7f), "GateEscort");

        // staging post: kit up before you are let through, not after
        var stage = new Vector3(-3f, 0f, gz - 13f);
        G1ArmorPack.Create(stage + new Vector3(-1.6f, 0.5f, 0f), 50f);
        G1AmmoPack.Create(stage + new Vector3(0f, 0.5f, 0f));
        G1HealthPack.Create(stage + new Vector3(1.6f, 0.5f, 0f));
        G1WallCharger.Create(stage + new Vector3(-4f, 1.1f, 0f));

        // SGT. KANE — the first voice in the game, facing the player's spawn
        var kane = G1QuestNpcBuilder.Contact(
            new Vector3(3.2f, 0.1f, gz - 12f), 180f, "SGT. KANE",
            G1NpcRole.SecurityChief, "SOUTH GATE — STAGING",
            "first-contact", "Find a survivor inside the Sprawl",
            FirstContact, "SURVIVOR SIGNAL",
            offer:
            "Stop right there. Nobody walks into the Sprawl blind. We have people " +
            "alive past this gate and no way to reach them — the yard beyond is " +
            "HECU ground. Say the word and I open it, but the second it moves " +
            "they will know you're coming. Find one of ours and get them talking.",
            accept:
            "Opening the gate. Take the crate first, then hit Q — the suit reads " +
            "bio-signals and you'll want to know who's breathing before you round " +
            "a corner. My two are with you. Move!",
            nag: "Gate's open and the yard's hot. Go find our people.",
            turnIn:
            "You found one. That's more than we managed in two days. Take the " +
            "resupply — and whatever that thing out there told you, keep walking.",
            done: "Gate stays open. Bring them home if you can.",
            introduces: "ITERATION 41",
            health: 40f, armor: 50f, ammo: true);

        if (kane != null)
        {
            kane.openOnAccept = new[] { gate };
            kane.activateOnAccept = new[] { picket };
            kane.alarmOnAccept = true;
        }
    }

    // ------------------------------------------------------------- structures
    /// A concrete strongpoint with a blast door and a reason to open it.
    static void Bunker(string name, Vector3 pos, float yaw)
    {
        const float w = 8f, d = 8f, h = 3.4f, t = 0.4f;

        // never grow a bunker out of the side of a warehouse, and never open
        // its only door into one either
        pos = G1Placement.FindClearFootprint(pos, new Vector2(w / 2f + 1f, d / 2f + 1f), name);
        yaw = G1Placement.BestDoorYaw(pos, d / 2f, yaw, name);

        var concrete = Mat(new Color(0.3f, 0.31f, 0.32f));
        var root = new GameObject(name);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Slab("Floor", new Vector3(0f, -0.15f, 0f), new Vector3(w, 0.3f, d), concrete, root.transform);
        Slab("Roof", new Vector3(0f, h, 0f), new Vector3(w, t, d), concrete, root.transform);
        Slab("WallBack", new Vector3(0f, h / 2f, d / 2f), new Vector3(w, h, t), concrete, root.transform);
        Slab("WallL", new Vector3(-w / 2f, h / 2f, 0f), new Vector3(t, h, d), concrete, root.transform);
        Slab("WallR", new Vector3(w / 2f, h / 2f, 0f), new Vector3(t, h, d), concrete, root.transform);
        // front wall, split around a 3.2m doorway
        Slab("WallFront_L", new Vector3(-2.8f, h / 2f, -d / 2f), new Vector3(2.4f, h, t), concrete, root.transform);
        Slab("WallFront_R", new Vector3(2.8f, h / 2f, -d / 2f), new Vector3(2.4f, h, t), concrete, root.transform);
        Slab("WallFront_Top", new Vector3(0f, h - 0.4f, -d / 2f), new Vector3(3.4f, 0.8f, t), concrete, root.transform);

        var door = BlastDoorway("BunkerDoor", root.transform.TransformPoint(new Vector3(0f, 0f, -d / 2f)),
                                yaw, 3.2f, 2.6f, "BUNKER", locked: false,
                                lockedMessage: "", parent: root.transform);
        door.moveTime = 1.4f;

        var lampGo = new GameObject("InteriorLight");
        lampGo.transform.SetParent(root.transform, false);
        lampGo.transform.localPosition = new Vector3(0f, h - 0.6f, 0f);
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.86f, 0.6f);
        lamp.range = 13f; lamp.intensity = 2.6f;   // the loot has to read from the door

        // the payoff for opening it
        G1ArmorPack.Create(root.transform.TransformPoint(new Vector3(-2f, 0.5f, 1.5f)));
        G1AmmoPack.Create(root.transform.TransformPoint(new Vector3(0f, 0.5f, 2f)));
        G1HealthPack.Create(root.transform.TransformPoint(new Vector3(2f, 0.5f, 1.5f)));
    }

    /// An open-topped walled yard with one way in — wraps a quest destination
    /// so reaching it is a door problem, not a walking problem.
    static void Compound(string name, Vector3 pos, float yaw, string doorLabel)
    {
        const float s = 18f, h = 4.2f, t = 0.5f;

        // The compound wants to stay on the structure the quest points at, so
        // it gets a short leash — but it does need one. Wrapped exactly around
        // the comms dish, all four of its walls opened onto mast footings, and
        // choosing a facing cannot fix a position problem: place and face have
        // to be solved together, so try the nearest spots in turn and take the
        // first that has both a clear footprint and a walkable way in.
        Vector3 chosen = pos;
        bool solved = false;
        for (float r = 0f; r <= 28f && !solved; r += 7f)
        {
            for (int i = 0; i < (r == 0f ? 1 : 8) && !solved; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var candidate = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!G1Placement.FootprintClear(candidate, new Vector2(s / 2f + 1f, s / 2f + 1f)))
                    continue;
                float y = G1Placement.BestDoorYaw(candidate, s / 2f, yaw, name);
                if (!G1Placement.IsStandable(
                        candidate + Quaternion.Euler(0f, y, 0f) * Vector3.back * (s / 2f + 4f), out _))
                    continue;
                chosen = candidate; yaw = y; solved = true;
            }
        }
        if (!solved)
            Debug.LogWarning($"G1: {name} found no position with a walkable entrance " +
                             $"within 28m of {pos} — leaving it put.");
        else if (chosen != pos)
            Debug.Log($"G1: {name} moved {Vector3.Distance(pos, chosen):0}m so its door opens " +
                      "onto ground you can walk on.");
        pos = chosen;

        var concrete = Mat(new Color(0.32f, 0.33f, 0.31f));
        var root = new GameObject(name);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Slab("WallBack", new Vector3(0f, h / 2f, s / 2f), new Vector3(s, h, t), concrete, root.transform);
        Slab("WallL", new Vector3(-s / 2f, h / 2f, 0f), new Vector3(t, h, s), concrete, root.transform);
        Slab("WallR", new Vector3(s / 2f, h / 2f, 0f), new Vector3(t, h, s), concrete, root.transform);
        Slab("WallFront_L", new Vector3(-6.1f, h / 2f, -s / 2f), new Vector3(5.8f, h, t), concrete, root.transform);
        Slab("WallFront_R", new Vector3(6.1f, h / 2f, -s / 2f), new Vector3(5.8f, h, t), concrete, root.transform);
        Slab("WallFront_Top", new Vector3(0f, h - 0.5f, -s / 2f), new Vector3(6.4f, 1f, t), concrete, root.transform);

        var door = BlastDoorway("CompoundDoor",
                                root.transform.TransformPoint(new Vector3(0f, 0f, -s / 2f)),
                                yaw, 6.2f, 3.2f, doorLabel, locked: false,
                                lockedMessage: "", parent: root.transform);
        door.moveTime = 2.2f;
    }

    /// Finds a z along the southern approach where the gate's opening lands on
    /// clear ground. The map's own guard mast stands on the road near the
    /// preferred spot and is baked into a merged mesh, so it can't be hidden —
    /// we walk outwards from the preferred z and take the first slot where
    /// nothing tall stands across the doorway.
    static float ClearestGateZ()
    {
        for (int step = 0; step <= 16; step++)
        {
            // -250, -249, -251, -248, -252 … so we stay as close as we can
            for (int dir = 0; dir < (step == 0 ? 1 : 2); dir++)
            {
                float z = GateZPreferred + (dir == 0 ? step : -step);
                if (IsClearAcross(z))
                {
                    if (step > 0)
                        Debug.Log($"G1: gate shifted to z={z} — {GateZPreferred} was obstructed.");
                    return z;
                }
            }
        }
        Debug.LogWarning("G1: no clear gate slot found; using the preferred z.");
        return GateZPreferred;
    }

    /// True when the doorway span is clear AND stays clear for 8m either side.
    /// The narrow test isn't enough: a mast three metres short of the gate no
    /// longer intersects it but still stands in the middle of the way through.
    static bool IsClearAcross(float z)
    {
        for (float dz = -8f; dz <= 8f; dz += 1f)
            for (float x = -5f; x <= 5f; x += 1f)
                if (TerrainHeight(new Vector3(x, 0f, z + dz)) > 1f)
                    return false;
        return true;
    }

    static float TerrainHeight(Vector3 at)
    {
        var origin = new Vector3(at.x, 60f, at.z);
        return Physics.Raycast(origin, Vector3.down, out RaycastHit h, 120f) ? h.point.y : 0f;
    }

    // ---------------------------------------------------------------- helpers
    /// Frame plus two panels that grind apart into the jambs.
    static G1BlastDoor BlastDoorway(string name, Vector3 worldPos, float yaw,
                                    float width, float height, string label,
                                    bool locked, string lockedMessage, Transform parent)
    {
        var doorMat = Mat(new Color(0.42f, 0.44f, 0.46f));
        var trimMat = Mat(new Color(0.2f, 0.21f, 0.22f));

        var root = new GameObject(name);
        root.transform.position = worldPos;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (parent != null) root.transform.SetParent(parent, true);

        float half = width / 2f;
        Slab("JambL", new Vector3(-half - 0.3f, height / 2f, 0f),
             new Vector3(0.6f, height + 0.4f, 0.9f), trimMat, root.transform);
        Slab("JambR", new Vector3(half + 0.3f, height / 2f, 0f),
             new Vector3(0.6f, height + 0.4f, 0.9f), trimMat, root.transform);

        var left = Slab("PanelL", new Vector3(-width / 4f, height / 2f, 0f),
                        new Vector3(half, height, 0.35f), doorMat, root.transform);
        var right = Slab("PanelR", new Vector3(width / 4f, height / 2f, 0f),
                         new Vector3(half, height, 0.35f), doorMat, root.transform);

        // panels must not carve the navmesh, or AI paths would depend on the
        // door state at bake time and never update when it opens
        left.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        right.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

        var lampGo = new GameObject("StatusLamp");
        lampGo.transform.SetParent(root.transform, false);
        lampGo.transform.localPosition = new Vector3(0f, height + 0.5f, -0.7f);
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 7f;

        var door = root.AddComponent<G1BlastDoor>();
        door.leftPanel = left.transform;
        door.rightPanel = right.transform;
        door.travel = half;
        door.doorLabel = label;
        door.locked = locked;
        if (!string.IsNullOrEmpty(lockedMessage)) door.lockedMessage = lockedMessage;
        door.statusLight = lamp;
        return door;
    }

    static GameObject Slab(string name, Vector3 localPos, Vector3 size,
                           Material mat, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static Material Mat(Color c)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        return m;
    }
}
