using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Cinematic in-engine cutscene and narrative camera manager.
/// Features opening typewriter intro text, eyelid wake-up camera animation, and character thoughts.
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

    // Typewriter status text
    private string titleChapter = "";
    private string titleSub = "";
    private string titleSubject = "";
    private string titleStatus = "";
    private string titleDirective = "";
    private float titleAlpha = 0f;

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

        // PHASE 1: Full Black Screen with Typewriter Status Text (4.5 seconds)
        float elapsed = 0f;
        while (elapsed < 4.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 4.5f;
            if (t < 0.2f) titleAlpha = t / 0.2f;
            else if (t > 0.8f) titleAlpha = (1.0f - t) / 0.2f;
            else titleAlpha = 1.0f;
            yield return null;
        }
        titleAlpha = 0f;

        // PHASE 2: Eyelid Wake-Up Sequence (Camera lying on floor looking up)
        Vector3 floorCamPos = playerCamTransform != null ? playerCamTransform.position - Vector3.up * 1.2f : Vector3.zero;
        Quaternion floorCamRot = Quaternion.Euler(65f, 0f, 15f);

        Vector3 eyeLevelPos = playerCamTransform != null ? playerCamTransform.position : Vector3.zero;
        Quaternion eyeLevelRot = playerCamTransform != null ? playerCamTransform.rotation : Quaternion.identity;

        if (mainCam != null)
        {
            mainCam.transform.position = floorCamPos;
            mainCam.transform.rotation = floorCamRot;
        }

        // First Eyelid Blink (eyes open slightly then close)
        elapsed = 0f;
        while (elapsed < 1.2f)
        {
            elapsed += Time.deltaTime;
            eyelidAlpha = 1.0f - Mathf.Sin((elapsed / 1.2f) * Mathf.PI) * 0.6f;
            yield return null;
        }

        // Second Eyelid Blink & Stand Up Motion (2.8 seconds)
        elapsed = 0f;
        ShowSubtitle("[CHAD'S THOUGHTS]: \"Ugh... my head. The experiment failed... Aliens everywhere, and government hit squads are killing all witnesses. I need to get up and ESCAPE NOW!\"", 6.0f);

        while (elapsed < 2.8f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 2.8f;
            eyelidAlpha = Mathf.Lerp(1.0f, 0.0f, t);

            if (mainCam != null)
            {
                mainCam.transform.position = Vector3.Lerp(floorCamPos, eyeLevelPos, t);
                mainCam.transform.rotation = Quaternion.Slerp(floorCamRot, eyeLevelRot, t);
            }
            yield return null;
        }

        // PHASE 3: Complete Wake-Up & Restore Player Control
        eyelidAlpha = 0f;
        targetLetterboxHeight = 0f;

        if (mainCam != null)
        {
            mainCam.transform.position = eyeLevelPos;
            mainCam.transform.rotation = eyeLevelRot;
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

        // Draw Typewriter Status Title Card during Phase 1
        if (titleAlpha > 0.01f && !string.IsNullOrEmpty(titleChapter))
        {
            Color oldCol = GUI.color;
            GUI.color = new Color(1f, 0.75f, 0.1f, titleAlpha);

            float startY = Screen.height * 0.28f;
            GUI.Label(new Rect(0, startY, Screen.width, 45), titleChapter, titleChapterStyle);

            GUI.color = new Color(0.9f, 0.9f, 0.9f, titleAlpha);
            GUI.Label(new Rect(0, startY + 48, Screen.width, 35), titleSub, titleSubStyle);

            GUI.color = new Color(0.2f, 0.9f, 0.4f, titleAlpha);
            GUI.Label(new Rect(0, startY + 83, Screen.width, 35), titleSubject, titleSubjectStyle);

            GUI.color = new Color(1f, 0.25f, 0.2f, titleAlpha);
            GUI.Label(new Rect(0, startY + 118, Screen.width, 35), titleStatus, titleStatusStyle);

            GUI.color = new Color(1f, 0.9f, 0.1f, titleAlpha);
            GUI.Label(new Rect(0, startY + 153, Screen.width, 35), titleDirective, titleDirectiveStyle);

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
