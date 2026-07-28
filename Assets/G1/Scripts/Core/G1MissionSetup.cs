using UnityEngine;

/// Declares a level's objectives at runtime. The scene builder fills the
/// `objectives` array; on Start they're registered with G1ObjectiveManager.
/// Reusable across every level for the multi-level campaign.
[RequireComponent(typeof(G1ObjectiveManager))]
public sealed class G1MissionSetup : MonoBehaviour
{
    [System.Serializable]
    public struct Def
    {
        public string id;
        public string description;
        public bool mandatory;
        public int count;
    }

    public Def[] objectives;

    void Start()
    {
        var om = G1ObjectiveManager.Instance;
        if (om == null || objectives == null) return;
        foreach (var d in objectives)
            om.AddObjective(d.id, d.description, d.mandatory, Mathf.Max(1, d.count));
    }
}

