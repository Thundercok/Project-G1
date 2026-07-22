using UnityEngine;

/// Helper trigger that launches the typewriter chapter title card and intro cutscene for Chad Thundercock.
public class G1IntroCutsceneTrigger : MonoBehaviour
{
    public string chapterTitle = "CHAPTER ONE: COLD START";
    public string locationSubtitle = "Corvus Deep Research Annex — Sub-Level C";
    public string subjectName = "Chad Thundercock";
    public string statusLine = "EXPERIMENT FAILED — ALIENS HAVE OVERRIDDEN THE FACILITY";
    public string directiveLine = "THE GOVERNMENT WILL KILL ANY WITNESSES. ESCAPE NOW!";

    private void Start()
    {
        if (G1CutsceneManager.Instance != null)
        {
            Vector3 camStartPos = transform.position + Vector3.up * 2f - transform.forward * 4f;
            Quaternion camStartRot = Quaternion.Euler(15f, transform.eulerAngles.y, 0f);
            G1CutsceneManager.Instance.PlayIntroCutscene(chapterTitle, locationSubtitle, subjectName, statusLine, directiveLine, camStartPos, camStartRot, 6.0f);
        }
    }
}
