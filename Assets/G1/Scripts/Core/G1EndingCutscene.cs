using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Level 3 Final Threshold Ending Cutscene.
/// Triggers upon reaching the Xen Portal. Plays Chad Thundercock's final character thoughts & disposition card.
public class G1EndingCutscene : MonoBehaviour
{
    private bool hasTriggered = false;
    private GUIStyle titleStyle;
    private GUIStyle alertStyle;
    private GUIStyle bodyStyle;
    private float cardAlpha = 0f;
    private Texture2D blackTex;

    private void Awake()
    {
        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (other.CompareTag("Player") || other.GetComponent<PlayerMovement>() != null)
        {
            hasTriggered = true;
            StartCoroutine(RoutinePlayEndingSequence());
        }
    }

    private IEnumerator RoutinePlayEndingSequence()
    {
        var mouseLook = FindFirstObjectByType<MouseLook>();
        var playerMove = FindFirstObjectByType<PlayerMovement>();
        if (mouseLook != null) mouseLook.enabled = false;
        if (playerMove != null) playerMove.enabled = false;

        if (G1CutsceneManager.Instance != null)
        {
            G1CutsceneManager.Instance.ShowSubtitle("[CHAD'S THOUGHTS]: \"Experiment failed... facility overrun by aliens... and a government execution order on my head.\"", 6.0f);
        }

        yield return new WaitForSeconds(6.5f);

        if (G1CutsceneManager.Instance != null)
        {
            G1CutsceneManager.Instance.ShowSubtitle("[CHAD'S THOUGHTS]: \"They thought I wouldn't survive... Time to step through the portal and get out NOW!\"", 6.0f);
        }

        yield return new WaitForSeconds(6.5f);

        // Fade black screen
        float elapsed = 0f;
        while (elapsed < 2.0f)
        {
            elapsed += Time.deltaTime;
            cardAlpha = elapsed / 2.0f;
            yield return null;
        }
        cardAlpha = 1f;

        yield return new WaitForSeconds(8.0f);

        // Load Menu Scene
        SceneManager.LoadScene("MenuScene");
    }

    private void OnGUI()
    {
        if (cardAlpha <= 0.01f) return;

        GUI.color = new Color(0, 0, 0, cardAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);

        if (cardAlpha >= 0.99f)
        {
            InitStyles();

            float y = Screen.height * 0.28f;

            GUI.color = new Color(1f, 0.25f, 0.2f, 1f);
            GUI.Label(new Rect(0, y, Screen.width, 45), "STATUS: EXPERIMENT FAILED — FACILITY OVERRIDDEN", alertStyle);

            GUI.color = new Color(1f, 0.85f, 0.1f, 1f);
            GUI.Label(new Rect(0, y + 48, Screen.width, 35), "GOVERNMENT ORDER: TERMINATE ALL WITNESSES", alertStyle);

            GUI.color = new Color(0.2f, 0.9f, 0.4f, 1f);
            GUI.Label(new Rect(0, y + 95, Screen.width, 45), "SUBJECT: CHAD THUNDERCOCK", titleStyle);
            GUI.Label(new Rect(0, y + 145, Screen.width, 35), "SURVIVAL STATUS: SURVIVED ALIEN & MILITARY PURGE", bodyStyle);
            GUI.Label(new Rect(0, y + 180, Screen.width, 35), "DIRECTIVE COMPLETE: ESCAPED — STATUS: UNCONTAINED", bodyStyle);
        }
    }

    private void InitStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };
        }
        if (alertStyle == null)
        {
            alertStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
        }
        if (bodyStyle == null)
        {
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Italic
            };
        }
    }
}
