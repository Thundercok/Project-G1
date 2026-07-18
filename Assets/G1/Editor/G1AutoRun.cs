using System.IO;
using UnityEditor;

/// Remote trigger: if Temp/g1_autoplay exists when scripts reload, rebuild the
/// test scene and enter Play mode. Lets tooling (or Claude) drive the open
/// editor without UI scripting. Temp/ is per-session and never committed.
[InitializeOnLoad]
public static class G1AutoRun
{
    const string Flag = "Temp/g1_autoplay";

    static G1AutoRun()
    {
        if (!File.Exists(Flag))
            return;
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += Run;    // retry once play mode ends
            return;
        }
        if (!File.Exists(Flag))
            return;
        File.Delete(Flag);
        G1SceneBuilder.BuildScene();
        EditorApplication.EnterPlaymode();
    }
}
