using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// Fullscreen video player for CorvusSprawl_Trailer.mp4.
/// Playable from Main Menu or Intro Cutscene. Supports skipping via Space/Esc.
public sealed class G1TrailerPlayer : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    VideoPlayer player;
    RenderTexture renderTex;
    Texture2D blackTex;
    Font font;
    GUIStyle skipStyle;
    Action onCompleteCallback;
    bool finished;
    float promptPulse;

    public static void Play(Action onComplete = null)
    {
        if (IsPlaying) return;

        var go = new GameObject("G1TrailerPlayer");
        var tp = go.AddComponent<G1TrailerPlayer>();
        tp.onCompleteCallback = onComplete;
        tp.StartPlayback();
    }

    void StartPlayback()
    {
        IsPlaying = true;

        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();

        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");

        renderTex = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        renderTex.Create();

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = renderTex;
        player.aspectRatio = VideoAspectRatio.FitInside;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;

        var audioSource = gameObject.AddComponent<AudioSource>();
        player.EnableAudioTrack(0, true);
        player.SetTargetAudioSource(0, audioSource);

        string file = "CorvusSprawl_Trailer.mp4";
        string path = Path.Combine(Application.streamingAssetsPath, file);
        if (!File.Exists(path)) path = Path.Combine(Application.dataPath, "StreamingAssets/" + file);
        if (!File.Exists(path)) path = Path.Combine(Application.dataPath, "../" + file);

        if (File.Exists(path))
        {
            player.url = path;
            player.loopPointReached += OnVideoEnd;
            player.Prepare();
            StartCoroutine(PrepareAndPlay());
        }
        else
        {
            Debug.LogWarning("[G1TrailerPlayer] Trailer video not found at " + path);
            Finish();
        }
    }

    IEnumerator PrepareAndPlay()
    {
        while (!player.isPrepared)
            yield return null;

        player.Play();
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        Finish();
    }

    void Update()
    {
        if (!IsPlaying || finished) return;

        promptPulse += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Escape))
        {
            Finish();
        }
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        IsPlaying = false;

        if (player != null)
        {
            player.Stop();
        }

        if (renderTex != null)
        {
            renderTex.Release();
            Destroy(renderTex);
        }

        Action cb = onCompleteCallback;
        onCompleteCallback = null;
        cb?.Invoke();

        Destroy(gameObject);
    }

    void OnGUI()
    {
        if (!IsPlaying || finished) return;

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);

        if (renderTex != null && player != null && player.isPlaying)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), renderTex, ScaleMode.ScaleToFit);
        }

        if (skipStyle == null)
        {
            skipStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
            };
            if (font != null) skipStyle.font = font;
        }

        float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(promptPulse * 2f));
        GUI.color = new Color(0.16f, 0.75f, 0.75f, pulse);
        GUI.Label(new Rect(0, Screen.height - 52, Screen.width - 40, 30),
                  "PRESS  [SPACE / ESC]  TO EXIT TRAILER  ▸", skipStyle);

        GUI.color = Color.white;
    }

    void OnDestroy()
    {
        IsPlaying = false;
    }
}
