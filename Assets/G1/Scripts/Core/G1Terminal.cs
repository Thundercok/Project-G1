using UnityEngine;

public class G1Terminal : MonoBehaviour, IUsable
{
    [TextArea(3, 5)]
    public string logMessage = "LOG: SPECIMEN CONTAINER STABILITY CRITICAL.";

    public void OnUse(GameObject user)
    {
        var hud = user.GetComponent<PlayerHUD>();
        if (hud != null)
        {
            hud.ShowTerminalLog(logMessage);
            G1Audio.Play2D("pickup", 0.35f, 1.25f);
        }
    }
}
