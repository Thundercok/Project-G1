using UnityEngine;
using UnityEngine.SceneManagement;

/// Campaign flow: entering the trigger loads the next scene (or restarts the
/// current one when nextScene is empty). Clears the checkpoint and saves
/// player inventory & unlocked campaign progress.
public class G1LevelExitTrigger : MonoBehaviour
{
    public string nextScene = "";
    public float delay = 3.5f;
    public bool requireUnlock = false;
    public static bool ElevatorUnlocked = false;

    bool fired;

    private void OnTriggerEnter(Collider other)
    {
        if (fired || !other.CompareTag("Player"))
            return;

        // Check if level objectives are incomplete
        if (G1ObjectiveManager.Instance != null && !G1ObjectiveManager.Instance.IsLevelComplete())
        {
            var hud = other.GetComponent<PlayerHUD>();
            if (hud != null)
            {
                string activeText = G1ObjectiveManager.Instance.GetActiveObjectiveText();
                hud.ShowTerminalLog($"OBJECTIVE INCOMPLETE: {activeText.ToUpper()}");
            }
            return;
        }

        if (requireUnlock && !ElevatorUnlocked)
        {
            var hud = other.GetComponent<PlayerHUD>();
            if (hud != null)
            {
                hud.ShowTerminalLog("ELEVATOR CONSOLE: EMERGENCY OVERRIDE CODES REQUIRED. ACCESS OVERRIDE TERMINAL.");
            }
            return;
        }

        fired = true;
        string targetScene = string.IsNullOrEmpty(nextScene) ? SceneManager.GetActiveScene().name : nextScene;
        Debug.Log("Level complete → " + targetScene);

        var playerHud = other.GetComponent<PlayerHUD>();
        if (playerHud != null)
        {
            playerHud.ShowTerminalLog("ELEVATOR STATUS: ESCAPING FACILITY... LEVEL CLEARED");
        }

        // Save inventory and campaign progression for next level
        G1SaveSystem.SaveLevelClear(targetScene, other.gameObject);

        PlayerPrefs.DeleteKey("G1_CP_Data");
        G1Audio.Play2D("pickup", 0.8f, 0.8f);
        Invoke(nameof(LoadNext), delay);
    }

    private void LoadNext()
    {
        SceneManager.LoadScene(string.IsNullOrEmpty(nextScene)
            ? SceneManager.GetActiveScene().name : nextScene);
    }
}

