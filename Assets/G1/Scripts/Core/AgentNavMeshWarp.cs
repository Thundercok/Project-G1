using UnityEngine;
using UnityEngine.AI;

/// Spawn safety for NavMeshAgents: if the agent starts off the NavMesh
/// (seeded scatter can land inside props), warp it to the nearest point on
/// the mesh; if none is within reach, disable the agent instead of letting
/// it spam "SetDestination can only be called on an active agent" errors.
public sealed class AgentNavMeshWarp : MonoBehaviour
{
    public float searchRadius = 6f;

    void Start()
    {
        var agent = GetComponent<NavMeshAgent>();
        if (!agent || !agent.enabled || agent.isOnNavMesh)
            return;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit,
                                   searchRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else
        {
            Debug.LogWarning($"{name}: no NavMesh within {searchRadius}m — agent disabled");
            agent.enabled = false;
        }
    }
}
