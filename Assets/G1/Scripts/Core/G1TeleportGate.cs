using UnityEngine;

// Split out of G1Rescuable.cs so the class name matches the file name.
//
// Unity only creates a MonoScript for the type whose name matches its file. Any
// other MonoBehaviour in that file can still be added by AddComponent while the
// editor session lasts, and then serialises into the scene as `m_Script:
// {fileID: 0}` — a component that silently is not there the next time the
// scene is opened. Twenty-eight of them had accumulated, including every quest
// zone, every objective-on-death and the extraction gate, which is why walking
// into a quest trigger did nothing and why killing the boss never ticked the
// objective. It fails without an error at any point, so the only defence is the
// naming rule.

/// Visual extraction gate: a ring that stays dim until every mandatory
/// objective is complete, then flares to signal it is active. The actual
/// scene-load is handled by a sibling G1LevelExitTrigger (which already gates
/// on objective completion).
public sealed class G1TeleportGate : MonoBehaviour
{
    public Renderer[] ringRenderers;
    bool online;

    void Update()
    {
        if (online) return;
        var om = G1ObjectiveManager.Instance;
        if (om == null || !om.IsLevelComplete()) return;
        online = true;
        foreach (var r in ringRenderers)
        {
            if (r == null) continue;
            var m = r.sharedMaterial;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.2f, 1f, 0.9f) * 2.5f);
        }
        G1Audio.Play("door_servo", transform.position, 0.9f, 1.3f);
    }
}
