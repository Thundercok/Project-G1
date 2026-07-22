using UnityEngine;

/// Attach to any object in the scene (Terminal, Exit, Boss, Door) to mark it as an active objective target.
/// PlayerHUD reads active waypoints and renders an on-screen target icon + distance indicator.
public class G1Waypoint : MonoBehaviour
{
    public string objectiveId;
    public string label = "OBJECTIVE";
    public bool isActive = true;
    public Vector3 offset = Vector3.up * 1.2f;

    void OnEnable()
    {
        G1ObjectiveManager.RegisterWaypoint(this);
    }

    void OnDisable()
    {
        G1ObjectiveManager.UnregisterWaypoint(this);
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position + offset;
    }
}
