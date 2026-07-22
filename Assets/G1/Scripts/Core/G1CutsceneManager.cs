using System.Collections;
using UnityEngine;

/// Cinematic in-engine cutscene and narrative camera manager.
/// Features sequential typewriter status text, realistic floor wake-up camera motion, and character thoughts.
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
    private float letterboxHeight = 0f;
    private float targetLetterboxHeight = 0f;

    private string currentSubtitle = "";
    private float subtitleTimer = 0f;

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

    private GUIStyle titleChapterStyle;
    private GUIStyle titleSubStyle;
    private GUIStyle titleSubjectStyle;
    private GUIStyle titleStatusStyle;
    private GUIStyle titleDirectiveStyle;
    private GUIStyle subtitleStyle;

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
    }

    private void Update()
    {
        // Smoothly animate letterbox bars
        letterboxHeight = Mathf.Lerp(letterboxHeight, targetLetterboxHeight, Time.deltaTime * 6f);

        if (subtitleTimer > 0f)
        {
            subtitleTimer -= Time.deltaTime;
            if (subtitleTimer <= 0f)
            {
                currentSubtitle = "";
            }
        }
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

        mainCam = Camera.main;
        if (mainCam != null)
        {
            playerCamTransform = mainCam.transform;
            mouseLook = FindFirstObjectByType<MouseLook>();
            playerMove = FindFirstObjectByType<PlayerMovement>();

            if (mouseLook != null) mouseLook.enabled = false;
            if (playerMove != null) playerMove.enabled = false;
        }

        titleChapter = chapter;
        titleSub = subLocation;
        titleSubject = $"SUBJECT: {subjectName.ToUpper()}";
        titleStatus = status;
        titleDirective = directive;

        // PHASE 1: Sequential Line-by-Line Typewriter Reveal (Clean & easy to read)
        visibleLineCount = 0;
        yield return new WaitForSeconds(0.5f);

        visibleLineCount = 1; // Show Chapter
        yield return new WaitForSeconds(1.1f);

        visibleLineCount = 2; // Show Sub-location
        yield return new WaitForSeconds(1.1f);

        visibleLineCount = 3; // Show Subject
        yield return new WaitForSeconds(1.1f);

        visibleLineCount = 4; // Show Status
        yield return new WaitForSeconds(1.2f);

        visibleLineCount = 5; // Show Directive
        yield return new WaitForSeconds(2.0f); // Hold full text so it's very clear to read

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

        // PHASE 2: Realistic Floor Wake-Up Sequence
        // Starting Pose: Lying sideways flat on the floor (Y = 0.25m, Roll = 45deg, Pitch = 75deg looking down)
        Vector3 standingPos = playerCamTransform != null ? playerCamTransform.position : new Vector3(0f, 1.6f, -14f);
        Quaternion standingRot = playerCamTransform != null ? playerCamTransform.rotation : Quaternion.identity;

        Vector3 floorPos = standingPos - new Vector3(0f, 1.35f, 0f); // Ground level
        Quaternion floorRot = Quaternion.Euler(75f, standingRot.eulerAngles.y - 30f, 45f); // Sideways floor posture

        if (mainCam != null)
        {
            mainCam.transform.position = floorPos;
            mainCam.transform.rotation = floorRot;
        }

        // First Eyelid Blink (slight open then close)
        float elapsed = 0f;
        while (elapsed < 1.4f)
        {
            elapsed += Time.deltaTime;
            eyelidAlpha = 1.0f - Mathf.Sin((elapsed / 1.4f) * Mathf.PI) * 0.5f;
            yield return null;
        }

        ShowSubtitle("[CHAD'S THOUGHTS]: \"*gasp*... *cough*... My head... The experiment failed. Aliens everywhere, and government hit squads are executing all witnesses. I need to get up and ESCAPE NOW!\"", 6.5f);

        // Slow Push-Up & Stand-Up Motion off Floor (4.5 seconds with heavy breathing head wobble)
        elapsed = 0f;
        float duration = 4.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Eyelid opens completely
            eyelidAlpha = Mathf.Lerp(1.0f, 0.0f, t * 1.5f);

            if (mainCam != null)
            {
                // Smooth position lift from floor to standing
                Vector3 currentPos = Vector3.Lerp(floorPos, standingPos, Mathf.SmoothStep(0f, 1f, t));

                // Add subtle heavy breathing head shake / wobble while pushing off floor
                float wobbleX = Mathf.Sin(t * Mathf.PI * 6f) * 0.03f * (1f - t);
                float wobbleY = Mathf.Cos(t * Mathf.PI * 4f) * 0.02f * (1f - t);
                currentPos += new Vector3(wobbleX, wobbleY, 0f);

                // Smooth rotation un-roll from sideways to upright eye-level
                Quaternion currentRot = Quaternion.Slerp(floorRot, standingRot, Mathf.SmoothStep(0f, 1f, t));

                mainCam.transform.position = currentPos;
                mainCam.transform.rotation = currentRot;
            }
            yield return null;
        }

        // PHASE 3: Complete Wake-Up & Restore Player Control
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

    public void ShowSubtitle(string text, float duration = 4f)
    {
        currentSubtitle = text;
        subtitleTimer = duration;
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
            Color oldCol = GUI.color;
            float startY = Screen.height * 0.28f;

            // Line 1: Chapter Title
            if (visibleLineCount >= 1 && !string.IsNullOrEmpty(titleChapter))
            {
                GUI.color = new Color(1f, 0.75f, 0.1f, textAlpha);
                GUI.Label(new Rect(0, startY, Screen.width, 45), titleChapter, titleChapterStyle);
            }

            // Line 2: Sub-Location
            if (visibleLineCount >= 2 && !string.IsNullOrEmpty(titleSub))
            {
                GUI.color = new Color(0.9f, 0.9f, 0.9f, textAlpha);
                GUI.Label(new Rect(0, startY + 48, Screen.width, 35), titleSub, titleSubStyle);
            }

            // Line 3: Subject Name
            if (visibleLineCount >= 3 && !string.IsNullOrEmpty(titleSubject))
            {
                GUI.color = new Color(0.2f, 0.9f, 0.4f, textAlpha);
                GUI.Label(new Rect(0, startY + 83, Screen.width, 35), titleSubject, titleSubjectStyle);
            }

            // Line 4: Status Warning
            if (visibleLineCount >= 4 && !string.IsNullOrEmpty(titleStatus))
            {
                GUI.color = new Color(1f, 0.25f, 0.2f, textAlpha);
                GUI.Label(new Rect(0, startY + 118, Screen.width, 35), titleStatus, titleStatusStyle);
            }

            // Line 5: Government Directive
            if (visibleLineCount >= 5 && !string.IsNullOrEmpty(titleDirective))
            {
                GUI.color = new Color(1f, 0.9f, 0.1f, textAlpha);
                GUI.Label(new Rect(0, startY + 153, Screen.width, 35), titleDirective, titleDirectiveStyle);
            }

            GUI.color = oldCol;
        }

        // Draw Subtitles
        if (!string.IsNullOrEmpty(currentSubtitle))
        {
            Color oldCol = GUI.color;
            GUI.color = Color.white;
            float subY = Screen.height - letterboxHeight - 45f;
            if (letterboxHeight < 10f) subY = Screen.height - 80f;

            GUI.Box(new Rect(Screen.width * 0.08f, subY, Screen.width * 0.84f, 38f), "", GUI.skin.box);
            GUI.Label(new Rect(Screen.width * 0.08f, subY + 6f, Screen.width * 0.84f, 30f), currentSubtitle, subtitleStyle);
            GUI.color = oldCol;
        }
    }

    private void InitStyles()
    {
        if (titleChapterStyle == null)
        {
            titleChapterStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };
        }
        if (titleSubStyle == null)
        {
            titleSubStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Italic
            };
        }
        if (titleSubjectStyle == null)
        {
            titleSubjectStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }
        if (titleStatusStyle == null)
        {
            titleStatusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }
        if (titleDirectiveStyle == null)
        {
            titleDirectiveStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };
        }
        if (subtitleStyle == null)
        {
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
