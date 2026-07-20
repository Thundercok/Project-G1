using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Builds the main-menu scene procedurally: dim corridor backdrop, flickering
/// emergency light, camera, and the menu components. Menu: G1 > Build Main Menu.
public static class G1MenuBuilder
{
    public const string MenuScenePath = "Assets/Scenes/MenuScene.unity";

    [MenuItem("G1/Build Main Menu")]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("G1: exit Play Mode before building scenes.");
            return;
        }
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.10f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 4f;
        RenderSettings.fogEndDistance = 18f;
        RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.05f);

        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.2f, 0.21f, 0.23f);
        void Slab(string name, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
        Slab("Floor", new Vector3(0, -0.25f, 6), new Vector3(4, 0.5f, 24));
        Slab("WallL", new Vector3(-2, 1.5f, 6), new Vector3(0.4f, 3.5f, 24));
        Slab("WallR", new Vector3(2, 1.5f, 6), new Vector3(0.4f, 3.5f, 24));
        Slab("Ceiling", new Vector3(0, 3.4f, 6), new Vector3(4, 0.4f, 24));

        var lightGo = new GameObject("EmergencyLight");
        var li = lightGo.AddComponent<Light>();
        li.type = LightType.Point;
        li.color = new Color(0.91f, 0.45f, 0.16f);   // warning orange
        li.range = 12f;
        li.intensity = 1.4f;
        lightGo.transform.position = new Vector3(0, 2.8f, 6f);

        var camGo = new GameObject("MenuCamera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0, 1.5f, -1.5f);
        camGo.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
        camGo.AddComponent<AudioListener>();

        var menuGo = new GameObject("MainMenu");
        var menu = menuGo.AddComponent<G1MainMenu>();
        menuGo.AddComponent<G1SettingsPanel>();
        menu.flickerLight = li;

        EnsureScenesFolder();
        EditorSceneManager.SaveScene(scene, MenuScenePath);
        RegisterScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("G1 MENU BUILD OK");
    }

    static void EnsureScenesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
    }

    /// Menu = build index 0 (when it exists), then every campaign scene
    /// that has been built so far.
    public static void RegisterScenes()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (string path in new[]
        {
            MenuScenePath,
            "Assets/Scenes/TestScene.unity",
            "Assets/Scenes/Level2.unity",
            "Assets/Scenes/Level3.unity",
        })
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                list.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = list.ToArray();
    }
}
