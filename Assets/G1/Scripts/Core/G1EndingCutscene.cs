using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Level 3 Final Threshold Ending Cutscene.
/// Triggers upon reaching the Xen Portal. Plays G-Man Auditor final disposition cutscene.
public class G1EndingCutscene : MonoBehaviour
{
    private bool hasTriggered = false;
    private GUIStyle titleStyle;
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
            G1CutsceneManager.Instance.ShowSubtitle("[THE AUDITOR]: \"Subject Chad Thundercock... your performance in Sector C has been... extraordinary.\"", 6.0f);
        }

        yield return new WaitForSeconds(6.5f);

        if (G1CutsceneManager.Instance != null)
        {
            G1CutsceneManager.Instance.ShowSubtitle("[THE AUDITOR]: \"My employers have authorized your retention. Step through the portal, Mr. Thundercock.\"", 6.0f);
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

        yield return new WaitForSeconds(7.0f);

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
            GUI.color = new Color(0.2f, 0.9f, 0.4f, 1f);

            float y = Screen.height * 0.35f;
            GUI.Label(new Rect(0, y, Screen.width, 50), "SUBJECT: CHAD THUNDERCOCK", titleStyle);
            GUI.Label(new Rect(0, y + 60, Screen.width, 40), "STATUS: EVALUATION COMPLETE", bodyStyle);
            GUI.Label(new Rect(0, y + 100, Screen.width, 40), "DISPOSITION: RETAINED FOR SPECIAL OPERATIONS", bodyStyle);
        }
    }

    private void InitStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold
            };
        }
        if (bodyStyle == null)
        {
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Italic
            };
        }
    }
}
