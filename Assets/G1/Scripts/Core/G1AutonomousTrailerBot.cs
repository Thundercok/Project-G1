using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Crash-proof Autonomous Gameplay Bot & 3D Trailer Recorder.
/// Automatically plays the game (moves player via CharacterController, aims at enemies,
/// cycles weapons, shoots) and records high-res 3D gameplay footage into CorvusSprawl_Trailer.mp4.
public sealed class G1AutonomousTrailerBot : MonoBehaviour
{
    public static bool IsRunning { get; private set; }

    [Header("Bot Control")]
    public KeyCode toggleKey = KeyCode.F10;
    public float targetScanRange = 40f;

    CharacterController controller;
    MouseLook mouseLook;
    WeaponSwitcher switcher;

    int frameCount = 0;
    string framesDir;
    float botStartTime;
    float lastShotTime;
    float nextWeaponSwitchTime;
    Transform currentTarget;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSetup()
    {
        var player = FindPlayerObject();
        if (player != null && player.GetComponent<G1AutonomousTrailerBot>() == null)
            player.AddComponent<G1AutonomousTrailerBot>();
    }

    public static void LaunchBotAndRecord()
    {
        var player = FindPlayerObject();
        if (player == null)
        {
            UnityEngine.Debug.Log("[G1Bot] Player not in current scene. Automatically loading 'TestScene' for autonomous recorder...");
            SceneManager.sceneLoaded += OnSceneLoadedForBot;
            SceneManager.LoadScene("TestScene");
            return;
        }

        var bot = player.GetComponent<G1AutonomousTrailerBot>();
        if (bot == null) bot = player.AddComponent<G1AutonomousTrailerBot>();
        bot.StartBot();
    }

    static void OnSceneLoadedForBot(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedForBot;
        var player = FindPlayerObject();
        if (player != null)
        {
            var bot = player.GetComponent<G1AutonomousTrailerBot>();
            if (bot == null) bot = player.AddComponent<G1AutonomousTrailerBot>();
            bot.StartBot();
        }
    }

    static GameObject FindPlayerObject()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) return player;
        var move = Object.FindObjectOfType<PlayerMovement>();
        return move != null ? move.gameObject : null;
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        mouseLook = GetComponentInChildren<MouseLook>();
        switcher = GetComponentInChildren<WeaponSwitcher>(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (IsRunning) StopBot();
            else StartBot();
        }

        if (!IsRunning) return;

        UpdateAutonomousAI();
    }

    void StartBot()
    {
        IsRunning = true;
        botStartTime = Time.time;
        frameCount = 0;
        Time.captureFramerate = 30;

        string projectDir = Path.Combine(Application.dataPath, "..");
        framesDir = Path.Combine(projectDir, "TrailerFramesBot");
        try
        {
            if (!Directory.Exists(framesDir))
                Directory.CreateDirectory(framesDir);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("[G1Bot] Frames directory error: " + e.Message);
        }

        UnityEngine.Debug.Log($"[G1Bot] ★ AUTONOMOUS TRAILER BOT LAUNCHED! Recording frames to {framesDir} (Press F10 to Stop) ★");
    }

    void UpdateAutonomousAI()
    {
        // 1. Scan for nearest active enemy target
        FindTargetEnemy();

        // 2. CharacterController Movement (Crash-proof: no NavMeshAgent conflicts!)
        if (controller != null && controller.enabled)
        {
            Vector3 moveDir = transform.forward * 4.5f + Vector3.down * 9.81f;
            controller.Move(moveDir * Time.deltaTime);
        }
        else
        {
            transform.Translate(Vector3.forward * 4.5f * Time.deltaTime, Space.Self);
        }

        // 3. Aim camera smoothly toward target
        if (currentTarget != null && mouseLook != null)
        {
            Vector3 aimPoint = currentTarget.position + Vector3.up * 1.2f;
            Vector3 dir = (aimPoint - mouseLook.transform.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(dir);
            mouseLook.transform.rotation = Quaternion.Slerp(mouseLook.transform.rotation, targetRot, Time.deltaTime * 12f);

            Vector3 fwdDir = new Vector3(dir.x, 0, dir.z).normalized;
            if (fwdDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwdDir), Time.deltaTime * 10f);
        }

        // 4. Weapon Cycling
        if (Time.time >= nextWeaponSwitchTime && switcher != null && switcher.weapons != null && switcher.weapons.Length > 0)
        {
            nextWeaponSwitchTime = Time.time + Random.Range(4f, 7f);
            int idx = Random.Range(0, Mathf.Min(4, switcher.weapons.Length));
            switcher.Select(idx);
        }

        // 5. Fire Weapon
        if (currentTarget != null && Time.time - lastShotTime >= 0.15f && switcher != null && switcher.weapons != null)
        {
            GameObject activeWep = null;
            foreach (var w in switcher.weapons)
            {
                if (w != null && w.activeSelf) { activeWep = w; break; }
            }

            if (activeWep != null)
            {
                lastShotTime = Time.time;
                activeWep.SendMessage("PrimaryAttack", SendMessageOptions.DontRequireReceiver);
            }
        }

        // Auto-stop after 30 seconds of autonomous gameplay
        if (Time.time - botStartTime >= 30f)
            StopBot();
    }

    void FindTargetEnemy()
    {
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
        {
            var hs = currentTarget.GetComponent<HealthSystem>();
            if (hs == null || !hs.IsDead)
            {
                if (Vector3.Distance(transform.position, currentTarget.position) <= targetScanRange)
                    return;
            }
        }

        var healthSystems = Object.FindObjectsByType<HealthSystem>(FindObjectsSortMode.None);
        HealthSystem bestHs = null;
        float bestDist = targetScanRange;

        foreach (var hs in healthSystems)
        {
            if (hs == null || hs.IsDead || hs.gameObject == gameObject) continue;
            if (!hs.CompareTag("Player"))
            {
                float d = Vector3.Distance(transform.position, hs.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestHs = hs;
                }
            }
        }

        currentTarget = bestHs != null ? bestHs.transform : null;
    }

    void LateUpdate()
    {
        if (!IsRunning || string.IsNullOrEmpty(framesDir)) return;

        try
        {
            frameCount++;
            string file = Path.Combine(framesDir, $"frame_{frameCount:D4}.png");
            ScreenCapture.CaptureScreenshot(file);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("[G1Bot] Screenshot error: " + e.Message);
        }
    }

    void StopBot()
    {
        if (!IsRunning) return;
        IsRunning = false;
        Time.captureFramerate = 0;

        UnityEngine.Debug.Log($"[G1Bot] Autonomous Bot finished! Captured {frameCount} frames. Compiling MP4...");

        string projectDir = Path.Combine(Application.dataPath, "..");
        string outPath = Path.Combine(projectDir, "CorvusSprawl_Trailer.mp4");
        string streamingPath = Path.Combine(projectDir, "Assets/StreamingAssets/CorvusSprawl_Trailer.mp4");
        string musicTrack = Path.Combine(projectDir, "renders/youtube_track.mp3");
        string ffmpegPath = "/opt/homebrew/bin/ffmpeg";

        try
        {
            string audioArgs = File.Exists(musicTrack) ? $"-i \"{musicTrack}\" -map 0:v:0 -map 1:a:0 -c:a aac -b:a 192k -af \"afade=t=out:st=27:d=3.0\" -shortest" : "";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-y -framerate 30 -i \"{framesDir}/frame_%04d.png\" {audioArgs} -c:v libx264 -pix_fmt yuv420p \"{outPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit();

            if (File.Exists(outPath))
            {
                File.Copy(outPath, streamingPath, true);
                UnityEngine.Debug.Log($"[G1Bot] ★ SUCCESS! Autonomous Gameplay Trailer MP4 saved to {outPath} ★");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("[G1Bot] FFmpeg error: " + e.Message);
        }
    }

    void OnGUI()
    {
        if (!IsRunning) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        var font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        if (font) style.font = font;

        GUI.color = new Color(0.2f, 0.9f, 0.4f, 0.95f);
        GUI.Label(new Rect(0, 16, Screen.width - 24, 26), $"🤖 AUTONOMOUS TRAILER BOT RECORDING ({frameCount} FRAMES) | PRESS F10 TO STOP", style);
        GUI.color = Color.white;
    }
}
