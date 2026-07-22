using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Cinematic in-engine cutscene and narrative camera manager.
/// Handles letterbox bars, typewriter title cards, camera lerps, and subtitles.
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

    private string titleChapter = "";
    private string titleSub = "";
    private string titleSubject = "";
    private string titleStatus = "";
    private string titleDirective = "";
    private float titleAlpha = 0f;

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

    public void PlayIntroCutscene(string chapter, string subLocation, string subjectName, string status, string directive, Vector3 camStartPos, Quaternion camStartRot, float duration = 6f)
    {
        StartCoroutine(RoutineIntroCutscene(chapter, subLocation, subjectName, status, directive, camStartPos, camStartRot, duration));
    }

    private IEnumerator RoutineIntroCutscene(string chapter, string subLocation, string subjectName, string status, string directive, Vector3 camStartPos, Quaternion camStartRot, float duration)
    {
        isCutsceneActive = true;
        targetLetterboxHeight = Screen.height * 0.14f;

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

        Vector3 originalPos = mainCam != null ? mainCam.transform.position : Vector3.zero;
        Quaternion originalRot = mainCam != null ? mainCam.transform.rotation : Quaternion.identity;

        if (mainCam != null)
        {
            mainCam.transform.position = camStartPos;
            mainCam.transform.rotation = camStartRot;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Fade title card in then out
            if (t < 0.15f) titleAlpha = t / 0.15f;
            else if (t > 0.85f) titleAlpha = (1f - t) / 0.15f;
            else titleAlpha = 1f;

            // Smoothly lerp camera back toward player head
            if (mainCam != null && playerCamTransform != null)
            {
                mainCam.transform.position = Vector3.Lerp(camStartPos, originalPos, t);
                mainCam.transform.rotation = Quaternion.Slerp(camStartRot, originalRot, t);
            }

            yield return null;
        }

        // Restore player control
        if (mainCam != null)
        {
            mainCam.transform.position = originalPos;
            mainCam.transform.rotation = originalRot;
        }

        if (mouseLook != null) mouseLook.enabled = true;
        if (playerMove != null) playerMove.enabled = true;

        targetLetterboxHeight = 0f;
        titleAlpha = 0f;
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

        // Draw top & bottom letterbox bars
        if (letterboxHeight > 1f)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, letterboxHeight), blackTex);
            GUI.DrawTexture(new Rect(0, Screen.height - letterboxHeight, Screen.width, letterboxHeight), blackTex);
        }

        // Draw Title Card
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

            GUI.Box(new Rect(Screen.width * 0.1f, subY, Screen.width * 0.8f, 38f), "", GUI.skin.box);
            GUI.Label(new Rect(Screen.width * 0.1f, subY + 6f, Screen.width * 0.8f, 30f), currentSubtitle, subtitleStyle);
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
