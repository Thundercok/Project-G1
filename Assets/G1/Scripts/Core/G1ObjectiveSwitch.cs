using UnityEngine;

// Split out of G1AccessControl.cs so the class name matches the file name.
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

/// A switch that completes an objective and, optionally, powers a lock group.
///
/// The turbine-hall breaker is the level's one gate: until it is thrown, the
/// research wing's outer airlock will not respond, so "restore power" is not a
/// chore bolted on beside the real objective — it *is* the door.
public sealed class G1ObjectiveSwitch : MonoBehaviour, IUsable
{
    public string objectiveId = "";
    public string message = "SYSTEM ONLINE";
    public string unlocksGroup = "";
    public int amount = 1;

    bool thrown;

    public void OnUse(GameObject user)
    {
        if (thrown)
        {
            Debug.Log(message + " (already)");
            return;
        }
        thrown = true;

        if (!string.IsNullOrEmpty(objectiveId))
            G1ObjectiveManager.Instance?.IncrementProgress(objectiveId, amount);

        if (!string.IsNullOrEmpty(unlocksGroup))
            foreach (var k in Object.FindObjectsOfType<G1Keycard>())
                if (k.group == unlocksGroup) k.powered = true;

        // Every lamp in the level jumps: the manifest lights were installed at
        // a fraction of their intensity so that throwing this reads as the
        // building waking up rather than as one message on the HUD.
        foreach (var l in Object.FindObjectsOfType<Light>())
            if (l.type != LightType.Directional) l.intensity *= 2.2f;

        G1Audio.Play("door_servo", transform.position, 0.5f, 0.9f);
        Debug.Log(message);
    }
}
