using UnityEngine;
using UnityEngine.SceneManagement;

/// Campaign flow: entering the trigger loads the next scene (or restarts the
/// current one when nextScene is empty). Clears the checkpoint — saves are
/// per-level.
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
        Debug.Log("Level complete → " +
                  (string.IsNullOrEmpty(nextScene) ? "restart" : nextScene));

        var playerHud = other.GetComponent<PlayerHUD>();
        if (playerHud != null)
        {
            playerHud.ShowTerminalLog("ELEVATOR STATUS: ESCAPING FACILITY...");
        }

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
