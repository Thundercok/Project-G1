using UnityEngine;

// Split out of G1QuestGiver.cs so the class name matches the file name.
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

/// A "reach this place" quest step: when the player enters, it completes (or
/// advances) the given objective. Harmless before the objective exists —
/// IncrementProgress on an unknown id is a no-op.
[RequireComponent(typeof(BoxCollider))]
public sealed class G1QuestZone : MonoBehaviour
{
    public string objectiveId;
    public int amount = 1;
    bool done;

    void Reset() { GetComponent<BoxCollider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (done || !other.CompareTag("Player")) return;
        var om = G1ObjectiveManager.Instance;
        if (om == null) return;
        // only fire once the objective actually exists (quest was handed out)
        if (om.objectives.Find(o => o.id == objectiveId) == null) return;
        done = true;
        om.IncrementProgress(objectiveId, amount);
    }
}
