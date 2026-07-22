using System.Collections;
using UnityEngine;

/// Interactive GoldSrc-style onboarding tutorial system for Project G1.
/// Displays unobtrusive retro amber training prompts at the top-center of the screen.
/// Automatically detects input completion, plays a success ping, advances steps,
/// and saves completion status to PlayerPrefs ("G1_TutorialCompleted").
public class G1TutorialSystem : MonoBehaviour
{
    public static G1TutorialSystem Instance { get; private set; }

    private class TutorialStep
    {
        public string keyPrompt;
        public string description;
        public bool completed;
        public TutorialStep(string prompt, string desc)
        {
            keyPrompt = prompt;
            description = desc;
            completed = false;
        }
    }

    private TutorialStep[] steps = new TutorialStep[]
    {
        new TutorialStep("W A S D  +  [HOLD SPACE]", "MOVE & AUTO-BUNNYHOP"),
        new TutorialStep("[E] INTERACT  |  [F] FLASHLIGHT", "OPEN DOORS & TERMINALS"),
        new TutorialStep("[LMB] ATTACK  |  [RMB] ALT-FIRE", "CROWBAR / SUNG COMBAT"),
        new TutorialStep("[KILL ENEMIES]", "ACTIVE RECOVERY (+15 HP & +10 ARMOR)"),
        new TutorialStep("[G] GOD MODE  |  [V] FLY  |  [TAB] SPAWNER", "SANDBOX & CHEAT ASSISTANCE")
    };

    private int currentStep = 0;
    private bool tutorialFinished = false;
    private float stepStartTime;
    private float stepCompleteTime = -1f;

    private Font font;
    private GUIStyle promptStyle;
    private GUIStyle checkStyle;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");

        // Check if tutorial was previously finished
        if (PlayerPrefs.GetInt("G1_TutorialCompleted", 0) == 1)
        {
            tutorialFinished = true;
        }
        stepStartTime = Time.time;
    }

    void Update()
    {
        if (tutorialFinished || currentStep >= steps.Length) return;

        // Auto-detect player input for current step
        switch (currentStep)
        {
            case 0: // WASD + Space
                if (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0 || Input.GetButton("Jump"))
                    CompleteCurrentStep();
                break;

            case 1: // E or F
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
                    CompleteCurrentStep();
                break;

            case 2: // Fire 1 or Fire 2
                if (Input.GetButtonDown("Fire1") || Input.GetButtonDown("Fire2"))
                    CompleteCurrentStep();
                break;

            case 3: // Kill reward or time hold
                if (Time.time - stepStartTime > 6.5f)
                    CompleteCurrentStep();
                break;

            case 4: // Cheats or 7s time hold
                if (Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.Tab) || Time.time - stepStartTime > 7f)
                    CompleteCurrentStep();
                break;
        }
    }

    public void ReportEnemyKilled()
    {
        if (currentStep == 3)
        {
            CompleteCurrentStep();
        }
    }

    private void CompleteCurrentStep()
    {
        if (steps[currentStep].completed) return;

        steps[currentStep].completed = true;
        stepCompleteTime = Time.time;
        G1Audio.Play2D("pickup", 0.7f, 1.4f);

        StartCoroutine(AdvanceStepRoutine());
    }

    private IEnumerator AdvanceStepRoutine()
    {
        yield return new WaitForSeconds(0.6f);
        currentStep++;
        stepStartTime = Time.time;
        stepCompleteTime = -1f;

        if (currentStep >= steps.Length)
        {
            tutorialFinished = true;
            PlayerPrefs.SetInt("G1_TutorialCompleted", 1);
            PlayerPrefs.Save();
        }
    }

    private bool IsCutsceneActive()
    {
        if (G1CutsceneManager.Instance != null && G1CutsceneManager.Instance.isCutsceneActive) return true;
        if (G1IntroStory.IsActive) return true;
        if (G1EndingCutscene.IsPlaying) return true;
        return false;
    }

    void OnGUI()
    {
        if (tutorialFinished || currentStep >= steps.Length || IsCutsceneActive())
            return;

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };
            if (font) promptStyle.font = font;

            checkStyle = new GUIStyle(promptStyle)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }

        var step = steps[currentStep];
        float cx = Screen.width / 2f;
        float y = 72f;

        // Banner box background
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.Box(new Rect(cx - 280, y - 6, 560, 48), "", GUI.skin.box);

        // Text & check mark formatting
        bool isDone = step.completed;
        Color textColor = isDone ? new Color(0.2f, 1f, 0.4f, 0.95f) : new Color(1f, 0.75f, 0.15f, 0.9f);
        string text = isDone ? $"[✓]  {step.keyPrompt}  —  {step.description}" : $"TUTORIAL: {step.keyPrompt}  —  {step.description}";

        // Drop shadow
        promptStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(cx - 279, y + 1, 560, 36), text, promptStyle);

        // Foreground
        promptStyle.normal.textColor = textColor;
        GUI.Label(new Rect(cx - 280, y, 560, 36), text, promptStyle);

        GUI.color = Color.white;
    }

    /// Reset tutorial so player can re-play onboarding anytime
    public static void ResetTutorial()
    {
        PlayerPrefs.DeleteKey("G1_TutorialCompleted");
        PlayerPrefs.Save();
        if (Instance != null)
        {
            Instance.currentStep = 0;
            Instance.tutorialFinished = false;
            Instance.stepStartTime = Time.time;
            foreach (var s in Instance.steps) s.completed = false;
        }
    }
}
