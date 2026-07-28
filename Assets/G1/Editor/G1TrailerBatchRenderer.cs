using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Batchmode Unity 3D In-Game Trailer Renderer.
/// Renders real high-res 1920x1080 3D frames directly from the Unity project scenes
/// (TestScene, Level2, Level3, HugeMap) with full materials, lighting, shaders,
/// and post-processing, then compiles CorvusSprawl_Trailer.mp4 using FFmpeg.
public static class G1TrailerBatchRenderer
{
    [MenuItem("G1/★ RENDER REAL 3D TRAILER MP4 ★", false, -80)]
    public static void Render3DTrailerMP4()
    {
        string projectDir = Path.Combine(Application.dataPath, "..");
        string framesDir = Path.Combine(projectDir, "TrailerFrames3D");
        if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
        Directory.CreateDirectory(framesDir);

        int frameIndex = 0;
        int width = 1920;
        int height = 1080;

        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);

        string[] scenes = new string[]
        {
            "Assets/Scenes/TestScene.unity",
            "Assets/Scenes/Level2.unity",
            "Assets/Scenes/Level3.unity",
            "Assets/Scenes/HugeMap.unity"
        };

        Vector3[][] cameraPositions = new Vector3[][]
        {
            // TestScene (Level 1 Sub-Surface)
            new Vector3[] { new Vector3(0f, 1.8f, -10f), new Vector3(0f, 1.8f, 5f), new Vector3(8f, 2.2f, 18f) },
            // Level2 (Quarantine)
            new Vector3[] { new Vector3(-6f, 1.2f, 10f), new Vector3(4f, 1.8f, 20f), new Vector3(0f, 2.5f, 30f) },
            // Level3 (Threshold Undercroft)
            new Vector3[] { new Vector3(0f, 3.5f, 35f), new Vector3(-5f, 1.6f, 48f), new Vector3(5f, 2.0f, 58f) },
            // HugeMap (Battlefield)
            new Vector3[] { new Vector3(-20f, 10f, -5f), new Vector3(0f, 15f, 20f), new Vector3(25f, 8f, 40f) }
        };

        Vector3[][] cameraRotations = new Vector3[][]
        {
            new Vector3[] { new Vector3(4f, 0f, 0f), new Vector3(2f, 15f, 0f), new Vector3(6f, -25f, 0f) },
            new Vector3[] { new Vector3(-2f, 35f, 0f), new Vector3(5f, -15f, 0f), new Vector3(8f, 0f, 0f) },
            new Vector3[] { new Vector3(16f, 0f, 0f), new Vector3(4f, 25f, 0f), new Vector3(2f, -20f, 0f) },
            new Vector3[] { new Vector3(18f, 40f, 0f), new Vector3(25f, 0f, 0f), new Vector3(12f, -35f, 0f) }
        };

        for (int s = 0; s < scenes.Length; s++)
        {
            string scenePath = scenes[s];
            if (!File.Exists(scenePath))
            {
                UnityEngine.Debug.LogWarning($"[G1TrailerBatchRenderer] Scene not found: {scenePath}");
                continue;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("TempTrailerCam");
                cam = camGo.AddComponent<Camera>();
            }

            cam.targetTexture = rt;

            var posWaypoints = cameraPositions[s];
            var rotWaypoints = cameraRotations[s];

            int framesPerScene = 180; // 6 seconds per scene @ 30 FPS
            for (int f = 0; f < framesPerScene; f++)
            {
                float t = (float)f / (framesPerScene - 1);
                int segment = Mathf.Min((int)(t * (posWaypoints.Length - 1)), posWaypoints.Length - 2);
                float segT = (t * (posWaypoints.Length - 1)) - segment;
                float smoothT = Mathf.SmoothStep(0f, 1f, segT);

                cam.transform.position = Vector3.Lerp(posWaypoints[segment], posWaypoints[segment + 1], smoothT);
                cam.transform.rotation = Quaternion.Euler(Vector3.Lerp(rotWaypoints[segment], rotWaypoints[segment + 1], smoothT));

                cam.Render();

                RenderTexture.active = rt;
                screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenShot.Apply();

                byte[] bytes = screenShot.EncodeToPNG();
                string framePath = Path.Combine(framesDir, $"frame_{frameIndex:D4}.png");
                File.WriteAllBytes(framePath, bytes);

                frameIndex++;
            }

            cam.targetTexture = null;
        }

        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(screenShot);

        UnityEngine.Debug.Log($"[G1TrailerBatchRenderer] Rendered {frameIndex} 3D in-game frames to {framesDir}. Compiling MP4...");

        // Compile FFmpeg MP4
        string outPath = Path.Combine(projectDir, "CorvusSprawl_Trailer.mp4");
        string streamingPath = Path.Combine(projectDir, "Assets/StreamingAssets/CorvusSprawl_Trailer.mp4");
        string ffmpegPath = "/opt/homebrew/bin/ffmpeg";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-y -framerate 30 -i \"{framesDir}/frame_%04d.png\" -c:v libx264 -pix_fmt yuv420p \"{outPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var p = Process.Start(psi);
        p?.WaitForExit();

        if (File.Exists(outPath))
        {
            File.Copy(outPath, streamingPath, true);
            UnityEngine.Debug.Log($"[G1TrailerBatchRenderer] ★ SUCCESS! 3D In-Game Trailer MP4 compiled to {outPath} ★");
        }
    }
}
