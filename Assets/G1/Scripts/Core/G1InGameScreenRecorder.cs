using System.IO;
using System.Collections;
using System.Diagnostics;
using UnityEngine;

/// In-game 3D Screen Recorder.
/// Press F9 at any time while playing in Unity Editor or Standalone build to start/stop
/// capturing real 3D gameplay frames and compile CorvusSprawl_Trailer.mp4 via FFmpeg.
public sealed class G1InGameScreenRecorder : MonoBehaviour
{
    public static bool IsRecording { get; private set; }

    [Header("Shortcut")]
    public KeyCode toggleKey = KeyCode.F9;

    int frameCount = 0;
    string framesDir;
    Texture2D pixelTex;
    Font font;
    GUIStyle statusStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null && player.GetComponent<G1InGameScreenRecorder>() == null)
            player.AddComponent<G1InGameScreenRecorder>();
    }

    void Start()
    {
        pixelTex = Texture2D.whiteTexture;
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (IsRecording) StopRecording();
            else StartRecording();
        }
    }

    void StartRecording()
    {
        IsRecording = true;
        frameCount = 0;
        Time.captureFramerate = 30;

        string projectDir = Path.Combine(Application.dataPath, "..");
        framesDir = Path.Combine(projectDir, "TrailerFramesInGame");

        if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
        Directory.CreateDirectory(framesDir);

        UnityEngine.Debug.Log($"[G1Recorder] ★ STARTED IN-GAME 3D RECORDING TO {framesDir} (Press F9 to Stop) ★");
    }

    void LateUpdate()
    {
        if (!IsRecording || string.IsNullOrEmpty(framesDir)) return;

        frameCount++;
        string file = Path.Combine(framesDir, $"frame_{frameCount:D4}.png");
        ScreenCapture.CaptureScreenshot(file);
    }

    void StopRecording()
    {
        if (!IsRecording) return;
        IsRecording = false;
        Time.captureFramerate = 0;

        UnityEngine.Debug.Log($"[G1Recorder] Captured {frameCount} 3D frames. Compiling MP4...");

        string projectDir = Path.Combine(Application.dataPath, "..");
        string outPath = Path.Combine(projectDir, "CorvusSprawl_Trailer.mp4");
        string streamingPath = Path.Combine(projectDir, "Assets/StreamingAssets/CorvusSprawl_Trailer.mp4");
        string ffmpegPath = "/opt/homebrew/bin/ffmpeg";

        try
        {
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
                UnityEngine.Debug.Log($"[G1Recorder] ★ SUCCESS! Real 3D In-Game Trailer MP4 saved to {outPath} ★");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("[G1Recorder] FFmpeg error: " + e.Message);
        }
    }

    void OnGUI()
    {
        if (!IsRecording) return;

        if (statusStyle == null)
        {
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            if (font) statusStyle.font = font;
        }

        GUI.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        GUI.Label(new Rect(0, 16, Screen.width - 24, 26), $"● REC 3D IN-GAME ({frameCount} FRAMES) | PRESS F9 TO FINISH", statusStyle);
        GUI.color = Color.white;
    }
}
