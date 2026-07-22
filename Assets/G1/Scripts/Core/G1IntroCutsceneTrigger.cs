using UnityEngine;

/// Helper trigger that launches the typewriter status text and Chad Thundercock wake-up cutscene.
public class G1IntroCutsceneTrigger : MonoBehaviour
{
    public string chapterTitle = "CHAPTER ONE: COLD START";
    public string locationSubtitle = "Corvus Deep Research Annex — Sub-Level C";
    public string subjectName = "Chad Thundercock";
    public string statusLine = "STATUS: EXPERIMENT FAILED — ALIENS HAVE OVERRIDDEN THE FACILITY";
    public string directiveLine = "GOVERNMENT ORDERS: KILL ALL WITNESSES. ESCAPE NOW!";

    private void Start()
    {
        if (G1CutsceneManager.Instance != null)
        {
            G1CutsceneManager.Instance.PlayWakeUpIntroCutscene(chapterTitle, locationSubtitle, subjectName, statusLine, directiveLine);
        }
    }
}
