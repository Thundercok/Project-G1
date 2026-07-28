using UnityEngine;

// Split out of G1MissionSetup.cs so the class name matches the file name.
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

/// Increments (or completes) an objective when this object's HealthSystem dies.
/// Put it on bosses/targets so killing them advances the mission.
[RequireComponent(typeof(HealthSystem))]
public sealed class G1ObjectiveOnDeath : MonoBehaviour
{
    public string objectiveId;

    void Start()
    {
        var hs = GetComponent<HealthSystem>();
        if (hs != null)
            hs.OnDeath += (p, n) =>
            {
                if (G1ObjectiveManager.Instance != null && !string.IsNullOrEmpty(objectiveId))
                    G1ObjectiveManager.Instance.IncrementProgress(objectiveId);
            };
    }
}
