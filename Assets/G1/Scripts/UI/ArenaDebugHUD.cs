using UnityEngine;

/// F3-toggled combat telemetry overlay: mobs engaging the player, player HP%,
/// opportunists waiting on the squad blackboard, and time since the last
/// alpha strike. Reads CombatArenaMonitor + SquadBlackboard, writes nothing.
public sealed class ArenaDebugHUD : MonoBehaviour
{
    public bool visible = true;
    public KeyCode toggleKey = KeyCode.F3;

    GUIStyle style;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            visible = !visible;
    }

    void OnGUI()
    {
        if (!visible)
            return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                richText = false,
            };
            style.normal.textColor = new Color(0.6f, 1f, 0.6f, 0.95f);
        }

        var monitor = CombatArenaMonitor.Instance;
        var board = SquadBlackboard.Instance;

        int mobs = monitor ? monitor.MobsEngagingPlayer : 0;
        float hpPct = monitor ? monitor.PlayerHealthPct * 100f : 0f;
        int opportunists = board ? board.OpportunistCount : 0;
        float alphaTs = board ? board.AlphaStrikeTimestamp : -1f;
        string alpha = alphaTs < 0f ? "never"
            : (Time.time - alphaTs).ToString("0.0") + "s ago";

        GUI.Box(new Rect(10, 10, 250, 102), "ARENA TELEMETRY (F3)");
        GUI.Label(new Rect(20, 32, 235, 20), $"Mobs engaging player: {mobs}", style);
        GUI.Label(new Rect(20, 50, 235, 20), $"Player HP: {hpPct:0}%", style);
        GUI.Label(new Rect(20, 68, 235, 20), $"Opportunists waiting: {opportunists}", style);
        GUI.Label(new Rect(20, 86, 235, 20), $"Last alpha strike: {alpha}", style);
    }
}
