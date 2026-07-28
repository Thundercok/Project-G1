using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Automated 62.9-second in-engine cinematic trailer generator & MP4 exporter.
/// Synchronized strictly to the 84 BPM musical bed (22 bars of 4/4 time).
/// 9 distinct shots covering Hook, Sprawl Wide, Breach Ruins, South Gate,
/// Transport Convoy, Armour Park, Cradle Station, Auditor Roof, and Title Impact.
public sealed class G1TrailerCinematicSequence : MonoBehaviour
{
    public static bool IsRunning { get; private set; }

    public bool recordToMP4 = true;
    int frameCount = 0;
    string framesDir;

    const float BPM = 84.0f;
    const float BEAT = 60.0f / BPM; // ~0.7143s
    const float BAR = BEAT * 4.0f;   // ~2.8571s

    struct Shot
    {
        public string title;
        public string subtitle;
        public Vector3 startPos, endPos;
        public Vector3 startRot, endRot;
        public float bars;
        public float timeScale;
        public string soundEffect;
    }

    Camera cam;
    Texture2D blackTex;
    Texture2D pixelTex;
    Font font;

    GUIStyle chapterStyle, subStyle, actionStyle;
    float currentShotTime;
    float shotDuration;
    string curChapter = "", curSub = "";
    float textFade = 0f;
    float letterboxHeight = 0f;
    float targetLetterbox = 0f;

    public static void LaunchTrailerDemo(bool recordMP4 = false)
    {
        if (IsRunning) return;
        var go = new GameObject("G1TrailerCinematicSequence");
        var seq = go.AddComponent<G1TrailerCinematicSequence>();
        seq.recordToMP4 = recordMP4;
    }

    void Start()
    {
        IsRunning = true;
        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();
        pixelTex = Texture2D.whiteTexture;
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");

        if (recordToMP4)
        {
            Time.captureFramerate = 30;
            framesDir = Path.Combine(Application.dataPath, "../TrailerFrames");
            if (!Directory.Exists(framesDir)) Directory.CreateDirectory(framesDir);
        }

        var main = Camera.main;
        if (main != null)
        {
            cam = main;
        }
        else
        {
            var camGo = new GameObject("TrailerCamera");
            cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
        }

        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var move = player.GetComponent<PlayerMovement>();
            var look = player.GetComponentInChildren<MouseLook>();
            if (move) move.enabled = false;
            if (look) look.enabled = false;
        }

        targetLetterbox = Screen.height * 0.12f;
        StartCoroutine(RoutinePlayTrailerSequence());
    }

    IEnumerator RoutinePlayTrailerSequence()
    {
        // Plays trailer audio soundtrack bed
        G1Audio.Play2D("ambient_alien", 0.5f, 1.0f);

        // ── 9 SHOTS SYNCHRONIZED TO 84 BPM BEAT (22 BARS = 62.85s) ───────────

        // 1. THE HOOK (Bars 0 -> 2: 5.71s)
        yield return PlayShot(new Shot
        {
            title = "THE CORVEX EXPERIMENT",
            subtitle = "CORVUS RESEARCH ANNEX — SUB-LEVEL C",
            startPos = new Vector3(1.35f, 1.85f, -2.0f),
            endPos = new Vector3(0.62f, 1.78f, -1.05f),
            startRot = new Vector3(4f, 0f, 0f),
            endRot = new Vector3(2f, 10f, 0f),
            bars = 2.0f,
            timeScale = 1.0f,
            soundEffect = "radio_bark_a"
        });

        // 2. THE SPRAWL WIDE (Bars 2 -> 4.5: 7.14s)
        yield return PlayShot(new Shot
        {
            title = "CHAPTER 01 // THE SPRAWL",
            subtitle = "VALLEY CONTAINER BOUNDARY — 800M EXPONSE",
            startPos = new Vector3(-470f, 190f, 470f),
            endPos = new Vector3(-250f, 132f, 300f),
            startRot = new Vector3(18f, 135f, 0f),
            endRot = new Vector3(12f, 140f, 0f),
            bars = 2.5f,
            timeScale = 1.0f,
            soundEffect = "door_servo"
        });

        // 3. THE BREACH (Bars 4.5 -> 7: 7.14s)
        yield return PlayShot(new Shot
        {
            title = "CHAPTER 02 // BREACH RUINS",
            subtitle = "SECTOR 4 TOXIC CONTAINMENT ZONE",
            startPos = new Vector3(66f, 96f, -250f),
            endPos = new Vector3(24f, 30f, -196f),
            startRot = new Vector3(14f, 210f, 0f),
            endRot = new Vector3(8f, 215f, 0f),
            bars = 2.5f,
            timeScale = 0.85f,
            soundEffect = "ambient_industrial"
        });

        // 4. SOUTH GATE ROAD (Bars 7 -> 9: 5.71s)
        yield return PlayShot(new Shot
        {
            title = "HECU DEFENSE PERIMETER",
            subtitle = "SOUTH GATE ACCESS ROAD",
            startPos = new Vector3(-30f, 2.6f, 372f),
            endPos = new Vector3(-6f, 2.2f, 360f),
            startRot = new Vector3(2f, 160f, 0f),
            endRot = new Vector3(2f, 165f, 0f),
            bars = 2.0f,
            timeScale = 1.0f,
            soundEffect = "radio_bark_b"
        });

        // 5. MILITARY CONVOY (Bars 9 -> 11.5: 7.14s)
        yield return PlayShot(new Shot
        {
            title = "HEAVY LOGISTICS CONVOY",
            subtitle = "MILITARY ARMORED FLEET",
            startPos = new Vector3(44f, 5.4f, 262f),
            endPos = new Vector3(22f, 3.2f, 240f),
            startRot = new Vector3(6f, -170f, 0f),
            endRot = new Vector3(4f, -175f, 0f),
            bars = 2.5f,
            timeScale = 1.0f,
            soundEffect = "fire_smg"
        });

        // 6. ARMOUR PARK (Bars 11.5 -> 14: 7.14s)
        yield return PlayShot(new Shot
        {
            title = "FORTIFIED TANK PARK",
            subtitle = "TACTICAL SURRENDER DIVISION",
            startPos = new Vector3(-262f, 7.6f, -6f),
            endPos = new Vector3(-286f, 4.4f, -34f),
            startRot = new Vector3(8f, -90f, 0f),
            endRot = new Vector3(5f, -95f, 0f),
            bars = 2.5f,
            timeScale = 1.0f,
            soundEffect = "explosion"
        });

        // 7. CRADLE STATION REVEAL (Bars 14 -> 17: 8.57s)
        yield return PlayShot(new Shot
        {
            title = "CRADLE STATION REVEAL",
            subtitle = "EAST RIDGE ACCESS FACILITY",
            startPos = new Vector3(430f, 110f, 40f),
            endPos = new Vector3(700f, 74f, 16f),
            startRot = new Vector3(12f, 80f, 0f),
            endRot = new Vector3(8f, 85f, 0f),
            bars = 3.0f,
            timeScale = 0.9f,
            soundEffect = "ambient_alien"
        });

        // 8. THE AUDITOR TOWER (Bars 17 -> 19: 5.71s)
        yield return PlayShot(new Shot
        {
            title = "THE AUDITOR'S OBSERVATION",
            subtitle = "TOWER ROOF — ITERATION AUDIT",
            startPos = new Vector3(22f, 41.4f, -26f),
            endPos = new Vector3(13.5f, 40.4f, -15.5f),
            startRot = new Vector3(10f, -40f, 0f),
            endRot = new Vector3(6f, -45f, 0f),
            bars = 2.0f,
            timeScale = 0.8f,
            soundEffect = "door_servo"
        });

        // 9. TITLE IMPACT (Bars 19 -> 22: 8.57s)
        curChapter = "THE CORVEX";
        curSub = "SOMETHING GOT OUT. THE ARMY SEALED THE VALLEY.";
        textFade = 1f;
        Time.timeScale = 1.0f;
        G1Audio.Play2D("pickup", 0.9f, 0.6f);

        float titleDuration = BAR * 3.0f;
        float elapsed = 0f;
        while (elapsed < titleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Finish();
    }

    IEnumerator PlayShot(Shot s)
    {
        curChapter = s.title;
        curSub = s.subtitle;
        shotDuration = BAR * s.bars;
        currentShotTime = 0f;
        textFade = 1f;
        Time.timeScale = s.timeScale;

        if (!string.IsNullOrEmpty(s.soundEffect))
            G1Audio.Play2D(s.soundEffect, 0.7f);

        while (currentShotTime < shotDuration)
        {
            currentShotTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(currentShotTime / shotDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (cam != null)
            {
                cam.transform.position = Vector3.Lerp(s.startPos, s.endPos, smoothT);
                cam.transform.rotation = Quaternion.Euler(Vector3.Lerp(s.startRot, s.endRot, smoothT));
            }

            if (currentShotTime > shotDuration - 1.2f)
                textFade = (shotDuration - currentShotTime) / 1.2f;

            yield return null;
        }
    }

    void LateUpdate()
    {
        if (!IsRunning) return;

        if (recordToMP4 && !string.IsNullOrEmpty(framesDir))
        {
            frameCount++;
            string file = Path.Combine(framesDir, $"frame_{frameCount:D4}.png");
            ScreenCapture.CaptureScreenshot(file);
        }
    }

    void Update()
    {
        if (!IsRunning) return;

        letterboxHeight = Mathf.Lerp(letterboxHeight, targetLetterbox, Time.unscaledDeltaTime * 4f);

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
            Finish();
    }

    void Finish()
    {
        Time.captureFramerate = 0;
        Time.timeScale = 1.0f;
        IsRunning = false;

        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var move = player.GetComponent<PlayerMovement>();
            var look = player.GetComponentInChildren<MouseLook>();
            if (move) move.enabled = true;
            if (look) look.enabled = true;
        }

        if (recordToMP4 && frameCount > 0)
        {
            CompileMP4WithFFmpeg();
        }

        Destroy(gameObject);
    }

    void CompileMP4WithFFmpeg()
    {
        try
        {
            string outPath = Path.Combine(Application.dataPath, "../CorvusSprawl_Trailer_Output.mp4");
            string ffmpegPath = "/opt/homebrew/bin/ffmpeg";
            if (!File.Exists(ffmpegPath)) ffmpegPath = "ffmpeg";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-y -framerate 30 -i \"{framesDir}/frame_%04d.png\" -c:v libx264 -pix_fmt yuv420p \"{outPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit();

            string copyPath = Path.Combine(Application.dataPath, "../CorvusSprawl_Trailer.mp4");
            if (File.Exists(outPath)) File.Copy(outPath, copyPath, true);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("FFmpeg MP4 compilation failed: " + e.Message);
        }
    }

    void OnDestroy()
    {
        Time.captureFramerate = 0;
        Time.timeScale = 1.0f;
        IsRunning = false;
    }

    void OnGUI()
    {
        if (!IsRunning) return;
        InitStyles();

        if (letterboxHeight > 1f)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, letterboxHeight), blackTex);
            GUI.DrawTexture(new Rect(0, Screen.height - letterboxHeight, Screen.width, letterboxHeight), blackTex);
        }

        if (textFade > 0.01f && !string.IsNullOrEmpty(curChapter))
        {
            float cy = Screen.height * 0.78f;
            float panelW = Screen.width * 0.7f;
            float panelX = (Screen.width - panelW) * 0.5f;

            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.8f * textFade);
            GUI.DrawTexture(new Rect(panelX, cy, panelW, 55f), pixelTex);

            GUI.color = new Color(0.16f, 0.75f, 0.75f, 0.9f * textFade);
            GUI.DrawTexture(new Rect(panelX, cy, 4f, 55f), pixelTex);

            GUI.color = new Color(1f, 0.75f, 0.12f, textFade);
            GUI.Label(new Rect(panelX + 16f, cy + 6f, panelW - 32f, 26f), curChapter, chapterStyle);

            GUI.color = new Color(0.85f, 0.88f, 0.90f, textFade * 0.85f);
            GUI.Label(new Rect(panelX + 16f, cy + 30f, panelW - 32f, 20f), curSub, subStyle);
        }

        string statusText = recordToMP4 ? $"REC ● MP4 ({frameCount} FRAMES) | PRESS [SPACE / ESC] TO FINISH" : "PRESS [SPACE / ESC] TO STOP CINEMATIC";
        GUI.color = recordToMP4 ? new Color(1f, 0.35f, 0.35f, 0.9f) : new Color(0.16f, 0.75f, 0.75f, 0.7f);
        GUI.Label(new Rect(0, Screen.height - 35, Screen.width - 25, 25), statusText, actionStyle);

        GUI.color = Color.white;
    }

    void InitStyles()
    {
        if (chapterStyle != null) return;

        chapterStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft, fontSize = 18, fontStyle = FontStyle.Bold
        };
        subStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft, fontSize = 13, fontStyle = FontStyle.Italic
        };
        actionStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight, fontSize = 12, fontStyle = FontStyle.Bold
        };

        if (font != null)
        {
            chapterStyle.font = font;
            subStyle.font = font;
            actionStyle.font = font;
        }
    }
}
