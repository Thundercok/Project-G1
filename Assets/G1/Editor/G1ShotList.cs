using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Photograph named things in a built scene.
///
/// G1VerifyBuild frames a hand-written list of coordinates, which was right when
/// the question was "did the districts build". The question now is "does the new
/// soldier / robot / truck actually look like that in the game", and a fixed
/// coordinate cannot answer it — the thing being checked moves whenever the
/// spawn list does. So this finds objects by name and frames whatever it found,
/// which means a shot can never quietly end up pointing at empty ground.
///
/// Drop a file at Temp/g1_shots whose first line is an output directory and
/// whose second is a scene path.
[InitializeOnLoad]
public static class G1ShotList
{
    const string Flag = "Temp/g1_shots";

    static G1ShotList()
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

        var lines = File.ReadAllLines(Flag);
        File.Delete(Flag);
        string outDir = lines.Length > 0 ? lines[0].Trim() : "Temp";
        string scenePath = lines.Length > 1 ? lines[1].Trim() : "Assets/Scenes/HugeMap.unity";
        Directory.CreateDirectory(outDir);

        var log = new StringBuilder();
        try
        {
            EditorSceneManager.OpenScene(scenePath);
            Physics.SyncTransforms();
            Shoot(outDir, log);
        }
        catch (System.Exception e)
        {
            log.AppendLine("EXCEPTION: " + e);
        }
        File.WriteAllText(Path.Combine(outDir, "shots.txt"), log.ToString());
        Debug.Log("G1 SHOTS DONE -> " + outDir);
    }

    /// Everything worth photographing, as (file name, object name, how far back,
    /// how high). Distance is the whole editorial decision: a soldier at 3 m is
    /// a model review and at 25 m is the question the player actually asks.
    static readonly (string shot, string find, float dist, float height)[] Subjects =
    {
        ("soldier_close", "HECUSoldier", 3.0f, 1.6f),
        ("soldier_range", "HECUSoldier", 22.0f, 2.4f),
        ("robot_close", "ParasitisedUnit", 2.6f, 1.7f),
        ("robot_range", "ParasitisedUnit", 18.0f, 2.2f),
        ("truck", "Truck_", 7.5f, 2.6f),
        ("tank", "Tank", 11.0f, 3.4f),
        ("apc", "Apc", 10.0f, 3.2f),
        ("lift", "Lift_", 9.0f, 3.0f),
        ("shutter", "Rollup_", 12.0f, 3.4f),
        ("barrier", "Barrier_", 9.0f, 2.6f),
        ("airlock", "BlastDoor_lab", 10.0f, 2.8f),
        ("fabricator", "Fabricator", 4.5f, 2.0f),
        ("containment", "ContainmentCore", 20.0f, 7.0f),
        ("helicopter", "Heli_", 12.0f, 3.4f),
        ("barracks", "Sleep0", 14.0f, 3.0f),
        ("warehouse", "Warehouse", 26.0f, 6.0f),
        ("hq", "HQ_L0", 34.0f, 8.0f),
    };

    static void Shoot(string outDir, StringBuilder log)
    {
        var cam = new GameObject("ShotCam").AddComponent<Camera>();
        cam.fieldOfView = 62f;
        cam.farClipPlane = 900f;

        // one index of everything in the scene, so a prefix match is cheap
        var all = Object.FindObjectsOfType<GameObject>();

        foreach (var s in Subjects)
        {
            GameObject hit = null;
            foreach (var go in all)
            {
                if (!go.name.StartsWith(s.find, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                // skip the FBX asset roots that carry no transform of their own
                if (go.transform.position == Vector3.zero) continue;
                hit = go;
                break;
            }
            if (hit == null)
            {
                log.AppendLine($"MISS  {s.shot,-14} no object named {s.find}*");
                continue;
            }

            var at = hit.transform.position;
            // stand off along the object's own forward where it has one, so a
            // vehicle is photographed from three-quarter front rather than
            // whichever way world +Z happens to point
            Vector3 dir = hit.transform.forward;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            Vector3 look = at + Vector3.up * Mathf.Min(1.4f, s.height * 0.6f);

            // Try several three-quarter angles and keep the first with a clear
            // line back to the subject. Without this a tank parked beside a
            // hangar gets photographed from inside the hangar wall, and the
            // report says OK because a camera was placed and a file was written.
            Vector3 eye = Vector3.zero;
            bool clear = false;
            for (int a = 0; a < 8 && !clear; a++)
            {
                float ang = a * 45f * Mathf.Deg2Rad;
                Vector3 off = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                // bias toward the object's own front-right for the first try
                if (a == 0) off = (dir * 0.75f + hit.transform.right * 0.85f).normalized;
                eye = at + off * s.dist + Vector3.up * s.height;
                clear = !Physics.Linecast(look, eye, ~0, QueryTriggerInteraction.Ignore);
            }
            if (!clear)
            {
                // nowhere on the ring is clear: go up and look down instead
                eye = at + Vector3.up * (s.dist * 0.9f) - Vector3.forward * (s.dist * 0.5f);
                log.AppendLine($"      {s.shot}: no clear ground angle, shooting from above");
            }
            cam.transform.position = eye;
            cam.transform.LookAt(look);

            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(outDir, s.shot + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);

            log.AppendLine($"OK    {s.shot,-14} {hit.name} at " +
                           $"({at.x:0},{at.y:0},{at.z:0})");
        }
        Object.DestroyImmediate(cam.gameObject);
    }
}
