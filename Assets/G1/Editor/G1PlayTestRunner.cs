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

    static G1PlayTestRunner()
    {
        if (File.Exists(Arm))
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
        if (!File.Exists(Arm)) return;

        string outDir = File.ReadAllText(Arm).Trim();
        File.Delete(Arm);
        if (string.IsNullOrEmpty(outDir)) outDir = "Temp";

        // Hand off through PlayerPrefs, not a file: entering Play mode wipes
        // Temp/, which silently ate the handoff and left the test inert.
        PlayerPrefs.SetString(G1SelfTest.OutKey, outDir);
        PlayerPrefs.SetInt(G1SelfTest.ArmKey, 1);
        PlayerPrefs.Save();

        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "runner.txt"),
                          "runner fired, entering play mode\n");

        EditorSceneManager.OpenScene("Assets/Scenes/HugeMap.unity");
        EditorApplication.EnterPlaymode();
    }
}
