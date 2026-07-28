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
        if (File.Exists("Temp/g1_hugemap"))
        {
            File.Delete("Temp/g1_hugemap");
            if (File.Exists(Flag)) File.Delete(Flag);
            // The huge map populates itself from prefabs the arena builder
            // saves (HECUSoldier, Zombie, Alien), so building it alone leaves
            // the world full of whatever those prefabs last contained — which
            // is how a rebuilt soldier model can be exported, imported and
            // still not appear in the game.
            G1SceneBuilder.BuildScene();
            G1CradleBuilder.BuildCradle();
            // and the shared scene both of them now feed: one world, one
            // NavMesh, no loading screen between the base and the facility
            G1WorldBuilder.BuildWorld();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/World.unity");
            EditorApplication.EnterPlaymode();
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
