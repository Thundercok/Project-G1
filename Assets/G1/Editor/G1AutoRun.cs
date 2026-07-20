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
        // Build any missing campaign scenes first; Level 1 last so Play
        // drops into Chapter One.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MenuScene.unity") == null)
            G1MenuBuilder.BuildMenu();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Level2.unity") == null)
            G1CampaignBuilders.BuildLevel2();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Level3.unity") == null)
            G1CampaignBuilders.BuildLevel3();
        G1SceneBuilder.BuildScene();
        EditorApplication.EnterPlaymode();
    }
}
