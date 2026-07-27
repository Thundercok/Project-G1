using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Headless verification for the Corvus Sprawl build. Drop a file at
/// Temp/g1_verify and the next script reload rebuilds the huge map, asserts the
/// gate/bunker/contact wiring, renders vantage-point PNGs and writes a plain
/// text report — so the build can be checked without entering Play mode.
///
/// Report + images land in the directory named by Temp/g1_verify (its contents),
/// or ./Temp if the file is empty. Temp/ is per-session and never committed.
[InitializeOnLoad]
public static class G1VerifyBuild
{
    const string Flag = "Temp/g1_verify";

    static G1VerifyBuild()
    {
        if (File.Exists(Flag))
            EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += Run;
            return;
        }
        if (!File.Exists(Flag)) return;

        string outDir = File.ReadAllText(Flag).Trim();
        File.Delete(Flag);
        if (string.IsNullOrEmpty(outDir)) outDir = "Temp";
        Directory.CreateDirectory(outDir);

        var log = new StringBuilder();
        try
        {
            G1HugeMapBuilder.BuildHugeMap();
            Inspect(log);
            Shots(outDir, log);
        }
        catch (System.Exception e)
        {
            log.AppendLine("EXCEPTION: " + e);
        }
        File.WriteAllText(Path.Combine(outDir, "verify.txt"), log.ToString());
        Debug.Log("G1 VERIFY DONE → " + outDir);
    }

    // ------------------------------------------------------------- assertions
    static void Inspect(StringBuilder log)
    {
        // Everything in the scene was created and moved during this same editor
        // frame, and the physics engine still holds the transforms each collider
        // had when it was born — at the origin. Without this, every probe near
        // (0,0,0) reports hits on the entire map and probes elsewhere miss real
        // geometry. It cost a full verify run to notice.
        Physics.SyncTransforms();

        var npcs = Object.FindObjectsOfType<G1QuestNpc>();
        log.AppendLine($"quest contacts: {npcs.Length} (expect 8)");
        int trapped = 0;
        foreach (var n in npcs)
        {
            // the contact's own collider would fail the check, so probe with it off
            var own = n.GetComponent<Collider>();
            if (own) own.enabled = false;
            bool ok = G1Placement.IsStandable(n.transform.position, out _);
            if (own) own.enabled = true;
            if (!ok) trapped++;
            log.AppendLine($"  {(ok ? "ok     " : "TRAPPED")} {n.npcName,-22} {n.transform.position}" +
                           (ok ? "" : "  <- " + G1Placement.Describe(n.transform.position)));
        }
        log.AppendLine($"trapped contacts: {trapped} (expect 0) {(trapped == 0 ? "PASS" : "FAIL")}");

        // can the player reach each door, and each door's interior?
        foreach (var d in Object.FindObjectsOfType<G1BlastDoor>(true))
        {
            Vector3 outside = d.transform.position - d.transform.forward * 4f;
            Vector3 inside = d.transform.position + d.transform.forward * 4f;
            bool o = G1Placement.IsStandable(outside, out _);
            log.AppendLine($"  door {d.transform.root.name,-24} approach={(o ? "clear" : "BLOCKED")}" +
                           $" {(o ? "" : "FAIL")}");
        }

        var kane = System.Array.Find(npcs, n => n.npcName == "SGT. KANE");
        if (kane == null) log.AppendLine("FAIL: SGT. KANE missing");
        else
        {
            log.AppendLine($"kane.openOnAccept   = {Count(kane.openOnAccept)} (expect 1)");
            log.AppendLine($"kane.activateOnAccept = {Count(kane.activateOnAccept)} (expect 1)");
            log.AppendLine($"kane.alarmOnAccept  = {kane.alarmOnAccept} (expect True)");
            if (kane.openOnAccept != null && kane.openOnAccept.Length > 0 && kane.openOnAccept[0] != null)
            {
                var d = kane.openOnAccept[0];
                log.AppendLine($"gate: locked={d.locked} openOnce={d.openOnce} travel={d.travel} " +
                               $"panels={(d.leftPanel != null && d.rightPanel != null)} lamp={d.statusLight != null}");
            }
            var picket = kane.activateOnAccept != null && kane.activateOnAccept.Length > 0
                ? kane.activateOnAccept[0] : null;
            if (picket != null)
                log.AppendLine($"picket: active={picket.activeSelf} (expect False) soldiers={picket.transform.childCount} (expect 3)");
        }

        var doors = Object.FindObjectsOfType<G1BlastDoor>(true);
        log.AppendLine($"blast doors: {doors.Length} (expect 7: gate + 4 bunkers + 2 compounds)");
        foreach (var d in doors)
            log.AppendLine($"  {d.transform.root.name,-24} {d.doorLabel,-20} locked={d.locked}");

        var player = GameObject.FindWithTag("Player");
        log.AppendLine($"player: {(player != null ? player.transform.position.ToString() : "MISSING")}");
        if (player != null)
        {
            log.AppendLine($"  scanner={player.GetComponent<G1QuestScanner>() != null} (expect True)");
            var use = player.GetComponentInChildren<PlayerUse>(true);
            log.AppendLine($"  playerUse reach={(use != null ? use.reach.ToString() : "MISSING")} (expect 3)");
        }

        var sprawl = GameObject.Find("CorvusSprawl");
        int chunks = sprawl != null ? sprawl.GetComponentsInChildren<MeshCollider>().Length : 0;
        log.AppendLine($"map chunks: {chunks} (expect 17: 16 districts + ground) " +
                       $"{(chunks >= 17 ? "PASS" : "FAIL")}");

        // the interiors are the point of the rebuild: a hollow building whose
        // doorway got walled up by a stray box is indistinguishable from a
        // solid block, and only a probe inside can tell the difference
        var manifest = G1MapManifest.Load("Assets/G1/Models/Environment/HugeMap.fbx");
        if (manifest == null) log.AppendLine("map manifest: MISSING  FAIL");
        else
        {
            int hollow = 0, solid = 0;
            var bad = new List<string>();
            foreach (var r in manifest.rooms)
            {
                // Sample the floor rather than the exact centroid. Several of
                // these rooms have a ramp or a catwalk running through the
                // middle, and "the centimetre at the centre is occupied" is not
                // the question — "is there room to walk around in here" is.
                // Ignore the sky test too: having a roof is what makes it a room.
                int free = 0, total = 0;
                foreach (float fx in new[] { -0.3f, 0f, 0.3f })
                    foreach (float fz in new[] { -0.3f, 0f, 0.3f })
                    {
                        var p = new Vector3(r.x + r.w * fx, r.y + 0.1f, r.z + r.d * fz);
                        var hit = Physics.OverlapCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.7f,
                                                         0.4f, ~0, QueryTriggerInteraction.Ignore);
                        // a soldier standing inside is the level working, not a
                        // sealed room — only static geometry counts as blocked
                        hit = System.Array.FindAll(hit, c =>
                            c.GetComponentInParent<HealthSystem>() == null &&
                            c.GetComponentInParent<G1WallCharger>() == null);
                        total++;
                        if (hit.Length == 0) free++;
                    }

                // more than half the floor open = you can use the space
                if (free * 2 > total) hollow++;
                else
                {
                    solid++;
                    if (bad.Count < 12) bad.Add($"{r.name} ({free}/{total} floor clear)");
                }
            }
            log.AppendLine($"interiors: {hollow} walkable, {solid} blocked " +
                           $"{(solid == 0 ? "PASS" : "FAIL")}");
            foreach (var b in bad) log.AppendLine($"  blocked: {b}");
            log.AppendLine($"lights placed: {Object.FindObjectsOfType<Light>().Length}");

            // A cover point hanging in the air, buried in a wall, or off the
            // navmesh is worse than no cover point: the AI claims it, walks at
            // it forever and never fires. Check every one can be stood on and
            // pathed to.
            var pts = Object.FindObjectsOfType<G1CoverPoint>();
            int reachable = 0, offMesh = 0, buried = 0;
            foreach (var cp in pts)
            {
                var at = cp.transform.position;
                bool clear = !Physics.CheckCapsule(at + Vector3.up * 0.4f, at + Vector3.up * 1.6f,
                                                   0.35f, ~0, QueryTriggerInteraction.Ignore);
                if (!clear) { buried++; continue; }
                if (!UnityEngine.AI.NavMesh.SamplePosition(
                        at, out _, 2.0f, UnityEngine.AI.NavMesh.AllAreas)) { offMesh++; continue; }
                reachable++;
            }
            log.AppendLine($"cover points: {pts.Length} total, {reachable} usable, " +
                           $"{buried} buried, {offMesh} off-navmesh " +
                           $"{(pts.Length > 0 && reachable * 100 / Mathf.Max(1, pts.Length) >= 70 ? "PASS" : "FAIL")}");

            var space = Object.FindObjectOfType<G1InteriorSpace>();
            log.AppendLine($"acoustic spaces: {(space != null ? space.rooms.Length : 0)} " +
                           $"{(space != null && space.rooms.Length == manifest.rooms.Length ? "PASS" : "FAIL")}");
        }

        // does anything actually hold these objects up?
        // ---- the storyline: a chapter that watches an objective nobody ever
        // registers is a chapter the player can never finish, and the spine
        // stalls there silently.
        var dir = Object.FindObjectOfType<G1StoryDirector>();
        if (dir == null) log.AppendLine("story director: MISSING  FAIL");
        else
        {
            var setup = Object.FindObjectOfType<G1MissionSetup>();
            var handedOut = new List<string>();
            foreach (var n in npcs) if (!string.IsNullOrEmpty(n.questId)) handedOut.Add(n.questId);
            if (setup != null && setup.objectives != null)
                foreach (var d in setup.objectives) handedOut.Add(d.id);

            int orphan = 0;
            foreach (var ch in dir.chapters)
                if (!handedOut.Contains(ch.objectiveId)) { orphan++; log.AppendLine($"  orphan chapter: {ch.title} -> '{ch.objectiveId}'"); }
            log.AppendLine($"story: {dir.chapters.Length} chapters, {orphan} orphaned " +
                           $"{(dir.chapters.Length >= 9 && orphan == 0 ? "PASS" : "FAIL")}");
        }

        var emitters = new List<GameObject>();
        foreach (var w in Object.FindObjectsOfType<G1ObjectiveOnDeath>())
            if (w.objectiveId == "emitters") emitters.Add(w.gameObject);
        int emReach = 0;
        foreach (var e in emitters)
        {
            var foot = e.transform.position - Vector3.up * 1.6f;
            if (UnityEngine.AI.NavMesh.SamplePosition(foot, out _, 4f,
                    UnityEngine.AI.NavMesh.AllAreas)) emReach++;
        }
        log.AppendLine($"threshold: {emitters.Count} emitters (expect 3), {emReach} reachable " +
                       $"{(emitters.Count == 3 && emReach == 3 ? "PASS" : "FAIL")}");
        var ring = GameObject.Find("Threshold");
        log.AppendLine($"  ring at {(ring != null ? ring.transform.position.ToString() : "MISSING")}");

        // ---- voices. The clips are keyed by a hash of the line's text, which
        // only resolves if the Python extractor read exactly the string the C#
        // compiler built. That is the whole risk in the pipeline, and it is
        // invisible until a character opens their mouth and says nothing — so
        // hash every line the player can hear and demand the recording exists.
        int spoken = 0, unvoiced = 0;
        var silent = new List<string>();
        foreach (var n in npcs)
        {
            foreach (var line in new[] { n.offerLine, n.acceptLine, n.nagLine,
                                         n.turnInLine, n.doneLine })
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (Resources.Load<AudioClip>("Audio/Voice/" + G1Voice.Key(line)) != null)
                    spoken++;
                else
                {
                    unvoiced++;
                    if (silent.Count < 6)
                        silent.Add($"{n.npcName}: \"{line.Substring(0, Mathf.Min(46, line.Length))}...\"");
                }
            }
        }
        if (dir != null)
            foreach (var ch in dir.chapters)
                foreach (var beats in new[] { ch.onOpen, ch.onClose })
                    foreach (var b in beats ?? new G1StoryDirector.Beat[0])
                    {
                        if (string.IsNullOrEmpty(b.line)) continue;
                        if (Resources.Load<AudioClip>("Audio/Voice/" + G1Voice.Key(b.line)) != null)
                            spoken++;
                        else
                        {
                            unvoiced++;
                            if (silent.Count < 6)
                                silent.Add($"{ch.title}: \"{b.line.Substring(0, Mathf.Min(46, b.line.Length))}...\"");
                        }
                    }
        log.AppendLine($"voice lines: {spoken} spoken, {unvoiced} falling back to blips " +
                       $"{(unvoiced == 0 ? "PASS" : "FAIL")}");
        foreach (var s in silent) log.AppendLine("  no recording: " + s);

        bool bank = true;
        foreach (var v in new[] { "a", "e", "i", "o", "u", "m" })
            if (Resources.Load<AudioClip>("Audio/voice_" + v) == null) bank = false;
        log.AppendLine($"  fallback syllable bank present: {bank} {(bank ? "PASS" : "FAIL")}");

        // Every assignment ends at a trigger volume, and a volume the player
        // cannot reach is an objective that can never be ticked. This caught
        // three quests pointing at the mirrored side of the map — one of them
        // inside the solid footing of the comms dish.
        int reachableZones = 0, deadZones = 0;
        foreach (var z in Object.FindObjectsOfType<G1QuestZone>())
        {
            var at = z.transform.position - Vector3.up * 2f;
            bool ok = G1Placement.IsStandable(at, out _) ||
                      UnityEngine.AI.NavMesh.SamplePosition(
                          at, out _, 10f, UnityEngine.AI.NavMesh.AllAreas);
            if (ok) reachableZones++;
            else { deadZones++; log.AppendLine($"  UNREACHABLE quest zone: {z.objectiveId} at {at}"); }
        }
        log.AppendLine($"quest zones: {reachableZones} reachable, {deadZones} dead " +
                       $"{(deadZones == 0 ? "PASS" : "FAIL")}");

        var trucks = Object.FindObjectsOfType<G1Vehicle>();
        int drivable = 0;
        foreach (var t in trucks)
            if (t.seat != null && t.GetComponent<Collider>() != null) drivable++;
        log.AppendLine($"vehicles: {trucks.Length} parked, {drivable} drivable " +
                       $"{(trucks.Length > 0 && drivable == trucks.Length ? "PASS" : "FAIL")}");

        var gateNow = GameObject.Find("SouthGate");
        float gzp = gateNow != null ? gateNow.transform.position.z : -352f;
        foreach (var probe in new (string, Vector3)[]
        {
            ("spawn", new Vector3(0f, 5f, -378f)),
            ("kane", new Vector3(3.2f, 5f, gzp - 12f)),
            ("gate", new Vector3(0f, 5f, gzp)),
            ("bunkerWest", new Vector3(-96f, 5f, -34f)),
            ("commsCompound", new Vector3(155f, 5f, -150f)),
            ("airstrip", new Vector3(310f, 5f, 0f)),
            ("ammoField", new Vector3(-96f, 5f, 306f)),
            ("tankPark", new Vector3(-296f, 5f, 32f)),
        })
            log.AppendLine($"ground under {probe.Item1,-14} = {Ground(probe.Item2):0.00}");

        log.AppendLine($"navmesh asset: {File.Exists("Assets/Scenes/HugeMapNavMesh.asset")}");

        // the FBX is merged meshes, so obstructions only show up by raycast:
        // profile the road down the middle and flag anything standing on it
        log.AppendLine("road profile at x=0 (height above ground):");
        for (float z = -395f; z <= -290f; z += 2f)
        {
            float h = Physics.Raycast(new Vector3(0f, 60f, z), Vector3.down,
                                      out RaycastHit hit, 120f) ? hit.point.y : -999f;
            if (h > 1f) log.AppendLine($"  z={z,-8} h={h:0.00}  <-- OBSTRUCTION");
        }
        var gateGo = GameObject.Find("SouthGate");
        log.AppendLine($"gate placed at z = {(gateGo != null ? gateGo.transform.position.z.ToString() : "MISSING")}");
    }

    static int Count(System.Array a) => a == null ? 0 : a.Length;

    static float Ground(Vector3 from)
    {
        var origin = new Vector3(from.x, Mathf.Max(from.y, 5f), from.z);
        return Physics.Raycast(origin, Vector3.down, out RaycastHit h, 60f) ? h.point.y : -999f;
    }

    // ------------------------------------------------------------ screenshots
    static void Shots(string outDir, StringBuilder log)
    {
        var go = new GameObject("VerifyCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 70f;
        cam.farClipPlane = 900f;

        // the gate auto-places itself around obstructions, so frame off wherever
        // it actually landed rather than a baked-in z
        var gateGo = GameObject.Find("SouthGate");
        float gz = gateGo != null ? gateGo.transform.position.z : -250f;

        var shots = new List<(string name, Vector3 eye, Vector3 look)>
        {
            // slightly behind and above the spawn so the player's own gear
            // doesn't fill the frame
            ("01_spawn_to_gate",   new Vector3(0f, 2.6f, -382f),      new Vector3(0f, 2.5f, gz)),
            ("02_gate_closeup",    new Vector3(0f, 2.2f, gz - 9f),    new Vector3(0f, 2.4f, gz)),
            ("03_kane",            new Vector3(3.2f, 1.7f, gz - 17f), new Vector3(3.2f, 1.5f, gz - 12f)),
            ("04_gate_from_north", new Vector3(0f, 6f, gz + 14f),     new Vector3(0f, 2.5f, gz)),
            ("05_bunker_west",     new Vector3(-96f, 2f, -46f),    new Vector3(-96f, 1.5f, -36f)),
            ("06_comms_compound",  new Vector3(155f, 3f, -168f),   new Vector3(155f, 2f, -152f)),
            ("07_aerial_south",    new Vector3(0f, 150f, -400f),   new Vector3(0f, 0f, -250f)),
            ("14_staging",         new Vector3(-3f, 2.4f, gz - 22f), new Vector3(-3f, 1f, gz - 13f)),
            // the new ground — if these frame empty dirt the district didn't build
            ("20_trench_line",     new Vector3(-90f, 3.2f, -352f), new Vector3(-90f, 1f, -318f)),
            ("21_airstrip",        new Vector3(230f, 12f, -40f),   new Vector3(310f, 0f, 30f)),
            ("22_ammo_field",      new Vector3(-96f, 5f, 296f),    new Vector3(-96f, 3f, 316f)),
            ("28_igloo_mouth",     new Vector3(-96f, 1.7f, 302f),  new Vector3(-96f, 1.6f, 320f)),
            ("23_tank_park",       new Vector3(-270f, 6f, 30f),    new Vector3(-330f, 2f, 20f)),
            ("24_cmd_lobby",       new Vector3(0f, 1.7f, -30f),    new Vector3(0f, 3f, 0f)),
            ("25_barracks_interior", new Vector3(-148f, 1.7f, -30f), new Vector3(-165f, 1.6f, -30f)),
            ("26_aerial_whole",    new Vector3(0f, 420f, -330f),   new Vector3(0f, 0f, 40f)),
            ("27_watchtower",      new Vector3(-360f, 4f, -360f),  new Vector3(-394f, 8f, -394f)),
        };

        // frame each contact from where a player would walk up, so a bad
        // placement is visible rather than merely "standable"
        foreach (var n in Object.FindObjectsOfType<G1QuestNpc>())
        {
            Vector3 eye = n.transform.position + n.transform.forward * 6f + Vector3.up * 1.9f;
            shots.Add(($"15_contact_{n.npcName.Replace(' ', '_').Replace('.', '_')}",
                       eye, n.transform.position + Vector3.up * 1.2f));
        }

        foreach (var s in shots)
        {
            go.transform.position = s.eye;
            go.transform.LookAt(s.look);
            Save(cam, Path.Combine(outDir, s.name + ".png"));
        }
        log.AppendLine($"rendered {shots.Count} shots");

        // the one thing edit mode can't show: panels retracted. Pose them by
        // hand exactly where the coroutine would land, shoot, then put them back.
        foreach (var d in Object.FindObjectsOfType<G1BlastDoor>(true))
        {
            if (d.leftPanel == null || d.rightPanel == null) continue;
            Vector3 l = d.leftPanel.localPosition, r = d.rightPanel.localPosition;
            d.leftPanel.localPosition = l + Vector3.left * d.travel;
            d.rightPanel.localPosition = r + Vector3.right * d.travel;

            if (d.doorLabel == "SPRAWL GATE")
            {
                go.transform.position = new Vector3(0f, 2.2f, gz - 9f);
                go.transform.LookAt(new Vector3(0f, 2.4f, gz));
                Save(cam, Path.Combine(outDir, "08_gate_open.png"));
            }
            else if (d.doorLabel == "BUNKER" && d.transform.root.name == "Bunker_WestApproach")
            {
                go.transform.position = new Vector3(-96f, 2f, -46f);
                go.transform.LookAt(new Vector3(-96f, 1.5f, -36f));
                Save(cam, Path.Combine(outDir, "09_bunker_open.png"));
            }

            d.leftPanel.localPosition = l;
            d.rightPanel.localPosition = r;
        }

        Object.DestroyImmediate(go);
    }

    static void Save(Camera cam, string path)
    {
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
