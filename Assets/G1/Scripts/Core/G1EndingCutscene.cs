using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Level 3 finale. Two endings, decided by what the player does at the ring:
///
///   STABILIZE (walk into the portal trigger) — the loop resets. The Auditor is
///     paid. Chad wakes on the locker-room floor. This is the "escape" the whole
///     game trained you to reach; it is the ending the Auditor wants.
///
///   COLLAPSE (destroy every G1ResonanceEmitter with the crowbar) — the ring
///     comes apart, the cycle resolves, every trapped echo is released, and the
///     Anchor is unmade across all iterations.
///
/// Whichever path fires first locks out the other.
public class G1EndingCutscene : MonoBehaviour
{
    static G1EndingCutscene instance;
    static bool sequenceStarted;
    public static bool IsPlaying => sequenceStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; sequenceStarted = false; }

    enum Ending { Stabilize, Collapse }
    Ending chosen;

    GUIStyle titleStyle;
    GUIStyle alertStyle;
    GUIStyle bodyStyle;
    float cardAlpha = 0f;
    Texture2D blackTex;

    private void Awake()
    {
        instance = this;
        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();
    }

    // STABILIZE: player stepped into the portal.
    private void OnTriggerEnter(Collider other)
    {
        if (sequenceStarted) return;
        if (other.CompareTag("Player") || other.GetComponent<PlayerMovement>() != null)
        {
            sequenceStarted = true;
            chosen = Ending.Stabilize;
            StartCoroutine(RoutineStabilize());
        }
    }

    // COLLAPSE: the last resonance emitter was destroyed. Called by G1ResonanceEmitter.
    public static void TriggerCollapse(Vector3 point)
    {
        if (sequenceStarted || instance == null) return;
        sequenceStarted = true;
        instance.chosen = Ending.Collapse;
        instance.StartCoroutine(instance.RoutineCollapse());
    }

    void LockPlayer()
    {
        if (G1CutsceneManager.Instance != null)
            G1CutsceneManager.Instance.isCutsceneActive = true;
        var mouseLook = FindFirstObjectByType<MouseLook>();
        var playerMove = FindFirstObjectByType<PlayerMovement>();
        if (mouseLook != null) mouseLook.enabled = false;
        if (playerMove != null) playerMove.enabled = false;
    }

    void Say(string line, float dur)
    {
        if (G1CutsceneManager.Instance != null)
            G1CutsceneManager.Instance.ShowSubtitle(line, dur);
    }

    // ---- Ending A: the loop continues -------------------------------------
    private IEnumerator RoutineStabilize()
    {
        LockPlayer();

        Say("[CHAD'S THOUGHTS]: \"The portal's right there. Step through, get out — just like every instinct is screaming.\"", 6f);
        yield return new WaitForSeconds(6.5f);
        Say("[THE AUDITOR]: \"Yes. That's it. Walk through. You always do.\"  (He closes his ledger, satisfied.)", 6f);
        yield return new WaitForSeconds(6.5f);

        yield return FadeToBlack();
        yield return new WaitForSeconds(8.0f);
        SceneManager.LoadScene("MenuScene");
    }

    // ---- Ending B: the loop breaks ----------------------------------------
    private IEnumerator RoutineCollapse()
    {
        LockPlayer();
        G1Audio.Play2D("explosion", 0.9f, 0.6f);

        Say("[CHAD'S THOUGHTS]: \"Not through it. Break it. The crowbar was the answer the whole time.\"", 6f);
        yield return new WaitForSeconds(6.5f);
        Say("[THE AUDITOR]: \"...No. You are the Anchor. If the ring goes, so do — \"  (For the first time, he looks afraid.)", 6.5f);
        yield return new WaitForSeconds(7.0f);

        yield return FadeToBlack();
        yield return new WaitForSeconds(8.0f);
        SceneManager.LoadScene("MenuScene");
    }

    private IEnumerator FadeToBlack()
    {
        float elapsed = 0f;
        while (elapsed < 2.0f)
        {
            elapsed += Time.deltaTime;
            cardAlpha = elapsed / 2.0f;
            yield return null;
        }
        cardAlpha = 1f;
    }

    private void OnGUI()
    {
        if (cardAlpha <= 0.01f) return;

        GUI.color = new Color(0, 0, 0, cardAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);
        if (cardAlpha < 0.99f) return;

        InitStyles();
        float y = Screen.height * 0.26f;

        if (chosen == Ending.Stabilize)
        {
            GUI.color = new Color(1f, 0.25f, 0.2f, 1f);
            GUI.Label(new Rect(0, y, Screen.width, 45), "STATUS: EXPERIMENT FAILED — FACILITY OVERRIDDEN", alertStyle);
            GUI.color = new Color(1f, 0.85f, 0.1f, 1f);
            GUI.Label(new Rect(0, y + 48, Screen.width, 35), "GOVERNMENT ORDER: TERMINATE ALL WITNESSES", alertStyle);
            GUI.color = new Color(0.2f, 0.9f, 0.4f, 1f);
            GUI.Label(new Rect(0, y + 95, Screen.width, 45), "SUBJECT: CHAD THUNDERCOCK", titleStyle);
            GUI.color = new Color(0.85f, 0.87f, 0.88f, 1f);
            GUI.Label(new Rect(0, y + 145, Screen.width, 35), "DISPOSITION: SURVIVED — RE-ANCHORED", bodyStyle);
            GUI.Label(new Rect(0, y + 178, Screen.width, 35), "ITERATION ##8   ·   AUDIT YIELD: NOMINAL", bodyStyle);
            GUI.Label(new Rect(0, y + 214, Screen.width, 35), "Somewhere, a locker-room floor is cold again.", bodyStyle);
        }
        else
        {
            GUI.color = new Color(0.16f, 0.85f, 0.85f, 1f);
            GUI.Label(new Rect(0, y, Screen.width, 45), "STATUS: THRESHOLD COLLAPSED — CYCLE RESOLVED", alertStyle);
            GUI.color = new Color(1f, 0.55f, 0.15f, 1f);
            GUI.Label(new Rect(0, y + 48, Screen.width, 35), "CONCORDANCE AUDIT: TERMINATED — ASSET LOST", alertStyle);
            GUI.color = new Color(0.2f, 0.9f, 0.4f, 1f);
            GUI.Label(new Rect(0, y + 95, Screen.width, 45), "SUBJECT: CHAD THUNDERCOCK", titleStyle);
            GUI.color = new Color(0.85f, 0.87f, 0.88f, 1f);
            GUI.Label(new Rect(0, y + 145, Screen.width, 35), "DISPOSITION: UNMADE — the door goes backwards.", bodyStyle);
            GUI.Label(new Rect(0, y + 178, Screen.width, 35), "For the first time in ##8 iterations, the floor stays empty.", bodyStyle);
            GUI.Label(new Rect(0, y + 214, Screen.width, 35), "The graffiti is gone. So is the hand that wrote it.", bodyStyle);
        }
    }

    private void InitStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 28, fontStyle = FontStyle.Bold
            };
        }
        if (alertStyle == null)
        {
            alertStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold
            };
        }
        if (bodyStyle == null)
        {
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Italic
            };
        }
    }
}
