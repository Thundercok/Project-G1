using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Arms the Play-mode self test. Drop the output directory into
/// Temp/g1_playtest_arm and the next script reload opens the huge map, hands
/// the directory to <see cref="G1SelfTest"/> and enters Play mode; the test
/// writes its report and leaves Play mode on its own.
[InitializeOnLoad]
public static class G1PlayTestRunner
{
    const string Arm = "Temp/g1_playtest_arm";
    const string Play = "Temp/g1_playthrough_arm";
    const string Cradle = "Temp/g1_cradle_arm";

    static G1PlayTestRunner()
    {
        if (File.Exists(Arm) || File.Exists(Play) || File.Exists(Cradle))
            EditorApplication.delayCall += Go;
    }

    static void Go()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += Go;
            return;
        }
        // Cradle Station is its own scene with its own test, so the runner
        // has to know which level it is arming as well as which test.
        bool cradle = File.Exists(Cradle);
        bool full = File.Exists(Play);
        string flag = cradle ? Cradle : full ? Play : Arm;
        if (!File.Exists(flag)) return;

        string outDir = File.ReadAllText(flag).Trim();
        File.Delete(flag);
        if (string.IsNullOrEmpty(outDir)) outDir = "Temp";

        // Hand off through PlayerPrefs, not a file: entering Play mode wipes
        // Temp/, which silently ate the handoff and left the test inert.
        if (cradle)
        {
            PlayerPrefs.SetString(G1CradlePlaythrough.OutKey, outDir);
            PlayerPrefs.SetInt(G1CradlePlaythrough.ArmKey, 1);
        }
        else if (full)
        {
            PlayerPrefs.SetString(G1Playthrough.OutKey, outDir);
            PlayerPrefs.SetInt(G1Playthrough.ArmKey, 1);
        }
        else
        {
            PlayerPrefs.SetString(G1SelfTest.OutKey, outDir);
            PlayerPrefs.SetInt(G1SelfTest.ArmKey, 1);
        }
        PlayerPrefs.Save();

        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "runner.txt"),
                          "runner fired, entering play mode\n");

        EditorSceneManager.OpenScene(cradle ? "Assets/Scenes/CradleStation.unity"
                                          : "Assets/Scenes/HugeMap.unity");
        EditorApplication.EnterPlaymode();
    }
}
