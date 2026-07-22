using UnityEngine;
using UnityEngine.SceneManagement;

/// Helper trigger that launches the opening narrative and the Chad Thundercock
/// wake-up cutscene. On the first level it plays the skippable origin-story
/// cinematic (G1IntroStory) which then chains into the wake-up; on later levels
/// it goes straight to the wake-up.
public class G1IntroCutsceneTrigger : MonoBehaviour
{
    public string chapterTitle = "CHAPTER ONE: COLD START";
    public string locationSubtitle = "Corvus Deep Research Annex — Sub-Level C";
    public string subjectName = "Chad Thundercock";
    public string statusLine = "STATUS: EXPERIMENT FAILED — ALIENS HAVE OVERRIDDEN THE FACILITY";
    public string directiveLine = "GOVERNMENT ORDERS: KILL ALL WITNESSES. ESCAPE NOW!";

    [Header("Opening story")]
    [Tooltip("Play the full skippable origin-story cinematic before the wake-up.")]
    public bool playNarrativeIntro = true;
    [Tooltip("The narrative origin story only plays on this scene (the first level).")]
    public string introSceneName = "TestScene";

    private void Start()
    {
        bool firstLevel = SceneManager.GetActiveScene().name == introSceneName;

        if (playNarrativeIntro && firstLevel)
        {
            // A story may already have been requested by a duplicate trigger.
            if (G1IntroStory.RequestedOrPlayed)
                return;

            var story = gameObject.AddComponent<G1IntroStory>();
            story.Begin(chapterTitle, locationSubtitle, subjectName, statusLine, directiveLine);
            return;
        }

        // Later levels (or narrative disabled): straight to the wake-up.
        if (G1CutsceneManager.Instance != null)
            G1CutsceneManager.Instance.PlayWakeUpIntroCutscene(
                chapterTitle, locationSubtitle, subjectName, statusLine, directiveLine);
    }
}
