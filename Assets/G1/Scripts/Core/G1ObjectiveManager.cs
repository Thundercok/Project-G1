using System.Collections.Generic;
using UnityEngine;

/// Central manager for Level Objectives and Level Completion status.
/// Keeps track of primary and secondary objectives for each level.
public class G1ObjectiveManager : MonoBehaviour
{
    public static G1ObjectiveManager Instance { get; private set; }

    [System.Serializable]
    public class Objective
    {
        public string id;
        public string description;
        public bool isCompleted;
        public bool isMandatory = true;
        public int currentCount;
        public int requiredCount = 1;

        public Objective(string id, string description, bool isMandatory = true, int requiredCount = 1)
        {
            this.id = id;
            this.description = description;
            this.isMandatory = isMandatory;
            this.requiredCount = requiredCount;
            this.currentCount = 0;
            this.isCompleted = false;
        }

        public string GetDisplayText()
        {
            if (requiredCount > 1)
                return $"{description} ({currentCount}/{requiredCount})";
            return description;
        }
    }

    public List<Objective> objectives = new List<Objective>();

    private static readonly List<G1Waypoint> _waypoints = new List<G1Waypoint>();

    public static void RegisterWaypoint(G1Waypoint wp)
    {
        if (wp != null && !_waypoints.Contains(wp))
            _waypoints.Add(wp);
    }

    public static void UnregisterWaypoint(G1Waypoint wp)
    {
        if (wp != null)
            _waypoints.Remove(wp);
    }

    public G1Waypoint GetActiveWaypoint()
    {
        var activeObj = GetActiveObjective();
        if (activeObj != null)
        {
            // First look for waypoint matching active objective ID
            var match = _waypoints.Find(w => w != null && w.isActive && w.objectiveId == activeObj.id);
            if (match != null) return match;
        }

        // Fallback: return any active waypoint
        return _waypoints.Find(w => w != null && w.isActive);
    }

    public delegate void ObjectiveUpdateHandler(Objective activeObjective, bool isNewCompletion);
    public static event ObjectiveUpdateHandler OnObjectiveUpdated;

    void Awake()
    {
        Instance = this;
    }

    public void AddObjective(string id, string description, bool mandatory = true, int requiredCount = 1)
    {
        objectives.Add(new Objective(id, description, mandatory, requiredCount));
        NotifyUpdate(null, false);
    }

    public void IncrementProgress(string id, int amount = 1)
    {
        var obj = objectives.Find(o => o.id == id);
        if (obj == null || obj.isCompleted) return;

        obj.currentCount += amount;
        if (obj.currentCount >= obj.requiredCount)
        {
            obj.currentCount = obj.requiredCount;
            obj.isCompleted = true;
            Debug.Log($"[G1ObjectiveManager] Objective completed: {obj.description}");
            NotifyUpdate(obj, true);
        }
        else
        {
            NotifyUpdate(obj, false);
        }
    }

    public void CompleteObjective(string id)
    {
        var obj = objectives.Find(o => o.id == id);
        if (obj == null || obj.isCompleted) return;

        obj.isCompleted = true;
        obj.currentCount = obj.requiredCount;
        Debug.Log($"[G1ObjectiveManager] Objective completed: {obj.description}");
        NotifyUpdate(obj, true);
    }

    public bool IsLevelComplete()
    {
        if (objectives.Count == 0) return true;
        foreach (var obj in objectives)
        {
            if (obj.isMandatory && !obj.isCompleted)
                return false;
        }
        return true;
    }

    public Objective GetActiveObjective()
    {
        foreach (var obj in objectives)
        {
            if (obj.isMandatory && !obj.isCompleted)
                return obj;
        }
        return null;
    }

    public string GetActiveObjectiveText()
    {
        var active = GetActiveObjective();
        if (active != null)
            return active.GetDisplayText();

        if (IsLevelComplete())
            return "ALL OBJECTIVES COMPLETE - PROCEED TO EXIT";

        return "SECURE THE AREA";
    }

    void NotifyUpdate(Objective updatedObj, bool isNewCompletion)
    {
        OnObjectiveUpdated?.Invoke(GetActiveObjective(), isNewCompletion);
    }
}
