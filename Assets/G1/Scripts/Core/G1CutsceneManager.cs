using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Cinematic in-engine cutscene and narrative camera manager.
/// Features sequential typewriter status text, realistic floor wake-up camera
/// motion, queued multi-beat subtitles with professional dark panel rendering,
/// and robust camera reference acquisition.
public class G1CutsceneManager : MonoBehaviour
{
    public static G1CutsceneManager Instance { get; private set; }

    [Header("Cutscene State")]
    public bool isCutsceneActive = false;

    private Camera mainCam;
    private Transform playerCamTransform;
    private MouseLook mouseLook;
    private PlayerMovement playerMove;

    private Texture2D blackTex;
    private Texture2D pixelTex;
    private float letterboxHeight = 0f;
    private float targetLetterboxHeight = 0f;

    // Subtitle system — supports queued multi-line beats
    private string currentSubtitle = "";
    private float subtitleTimer = 0f;
    private float subtitleFade = 0f;     // 0..1, used for fade-in/out
    private float subtitleDuration = 0f; // total duration of current subtitle

    // Sequential Typewriter status lines
    private int visibleLineCount = 0;
    private string titleChapter = "";
    private string titleSub = "";
    private string titleSubject = "";
    private string titleStatus = "";
    private string titleDirective = "";
    private float textAlpha = 0f;

    // Eyelid blink overlay alpha (1.0 = eyes closed, 0.0 = fully awake)
    private float eyelidAlpha = 0f;

    private Font font;
    private GUIStyle titleChapterStyle;
    private GUIStyle titleSubStyle;
    private GUIStyle titleSubjectStyle;
    private GUIStyle titleStatusStyle;
    private GUIStyle titleDirectiveStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle subtitleSpeakerStyle;

    // Pre-parsed subtitle parts (speaker tag vs body)
    private string subtitleSpeaker = "";
    private string subtitleBody = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();

        pixelTex = Texture2D.whiteTexture;
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
    }

    private void Update()
    {
        // Smoothly animate letterbox bars
        letterboxHeight = Mathf.Lerp(letterboxHeight, targetLetterboxHeight, Time.deltaTime * 6f);

        if (subtitleTimer > 0f)
        {
            subtitleTimer -= Time.deltaTime;

            // Fade in during first 0.3s
            float elapsed = subtitleDuration - subtitleTimer;
            if (elapsed < 0.3f)
                subtitleFade = elapsed / 0.3f;
            // Fade out during last 0.5s
            else if (subtitleTimer < 0.5f)
                subtitleFade = subtitleTimer / 0.5f;
            else
                subtitleFade = 1f;

            if (subtitleTimer <= 0f)
            {
                currentSubtitle = "";
                subtitleSpeaker = "";
                subtitleBody = "";
                subtitleFade = 0f;
            }
        }
    }

    /// Acquire camera and player references, retrying if they haven't spawned yet.
    private IEnumerator AcquirePlayerReferences()
    {
        float timeout = 3f;
        float waited = 0f;
        while (waited < timeout)
        {
            mainCam = Camera.main;
            if (mainCam != null)
            {
                playerCamTransform = mainCam.transform;
                mouseLook = FindFirstObjectByType<MouseLook>();
                playerMove = FindFirstObjectByType<PlayerMovement>();
                if (mouseLook != null && playerMove != null)
                    yield break; // Got everything
            }
            yield return null;
            waited += Time.deltaTime;
        }
        // Best-effort: use whatever we found
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null) playerCamTransform = mainCam.transform;
        if (mouseLook == null) mouseLook = FindFirstObjectByType<MouseLook>();
        if (playerMove == null) playerMove = FindFirstObjectByType<PlayerMovement>();
    }

    public void PlayWakeUpIntroCutscene(string chapter, string subLocation, string subjectName, string status, string directive)
    {
        StartCoroutine(RoutineWakeUpCutscene(chapter, subLocation, subjectName, status, directive));
    }

    private IEnumerator RoutineWakeUpCutscene(string chapter, string subLocation, string subjectName, string status, string directive)
    {
        isCutsceneActive = true;
        targetLetterboxHeight = Screen.height * 0.14f;
        eyelidAlpha = 1.0f;
        textAlpha = 1.0f;

        // Robustly acquire player references (handles race conditions)
        yield return StartCoroutine(AcquirePlayerReferences());

        if (mouseLook != null) mouseLook.enabled = false;
        if (playerMove != null) playerMove.enabled = false;

        titleChapter = chapter;
        titleSub = subLocation;
        titleSubject = $"SUBJECT: {subjectName.ToUpper()}";
        titleStatus = status;
        titleDirective = directive;

        // ─────────────────────────────────────────────────────────
        // PHASE 1: Sequential Line-by-Line Typewriter Reveal
        // ─────────────────────────────────────────────────────────
        visibleLineCount = 0;
        yield return new WaitForSeconds(0.5f);

        // Play a quiet terminal boot sound for atmosphere
        G1Audio.Play2D("door_servo", 0.2f, 0.6f, 0f);

        visibleLineCount = 1; // Chapter
        yield return new WaitForSeconds(1.1f);

        visibleLineCount = 2; // Sub-location
        yield return new WaitForSeconds(1.1f);

        visibleLineCount = 3; // Subject
        yield return new WaitForSeconds(1.1f);

        visibleLineCount = 4; // Status
        yield return new WaitForSeconds(1.2f);

        visibleLineCount = 5; // Directive
        yield return new WaitForSeconds(2.2f); // Hold full text long enough to read

        // Smoothly fade out text lines before eyes open
        float fadeElapsed = 0f;
        while (fadeElapsed < 0.8f)
        {
            fadeElapsed += Time.deltaTime;
            textAlpha = 1.0f - (fadeElapsed / 0.8f);
            yield return null;
        }
        textAlpha = 0f;
        visibleLineCount = 0;

        // ─────────────────────────────────────────────────────────
        // PHASE 2: Realistic Floor Wake-Up Sequence
        // ─────────────────────────────────────────────────────────
        Vector3 standingPos = playerCamTransform != null
            ? playerCamTransform.position
            : new Vector3(0f, 1.6f, -14f);
        Quaternion standingRot = playerCamTransform != null
            ? playerCamTransform.rotation
            : Quaternion.identity;

        Vector3 floorPos = standingPos - new Vector3(0f, 1.35f, 0f);
        Quaternion floorRot = Quaternion.Euler(75f, standingRot.eulerAngles.y - 30f, 45f);

        if (mainCam != null)
        {
            mainCam.transform.position = floorPos;
            mainCam.transform.rotation = floorRot;
        }

        // First Eyelid Blink — a groggy flutter (open briefly, close, open again)
        float elapsed = 0f;
        while (elapsed < 1.0f)
        {
            elapsed += Time.deltaTime;
            float t01 = elapsed / 1.0f;
            // Quick flutter: sin curve that opens to ~50%, closes, opens to ~60%
            eyelidAlpha = 1.0f - Mathf.Sin(t01 * Mathf.PI) * 0.5f;
            yield return null;
        }

        // Brief darkness again — eyes slam shut
        eyelidAlpha = 0.95f;
        yield return new WaitForSeconds(0.3f);

        // Second blink — slightly wider this time
        elapsed = 0f;
        while (elapsed < 0.8f)
        {
            elapsed += Time.deltaTime;
            float t01 = elapsed / 0.8f;
            eyelidAlpha = 0.95f - Mathf.Sin(t01 * Mathf.PI) * 0.65f;
            yield return null;
        }
        eyelidAlpha = 0.85f;
        yield return new WaitForSeconds(0.15f);

        // ─────────────────────────────────────────────────────────
        // PHASE 2B: Multi-Beat Internal Monologue (during stand-up)
        // ─────────────────────────────────────────────────────────
        ShowSubtitle("[CHAD]: ...Ugh. My head.", 2.4f);

        // Slow Push-Up & Stand-Up from floor (5s with breathing wobble)
        elapsed = 0f;
        float duration = 5.0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Eyelid opens completely during the first third
            eyelidAlpha = Mathf.Lerp(0.85f, 0.0f, Mathf.Clamp01(t * 3f));

            if (mainCam != null)
            {
                // Smooth position lift from floor to standing
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 currentPos = Vector3.Lerp(floorPos, standingPos, smoothT);

                // Heavy breathing head wobble (stronger early, fades out)
                float wobbleIntensity = (1f - t) * (1f - t); // quadratic falloff
                float wobbleX = Mathf.Sin(t * Mathf.PI * 5f) * 0.04f * wobbleIntensity;
                float wobbleY = Mathf.Cos(t * Mathf.PI * 3.5f) * 0.025f * wobbleIntensity;
                currentPos += new Vector3(wobbleX, wobbleY, 0f);

                // Smooth rotation un-roll from sideways to upright
                Quaternion currentRot = Quaternion.Slerp(floorRot, standingRot, smoothT);

                mainCam.transform.position = currentPos;
                mainCam.transform.rotation = currentRot;
            }

            // Trigger subtitle beats at timed intervals during stand-up
            if (t > 0.35f && t < 0.36f)
                ShowSubtitle("[CHAD]: The experiment... it failed.", 2.2f);
            if (t > 0.65f && t < 0.66f)
                ShowSubtitle("[CHAD]: I need to get out. Now.", 2.5f);

            yield return null;
        }

        // ─────────────────────────────────────────────────────────
        // PHASE 3: Complete Wake-Up & Restore Player Control
        // ─────────────────────────────────────────────────────────
        eyelidAlpha = 0f;
        targetLetterboxHeight = 0f;

        if (mainCam != null)
        {
            mainCam.transform.position = standingPos;
            mainCam.transform.rotation = standingRot;
        }

        if (mouseLook != null) mouseLook.enabled = true;
        if (playerMove != null) playerMove.enabled = true;

        isCutsceneActive = false;
    }

    /// Show a subtitle. If a speaker tag is present in brackets (e.g. "[CHAD]: text"),
    /// it will be rendered separately with a teal highlight.
    public void ShowSubtitle(string text, float dur = 4f)
    {
        currentSubtitle = text;
        subtitleTimer = dur;
        subtitleDuration = dur;
        subtitleFade = 0f;

        // Parse speaker tag: "[SPEAKER]: body"
        if (text.StartsWith("[") && text.Contains("]:"))
        {
            int endBracket = text.IndexOf("]:");
            subtitleSpeaker = text.Substring(0, endBracket + 2); // e.g. "[CHAD]:"
            subtitleBody = text.Substring(endBracket + 2).TrimStart();
        }
        else
        {
            subtitleSpeaker = "";
            subtitleBody = text;
        }
    }

    private void OnGUI()
    {
        InitStyles();

        // Draw Eyelid / Full Black overlay during wake-up
        if (eyelidAlpha > 0.01f)
        {
            Color oldCol = GUI.color;
            GUI.color = new Color(0, 0, 0, eyelidAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);
            GUI.color = oldCol;
        }

        // Draw top & bottom letterbox bars
        if (letterboxHeight > 1f && eyelidAlpha < 0.99f)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, letterboxHeight), blackTex);
            GUI.DrawTexture(new Rect(0, Screen.height - letterboxHeight, Screen.width, letterboxHeight), blackTex);
        }

        // Draw Sequential Typewriter Status Title Card during Phase 1
        if (textAlpha > 0.01f && visibleLineCount > 0)
        {
            DrawTypewriterTitleCard();
        }

        // Draw Subtitles (professional dark panel rendering)
        if (!string.IsNullOrEmpty(currentSubtitle) && subtitleFade > 0.01f)
        {
            DrawSubtitle();
        }
    }

    private void DrawTypewriterTitleCard()
    {
        Color oldCol = GUI.color;
        float startY = Screen.height * 0.28f;

        // Dark panel background behind title card for legibility
        GUI.color = new Color(0f, 0f, 0f, 0.55f * textAlpha);
        float panelTop = startY - 20f;
        float panelH = 220f;
        GUI.DrawTexture(new Rect(Screen.width * 0.1f, panelTop, Screen.width * 0.8f, panelH), pixelTex);
        GUI.color = oldCol;

        // Teal accent line at top of panel
        GUI.color = new Color(0.16f, 0.75f, 0.75f, 0.5f * textAlpha);
        GUI.DrawTexture(new Rect(Screen.width * 0.1f, panelTop, Screen.width * 0.8f, 2f), pixelTex);
        GUI.color = oldCol;

        // Line 1: Chapter Title (amber/gold)
        if (visibleLineCount >= 1 && !string.IsNullOrEmpty(titleChapter))
        {
            GUI.color = new Color(1f, 0.75f, 0.1f, textAlpha);
            GUI.Label(new Rect(0, startY, Screen.width, 45), titleChapter, titleChapterStyle);
        }

        // Line 2: Sub-Location (light grey)
        if (visibleLineCount >= 2 && !string.IsNullOrEmpty(titleSub))
        {
            GUI.color = new Color(0.9f, 0.9f, 0.9f, textAlpha * 0.85f);
            GUI.Label(new Rect(0, startY + 48, Screen.width, 35), titleSub, titleSubStyle);
        }

        // Separator line
        if (visibleLineCount >= 3)
        {
            GUI.color = new Color(0.16f, 0.75f, 0.75f, 0.3f * textAlpha);
            GUI.DrawTexture(new Rect(Screen.width * 0.3f, startY + 82, Screen.width * 0.4f, 1f), pixelTex);
        }

        // Line 3: Subject Name (green)
        if (visibleLineCount >= 3 && !string.IsNullOrEmpty(titleSubject))
        {
            GUI.color = new Color(0.2f, 0.9f, 0.4f, textAlpha);
            GUI.Label(new Rect(0, startY + 90, Screen.width, 35), titleSubject, titleSubjectStyle);
        }

        // Line 4: Status Warning (red)
        if (visibleLineCount >= 4 && !string.IsNullOrEmpty(titleStatus))
        {
            GUI.color = new Color(1f, 0.25f, 0.2f, textAlpha);
            GUI.Label(new Rect(0, startY + 125, Screen.width, 35), titleStatus, titleStatusStyle);
        }

        // Line 5: Directive (yellow)
        if (visibleLineCount >= 5 && !string.IsNullOrEmpty(titleDirective))
        {
            GUI.color = new Color(1f, 0.9f, 0.1f, textAlpha);
            GUI.Label(new Rect(0, startY + 160, Screen.width, 35), titleDirective, titleDirectiveStyle);
        }

        GUI.color = oldCol;
    }

    private void DrawSubtitle()
    {
        Color oldCol = GUI.color;
        float alpha = subtitleFade;

        // Position: above bottom letterbox bar (or near bottom of screen)
        float subY = Screen.height - letterboxHeight - 60f;
        if (letterboxHeight < 10f) subY = Screen.height - 90f;

        float panelW = Screen.width * 0.72f;
        float panelX = (Screen.width - panelW) * 0.5f;
        float panelH = 46f;

        // Dark semi-transparent panel background
        GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.82f * alpha);
        GUI.DrawTexture(new Rect(panelX, subY, panelW, panelH), pixelTex);

        // Teal accent line at bottom of subtitle panel
        GUI.color = new Color(0.16f, 0.75f, 0.75f, 0.45f * alpha);
        GUI.DrawTexture(new Rect(panelX, subY + panelH - 2f, panelW, 2f), pixelTex);

        // Small teal accent on left edge
        GUI.color = new Color(0.16f, 0.75f, 0.75f, 0.6f * alpha);
        GUI.DrawTexture(new Rect(panelX, subY, 3f, panelH), pixelTex);

        // Render speaker tag in teal, body text in white
        if (!string.IsNullOrEmpty(subtitleSpeaker))
        {
            // Speaker tag (teal)
            GUI.color = new Color(0.16f, 0.75f, 0.75f, alpha);
            float speakerW = subtitleSpeakerStyle.CalcSize(new GUIContent(subtitleSpeaker)).x + 8f;
            GUI.Label(new Rect(panelX + 18f, subY + 10f, speakerW, 28f), subtitleSpeaker, subtitleSpeakerStyle);

            // Body text (white)
            GUI.color = new Color(0.92f, 0.94f, 0.95f, alpha);
            GUI.Label(new Rect(panelX + 18f + speakerW, subY + 10f, panelW - speakerW - 36f, 28f), subtitleBody, subtitleStyle);
        }
        else
        {
            // No speaker tag — render all white
            GUI.color = new Color(0.92f, 0.94f, 0.95f, alpha);
            GUI.Label(new Rect(panelX + 18f, subY + 10f, panelW - 36f, 28f), subtitleBody, subtitleStyle);
        }

        GUI.color = oldCol;
    }

    private void InitStyles()
    {
        if (titleChapterStyle != null) return;

        titleChapterStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 28,
            fontStyle = FontStyle.Bold
        };
        titleSubStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
            fontStyle = FontStyle.Italic
        };
        titleSubjectStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        titleStatusStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        titleDirectiveStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
            fontStyle = FontStyle.Bold
        };
        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 16,
            fontStyle = FontStyle.Normal,
            wordWrap = true,
        };
        subtitleSpeakerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
        };

        // Apply custom font to all styles
        if (font != null)
        {
            titleChapterStyle.font = font;
            titleSubStyle.font = font;
            titleSubjectStyle.font = font;
            titleStatusStyle.font = font;
            titleDirectiveStyle.font = font;
            subtitleStyle.font = font;
            subtitleSpeakerStyle.font = font;
        }
    }
}
