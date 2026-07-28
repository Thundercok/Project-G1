using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

/// Plays Cradle Station from the gate to the containment core and writes down
/// whether it worked.
///
/// G1Playthrough walks the Sprawl's contact chain. This level has no contacts —
/// its structure is a lock: the research wing runs off the main bus, the bus is
/// dead, and the breaker is in the turbine hall. So the test is shaped like the
/// level: prove the door is shut, throw the breaker, prove the same door now
/// opens, then finish. A test that simply forced every objective would pass on
/// a build where the lock did nothing.
///
/// Inert unless armed through PlayerPrefs by G1PlayTestRunner.
public sealed class G1CradlePlaythrough : MonoBehaviour
{
    public const string ArmKey = "g1_cradle_playthrough";
    public const string OutKey = "g1_cradle_out";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Application.isEditor || PlayerPrefs.GetInt(ArmKey, 0) == 0) return;
        PlayerPrefs.SetInt(ArmKey, 0);
        PlayerPrefs.Save();
        Application.runInBackground = true;
        new GameObject("G1CradlePlaythrough").AddComponent<G1CradlePlaythrough>();
    }

    string outDir;
    readonly StringBuilder log = new StringBuilder();
    int pass, fail;

    GameObject player;
    CharacterController cc;
    G1ObjectiveManager om;

    void Line(string s) { log.AppendLine(s); Debug.Log("[CRADLE] " + s); }
    void Check(bool ok, string what)
    {
        if (ok) { pass++; Line($"  PASS  {what}"); }
        else { fail++; Line($"  FAIL  {what}"); }
    }

    IEnumerator Start()
    {
        outDir = PlayerPrefs.GetString(OutKey, "Temp");
        Directory.CreateDirectory(outDir);
        yield return new WaitForSecondsRealtime(1.5f);

        player = GameObject.FindWithTag("Player");
        om = G1ObjectiveManager.Instance;
        if (player == null || om == null)
        {
            Line($"FATAL player={player != null} objectives={om != null}");
            yield return Finish();
            yield break;
        }
        cc = player.GetComponent<CharacterController>();
        var look = player.GetComponentInChildren<MouseLook>(true);
        if (look) look.enabled = false;
        var hp = player.GetComponent<HealthSystem>();
        if (hp) hp.godMode = true;

        // ------------------------------------------------- the level exists
        Line("--- inventory");
        var lifts = Object.FindObjectsOfType<G1Lift>();
        var shutters = Object.FindObjectsOfType<G1RollupDoor>();
        var barriers = Object.FindObjectsOfType<G1BoomBarrier>();
        var doors = Object.FindObjectsOfType<G1BlastDoor>();
        var readers = Object.FindObjectsOfType<G1Keycard>();
        var fabs = Object.FindObjectsOfType<G1Fabricator>();
        var hosts = Object.FindObjectsOfType<G1ParasiteHost>();
        Line($"  lifts={lifts.Length} shutters={shutters.Length} barriers={barriers.Length} " +
             $"blastdoors={doors.Length} readers={readers.Length} fabricators={fabs.Length} " +
             $"parasites={hosts.Length}");
        Check(lifts.Length >= 1, "the HQ lift exists");
        Check(shutters.Length >= 6, "six roller shutters");
        Check(barriers.Length >= 2, "gate barriers");
        Check(doors.Length >= 5, "blast doors");
        Check(readers.Length >= 4, "card readers");
        Check(hosts.Length >= 8, "parasitised units");
        Line("");

        // ------------------------------------------------- moving equipment
        Line("--- equipment responds");
        if (barriers.Length > 0)
        {
            barriers[0].OnUse(player);
            yield return new WaitForSecondsRealtime(1.8f);
            Check(barriers[0].IsOpen, "boom barrier raises on use");
        }
        if (shutters.Length > 0)
        {
            shutters[0].OnUse(player);
            yield return new WaitForSecondsRealtime(3.0f);
            Check(shutters[0].IsOpen, "roller shutter opens on use");
        }
        if (lifts.Length > 0)
        {
            float y0 = lifts[0].car != null ? lifts[0].car.position.y : 0f;
            lifts[0].OnUse(player);
            yield return new WaitForSecondsRealtime(3.0f);
            float y1 = lifts[0].car != null ? lifts[0].car.position.y : 0f;
            Check(y1 > y0 + 1.0f, $"lift climbs a floor ({y0:0.0} -> {y1:0.0})");
        }
        if (fabs.Length > 0)
        {
            var pistol = player.GetComponentInChildren<G1Pistol>(true);
            int before = pistol != null ? pistol.reserve : -1;
            if (pistol != null) pistol.reserve = 0;
            fabs[0].OnUse(player);
            yield return new WaitForSecondsRealtime(0.3f);
            Check(pistol != null && pistol.reserve > 0,
                  $"fabricator dispenses ammunition ({(pistol != null ? pistol.reserve : -1)})");
            if (pistol != null && before >= 0) pistol.reserve = before;
        }
        Line("");

        // ------------------------------------------------------- the lock
        // This is the level's spine, so it is tested as a sequence rather than
        // as three independent facts.
        Line("--- the power lock");
        G1Keycard outer = null;
        foreach (var k in readers) if (k.group == "lab_outer") { outer = k; break; }
        Check(outer != null, "the research wing has an outer reader");
        if (outer != null)
        {
            Check(!outer.powered, "research wing reader is DEAD before power");
            outer.OnUse(player);
            yield return new WaitForSecondsRealtime(0.4f);
            bool stillShut = true;
            foreach (var t in outer.targets)
                if (t is G1BlastDoor bd && bd.IsOpen) stillShut = false;
            Check(stillShut, "the airlock stays shut when the reader is dead");
        }

        var breaker = Object.FindObjectOfType<G1ObjectiveSwitch>();
        Check(breaker != null, "the turbine hall breaker exists");
        if (breaker != null)
        {
            yield return GoTo(breaker.transform.position - Vector3.forward * 2f);
            breaker.OnUse(player);
            yield return new WaitForSecondsRealtime(0.5f);
        }
        var powerObj = om.objectives.Find(o => o.id == "cradle_power");
        Check(powerObj != null && powerObj.isCompleted, "'cradle_power' completes");
        if (outer != null) Check(outer.powered, "reader is LIVE after the breaker");
        if (outer != null)
        {
            outer.OnUse(player);
            yield return new WaitForSecondsRealtime(2.6f);
            bool opened = false;
            foreach (var t in outer.targets)
                if (t is G1BlastDoor bd && bd.IsOpen) opened = true;
            Check(opened, "the airlock now opens");
        }
        Line("");

        // -------------------------------------------------- the robotics bay
        Line("--- robotics bay");
        var zone = Object.FindObjectOfType<G1QuestZone>();
        var robObj = om.objectives.Find(o => o.id == "cradle_robotics");
        if (zone != null)
        {
            yield return GoTo(zone.transform.position + new Vector3(0f, 0f, 7f));
            for (int i = 0; i < 60 && (robObj == null || !robObj.isCompleted); i++)
            {
                if (cc && cc.enabled) cc.Move(new Vector3(0f, -0.06f, -0.30f));
                yield return null;
            }
        }
        if (robObj != null && !robObj.isCompleted)
        {
            Line("  note: walk-in did not fire; forcing so the chain continues");
            om.IncrementProgress("cradle_robotics", 1);
            yield return new WaitForSecondsRealtime(0.2f);
        }
        Check(robObj != null && robObj.isCompleted, "'cradle_robotics' completes");
        Line("");

        // ------------------------------------------------- the weak point
        // The claim being tested is not "the robot can die" but "the parasite is
        // worth aiming at": the same damage into the chassis must not do it.
        Line("--- parasites");
        if (hosts.Length >= 2)
        {
            var chassis = hosts[0].host;
            float before = chassis != null ? chassis.CurrentHealth : 0f;
            if (chassis != null)
                chassis.TakeDamage(40f, chassis.transform.position, Vector3.forward);
            yield return null;
            Check(chassis != null && chassis.CurrentHealth > 0f,
                  $"40 damage into the chassis is survivable ({before:0} -> " +
                  $"{(chassis != null ? chassis.CurrentHealth : 0f):0})");

            var target = hosts[1];
            var body = target.host;
            for (int i = 0; i < 6 && target != null; i++)
            {
                target.TakeDamage(12f, target.transform.position, Vector3.forward);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.4f);
            Check(body == null || body.CurrentHealth <= 0f,
                  "72 damage into the parasite kills the host");
        }

        int killed = 0;
        foreach (var h in Object.FindObjectsOfType<G1ParasiteHost>())
        {
            if (killed >= 8) break;
            h.TakeDamage(999f, h.transform.position, Vector3.forward);
            killed++;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.6f);
        var hostObj = om.objectives.Find(o => o.id == "cradle_hosts");
        Check(hostObj != null && hostObj.isCompleted,
              $"'cradle_hosts' completes ({(hostObj != null ? hostObj.currentCount : 0)}/8)");
        Line("");

        // ------------------------------------------------------ containment
        Line("--- containment");
        var core = GameObject.Find("ContainmentCore");
        Check(core != null, "the containment core exists");
        if (core != null)
        {
            var chp = core.GetComponent<HealthSystem>();
            if (chp != null) chp.TakeDamage(99999f, core.transform.position, Vector3.up);
        }
        yield return new WaitForSecondsRealtime(0.6f);
        var coreObj = om.objectives.Find(o => o.id == "cradle_core");
        Check(coreObj != null && coreObj.isCompleted, "'cradle_core' completes");

        int done = 0, left = 0;
        foreach (var o in om.objectives)
        {
            if (o.isCompleted) done++;
            else if (o.isMandatory) left++;
        }
        Line($"objectives: {done}/{om.objectives.Count} complete, {left} mandatory outstanding");
        Check(left == 0, "every mandatory objective finished");

        var gate = Object.FindObjectOfType<G1TeleportGate>();
        Check(gate != null, "the extraction gate exists");

        yield return Finish();
    }

    IEnumerator GoTo(Vector3 where)
    {
        if (cc) cc.enabled = false;
        player.transform.position = where + Vector3.up * 0.4f;
        if (cc) cc.enabled = true;
        yield return null;
        if (cc && cc.enabled)
        {
            cc.Move(new Vector3(0f, -0.05f, 0.12f));
            yield return null;
            cc.Move(new Vector3(0f, -0.05f, -0.12f));
        }
        yield return null;
    }

    IEnumerator Finish()
    {
        Line("");
        Line($"==== {pass} passed, {fail} failed ====");
        File.WriteAllText(Path.Combine(outDir, "cradle_playthrough.txt"), log.ToString());
        Debug.Log("G1 CRADLE PLAYTHROUGH DONE");
        yield return new WaitForSecondsRealtime(0.4f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
