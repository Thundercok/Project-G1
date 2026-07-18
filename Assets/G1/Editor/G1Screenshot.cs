using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Headless scene verification: renders the test scene to PNGs.
public static class G1Screenshot
{
    public static void Snap()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity");
        var go = new GameObject("ShotCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 65f;

        go.transform.position = new Vector3(7f, 3.5f, -8f);
        go.transform.LookAt(new Vector3(-1f, 0.8f, 4f));
        Save(cam, "/Users/minhdang_work/halflife-like-game/renders/unity_overview.png");

        go.transform.position = new Vector3(0f, 1.62f, -8f);
        go.transform.LookAt(new Vector3(-1.5f, 1.1f, 2f));
        Save(cam, "/Users/minhdang_work/halflife-like-game/renders/unity_pov.png");

        Debug.Log("G1 SNAP OK");
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
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
    }
}
