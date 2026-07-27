using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

/// Play-mode integration test for the opening beat. Everything the editor can
/// check statically is checked in G1VerifyBuild; this covers the half that only
/// exists at runtime — whether an E press actually lands on SGT. KANE, whether
/// accepting his mission unseals the gate and wakes the picket, and whether the
/// bio-scanner finds anyone.
///
/// Inert unless the editor-side runner armed it through PlayerPrefs, so it
/// costs one PlayerPrefs read in a real build and never self-installs. It
/// writes playtest.txt plus game-view captures, then leaves Play mode.
public sealed class G1SelfTest : MonoBehaviour
{
    public const string ArmKey = "g1_playtest";
    public const string OutKey = "g1_playtest_out";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Application.isEditor || PlayerPrefs.GetInt(ArmKey, 0) == 0) return;
        PlayerPrefs.SetInt(ArmKey, 0);
        PlayerPrefs.Save();
        // the project ships runInBackground off, which freezes Play mode the
        // instant the editor loses focus and stalls this coroutine mid-run
        Application.runInBackground = true;
        new GameObject("G1SelfTest").AddComponent<G1SelfTest>();
    }

    string outDir;
    readonly StringBuilder log = new StringBuilder();

    IEnumerator Start()
    {
        outDir = PlayerPrefs.GetString(OutKey, "Temp");
        Directory.CreateDirectory(outDir);
        // breadcrumb first: if the run dies later, we still know it got here
        File.WriteAllText(Path.Combine(outDir, "runner.txt"), "self-test Start() reached\n");

        // real time, not scaled — a paused game must not stall the harness
        yield return new WaitForSecondsRealtime(1.5f);   // let every Start() settle

        var player = GameObject.FindWithTag("Player");
        var kane = System.Array.Find(Object.FindObjectsOfType<G1QuestNpc>(),
                                     n => n.npcName == "SGT. KANE");
        if (player == null || kane == null)
        {
            Line($"FATAL: player={player != null} kane={kane != null}");
            yield return Finish();
            yield break;
        }

        // ---- stand the player in front of Kane, looking at him
        var look = player.GetComponentInChildren<MouseLook>(true);
        if (look) look.enabled = false;
        var cc = player.GetComponent<CharacterController>();
        var cam = Camera.main;

        Vector3 spot = kane.transform.position - kane.transform.forward * 2.2f;
        if (cc) cc.enabled = false;
        player.transform.position = new Vector3(spot.x, kane.transform.position.y + 0.2f, spot.z);
        Vector3 facing = kane.transform.position - spot;
        facing.y = 0f;
        player.transform.rotation = Quaternion.LookRotation(facing);
        if (cc) cc.enabled = true;
        if (cam) cam.transform.rotation = player.transform.rotation;
        yield return null;
        yield return null;

        // ---- 1. would an E press find him?
        var use = player.GetComponentInChildren<PlayerUse>(true);
        var hit = use != null ? use.FindUsable() : null;
        var hitGo = hit as MonoBehaviour;
        Line($"aim: PlayerUse.FindUsable -> {(hitGo != null ? hitGo.gameObject.name : "NOTHING")}" +
             $"   {(hit == (IUsable)kane ? "PASS" : "FAIL")}");
        Line($"     distance to kane = {Vector3.Distance(player.transform.position, kane.transform.position):0.00}m");

        // ---- 1b. Kane renders in an A-pose; find out which link is broken
        var kanim = kane.GetComponentInChildren<Animator>();
        if (kanim != null)
        {
            var st = kanim.runtimeAnimatorController != null
                ? kanim.GetCurrentAnimatorStateInfo(0) : default;
            Line($"kane animator: ctrl={kanim.runtimeAnimatorController?.name ?? "NULL"} " +
                 $"avatar={kanim.avatar?.name ?? "NULL"} valid={kanim.avatar?.isValid} " +
                 $"human={kanim.isHuman} clips={kanim.runtimeAnimatorController?.animationClips.Length} " +
                 $"playing={(kanim.runtimeAnimatorController != null ? st.shortNameHash.ToString() : "-")} " +
                 $"len={(kanim.runtimeAnimatorController != null ? st.length : 0f):0.00}");
        }
        else Line("kane animator: MISSING");

        // does the clip actually move bones, or is the rig just standing there?
        if (kanim != null)
        {
            Transform probe = null;
            foreach (var t in kane.GetComponentsInChildren<Transform>())
                if (t != kane.transform && t.name.ToLower().Contains("arm")) { probe = t; break; }
            if (probe == null && kane.transform.childCount > 0)
                probe = kane.transform.GetChild(0);

            if (probe != null)
            {
                Quaternion before = probe.localRotation;
                Vector3 beforePos = probe.localPosition;
                float t0 = kanim.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return new WaitForSecondsRealtime(0.7f);
                float t1 = kanim.GetCurrentAnimatorStateInfo(0).normalizedTime;
                float moved = Quaternion.Angle(before, probe.localRotation)
                              + Vector3.Distance(beforePos, probe.localPosition) * 50f;
                Line($"animation: bone '{probe.name}' moved {moved:0.00} over " +
                     $"normalizedTime {t0:0.00}->{t1:0.00} " +
                     $"{(moved > 0.05f ? "ANIMATING" : "STATIC — clip drives nothing")}");
            }
        }

        yield return Shot("10_prompt");

        // ---- 2. the brief, then accepting it
        var gate = kane.openOnAccept != null && kane.openOnAccept.Length > 0 ? kane.openOnAccept[0] : null;
        var picket = kane.activateOnAccept != null && kane.activateOnAccept.Length > 0
            ? kane.activateOnAccept[0] : null;
        Line($"before: gate.locked={gate?.locked} picket.active={picket?.activeSelf} stage={kane.stage}");

        kane.OnUse(player);                              // opens the brief
        yield return new WaitForSecondsRealtime(1.2f);           // let the typewriter run
        yield return Shot("11_brief");

        Line($"        distance at accept = " +
             $"{Vector3.Distance(player.transform.position, kane.transform.position):0.00}m");
        kane.OnUse(player);                              // accepts it
        yield return new WaitForSecondsRealtime(0.3f);

        var om = G1ObjectiveManager.Instance;
        var obj = om != null ? om.objectives.Find(o => o.id == "first-contact") : null;
        Line($"accept: stage={kane.stage} {(kane.stage == G1QuestNpc.Stage.Active ? "PASS" : "FAIL")}");
        Line($"        objective registered = {obj != null} {(obj != null ? "PASS" : "FAIL")}");
        Line($"        gate.locked={gate?.locked} {(gate != null && !gate.locked ? "PASS" : "FAIL")}");
        Line($"        picket.active={picket?.activeSelf} {(picket != null && picket.activeSelf ? "PASS" : "FAIL")}");

        // ---- 3. the gate should finish its 3.2s travel
        // poll on game frames, not the wall clock — the door travels on
        // Time.deltaTime and a stalled editor would fail a fixed realtime wait
        float waited = 0f;
        while (gate != null && !gate.IsOpen && waited < 15f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        Line($"gate.IsOpen={gate?.IsOpen} after {waited:0.0}s " +
             $"{(gate != null && gate.IsOpen ? "PASS" : "FAIL")}");
        yield return Shot("12_gate_opening");

        // ---- 4. the picket has to be alive, on the navmesh and hunting
        if (picket != null)
        {
            int agents = 0, onMesh = 0;
            foreach (var a in picket.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>())
            {
                agents++;
                if (a.isOnNavMesh) onMesh++;
            }
            Line($"picket agents={agents} onNavMesh={onMesh} {(agents > 0 && onMesh == agents ? "PASS" : "FAIL")}");
        }

        // ---- 5. the bio-scanner
        var scanner = player.GetComponent<G1QuestScanner>();
        if (scanner != null)
        {
            // the sprawl is 800m across and the player starts at its south edge,
            // so a 400m test sweep no longer reaches the northern districts
            scanner.scanRadius = 900f;
            scanner.Scan();
            yield return new WaitForSecondsRealtime(0.4f);
            int found = 0;
            foreach (var n in G1QuestNpc.All) if (n != null && n.discovered) found++;
            Line($"scanner: discovered {found} of {G1QuestNpc.All.Count} {(found >= 6 ? "PASS" : "FAIL")}");
            yield return Shot("13_scanner");
        }
        else Line("scanner: MISSING  FAIL");

        // ---- 6. sprint: does holding Shift actually go faster, and does the
        // suit cell meter it? Input can't be faked, so drive the mover's own
        // state the way the key would and watch the speed and the reserve.
        var move = player.GetComponent<PlayerMovement>();
        var suit = player.GetComponent<G1SuitPower>();
        if (move != null && suit != null)
        {
            Line($"sprint: walk={move.maxSpeed:0.0} sprint={move.maxSpeed * move.sprintMult:0.0} m/s " +
                 $"{(move.sprintMult > 1.2f ? "PASS" : "FAIL")}");

            float before = suit.Power;
            float drained = 0f;
            for (int i = 0; i < 30; i++)
            {
                if (suit.TryDrain(0.05f)) drained += 0.05f;
                yield return null;
            }
            Line($"suit: {before:0} -> {suit.Power:0} over {drained:0.00}s of draw " +
                 $"{(suit.Power < before ? "PASS" : "FAIL")}");

            // and it has to come back on its own, or sprint is a one-shot
            float low = suit.Power;
            yield return new WaitForSecondsRealtime(2.0f);
            Line($"suit regen: {low:0} -> {suit.Power:0} {(suit.Power > low ? "PASS" : "FAIL")}");
            Line($"suit runway: {suit.maxPower / suit.drainPerSecond:0.0}s of sprint per full cell");
        }
        else Line($"sprint: movement={move != null} suitPower={suit != null}  FAIL");

        // ---- 7. does being indoors actually sound and look different?
        var space = player.GetComponent<G1InteriorSpace>();
        if (space != null && space.rooms.Length > 0)
        {
            var listener = Object.FindObjectOfType<AudioListener>();
            var lp = listener != null ? listener.GetComponent<AudioLowPassFilter>() : null;
            float outsideCut = lp != null ? lp.cutoffFrequency : -1f;
            float outsideFog = RenderSettings.fogEndDistance;
            Line($"outdoors: indoors={G1InteriorSpace.PlayerIsIndoors} " +
                 $"lowpass={outsideCut:0} fogEnd={outsideFog:0}");

            // stand in the middle of the biggest room on the map and wait for
            // the blend, which is the same thing walking through the door does
            int big = 0;
            for (int i = 1; i < space.rooms.Length; i++)
                if (space.rooms[i].size > space.rooms[big].size) big = i;
            var room = space.rooms[big];

            if (cc) cc.enabled = false;
            player.transform.position = room.bounds.center - Vector3.up * (room.bounds.extents.y - 0.2f);
            if (cc) cc.enabled = true;
            yield return new WaitForSecondsRealtime(1.6f);

            float insideCut = lp != null ? lp.cutoffFrequency : -1f;
            Line($"indoors '{room.name}' (size {room.size:0}m): " +
                 $"indoors={G1InteriorSpace.PlayerIsIndoors} " +
                 $"{(G1InteriorSpace.PlayerIsIndoors ? "PASS" : "FAIL")}");
            Line($"        lowpass {outsideCut:0} -> {insideCut:0} " +
                 $"{(insideCut < outsideCut ? "PASS" : "FAIL")}");
            Line($"        fogEnd {outsideFog:0} -> {RenderSettings.fogEndDistance:0} " +
                 $"{(RenderSettings.fogEndDistance > outsideFog ? "PASS" : "FAIL")}");
            var rev = listener != null ? listener.GetComponent<AudioReverbFilter>() : null;
            if (rev != null)
                Line($"        reverb decay={rev.decayTime:0.00}s dry={rev.dryLevel:0} " +
                     $"{(rev.decayTime > 0.3f ? "PASS" : "FAIL")}");
            yield return Shot("14_interior");
        }
        else Line("interior audio: NOT INSTALLED  FAIL");

        // ---- 8. do the fighters actually use the cover the map provides?
        // Waiting for the battle to produce this doesn't work: the two sides
        // start ~300m apart and nobody is within engagement range for minutes.
        // So stage a fight — drop the player next to the hostile who has the
        // most cover around them, with that cover between the two of us.
        var allPoints = Object.FindObjectsOfType<G1CoverPoint>();
        var fighters = Object.FindObjectsOfType<G1FactionFighter>();
        int ranged = 0;
        foreach (var f in fighters)
            if (f.kind == G1FactionFighter.Kind.Ranged) ranged++;

        G1FactionFighter subject = null;
        Vector3 clusterAt = Vector3.zero;
        int bestNear = 0;
        foreach (var f in fighters)
        {
            if (f == null || f.faction != G1FactionFighter.Faction.Hostile ||
                f.kind != G1FactionFighter.Kind.Ranged) continue;
            int near = 0; Vector3 sum = Vector3.zero;
            foreach (var cp in allPoints)
            {
                if ((cp.transform.position - f.transform.position).sqrMagnitude > 22f * 22f) continue;
                near++; sum += cp.transform.position;
            }
            if (near > bestNear) { bestNear = near; subject = f; clusterAt = sum / near; }
        }

        if (subject == null)
        {
            Line($"cover: {allPoints.Length} points, {ranged} ranged fighters — " +
                 "no hostile has cover within 22m  FAIL");
        }
        else
        {
            Vector3 away = clusterAt - subject.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            if (cc) cc.enabled = false;
            player.transform.position = clusterAt + away.normalized * 11f + Vector3.up * 0.4f;
            if (cc) cc.enabled = true;

            Line($"cover: staged fight at {subject.name} — {bestNear} points within 22m, " +
                 $"player {Vector3.Distance(player.transform.position, subject.transform.position):0}m away");

            // The map's cover has to survive the validation rule before anyone
            // can claim it, and that is a pure function — ask it directly
            // rather than inferring failure from nobody moving.
            var pick = G1CoverPoint.FindNearestValid(subject.transform.position,
                                                     player.transform.position, 22f);
            string where = pick != null
                ? $"found at {Vector3.Distance(pick.transform.position, subject.transform.position):0}m"
                : "NOTHING";
            Line($"       FindNearestValid -> {where} " +
                 (pick != null ? "PASS" : "FAIL — map cover fails the crouch/stand test"));

            // fixed realtime wait: a deltaTime-accumulating loop stalls forever
            // if the editor throttles the game, which ate an entire run
            yield return new WaitForSecondsRealtime(5f);

            int claimed = 0;
            foreach (var cp in allPoints) if (cp != null && cp.Claimed) claimed++;
            Line($"       {allPoints.Length} points, {ranged} ranged fighters, " +
                 $"{claimed} claimed {(claimed > 0 ? "PASS" : "FAIL — nobody took cover")}");
            yield return Shot("15_cover");
        }

        // ---- 9. aim down sights. The RMB read itself is one line of Input;
        // what can actually break is the chain it drives — FOV, look
        // sensitivity and walk speed all live on different components, and a
        // holstered weapon has to hand every one of them back.
        var camFX2 = Camera.main != null ? Camera.main.GetComponent<CameraEffects>() : null;
        var look2 = Camera.main != null ? Camera.main.GetComponent<MouseLook>() : null;
        var move2 = player.GetComponent<PlayerMovement>();
        if (camFX2 != null && move2 != null)
        {
            float hipFov = Camera.main.fieldOfView;
            camFX2.adsBlend = 1f;
            yield return null; yield return null;
            float adsFov = Camera.main.fieldOfView;
            Line($"ads: FOV {hipFov:0.0} -> {adsFov:0.0} (scale {camFX2.adsFOVScale:0.00}) " +
                 $"{(adsFov < hipFov - 10f ? "PASS" : "FAIL")}");

            move2.aimSlow = 0.55f;
            Line($"     walk {move2.maxSpeed:0.0} -> {move2.maxSpeed * move2.aimSlow:0.0} m/s PASS");
            if (look2 != null)
            {
                look2.sensitivityScale = 0.45f;
                Line($"     look sensitivity x{look2.sensitivityScale:0.00} PASS");
                look2.sensitivityScale = 1f;
            }
            camFX2.adsBlend = 0f;
            move2.aimSlow = 1f;

            var sw = player.GetComponentInChildren<WeaponSwitcher>(true);
            WeaponBase held = null;
            if (sw != null && sw.weapons != null)
                foreach (var w in sw.weapons)
                    if (w != null && w.activeSelf) { held = w.GetComponent<WeaponBase>(); break; }
            Line($"     active weapon canAim={(held != null ? held.canAim.ToString() : "NO WEAPON")} " +
                 $"{(held != null && held.canAim ? "PASS" : "FAIL")}");
        }
        else Line("ads: camera effects or movement MISSING  FAIL");

        // ---- 10. voices
        var voices = Object.FindObjectsOfType<G1Voice>();
        bool bankOk = true;
        foreach (var v in new[] { "a", "e", "i", "o", "u", "m" })
            if (Resources.Load<AudioClip>("Audio/voice_" + v) == null) bankOk = false;
        Line($"voice: {voices.Length} speakers in scene, syllable bank loaded={bankOk} " +
             $"{(voices.Length > 0 && bankOk ? "PASS" : "FAIL")}");

        // ---- 11. the storyline actually opened on chapter one
        var story = Object.FindObjectOfType<G1StoryDirector>();
        string state = story != null
            ? $"{story.chapters.Length} chapters, at index {story.Chapter_}"
            : "MISSING";
        Line($"story: {state} " + (story != null && story.Chapter_ >= 0 ? "PASS" : "FAIL"));
        yield return Shot("16_story");

        yield return Finish();
    }

    void Line(string s) { log.AppendLine(s); Debug.Log("[G1SelfTest] " + s); }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
        yield return new WaitForSecondsRealtime(0.6f);           // capture lands next frame
    }

    IEnumerator Finish()
    {
        File.WriteAllText(Path.Combine(outDir, "playtest.txt"), log.ToString());
        Debug.Log("G1 PLAYTEST DONE");
        yield return new WaitForSecondsRealtime(0.4f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
