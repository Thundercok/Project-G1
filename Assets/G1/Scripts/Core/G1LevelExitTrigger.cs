using UnityEngine;
using UnityEngine.SceneManagement;

public class G1LevelExitTrigger : MonoBehaviour
{
    public bool requireUnlock = false;
    public static bool ElevatorUnlocked = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (requireUnlock && !ElevatorUnlocked)
            {
                var hud = other.GetComponent<PlayerHUD>();
                if (hud != null)
                {
                    hud.ShowTerminalLog("ELEVATOR CONSOLE: EMERGENCY OVERRIDE CODES REQUIRED. ACCESS OVERRIDE TERMINAL.");
                }
                return;
            }

            Debug.LogWarning("🎉 LEVEL COMPLETE! You escaped the facility!");
            var playerHud = other.GetComponent<PlayerHUD>();
            if (playerHud != null)
            {
                playerHud.ShowTerminalLog("ELEVATOR STATUS: ESCAPING FACILITY...");
            }
            // Reload the scene after 3.5 seconds to restart the demo
            Invoke("ReloadScene", 3.5f);
        }
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
