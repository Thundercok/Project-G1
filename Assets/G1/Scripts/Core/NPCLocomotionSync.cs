using UnityEngine;
using UnityEngine.AI;

/// Kills foot-skating: scales walk-animation playback to the character's real
/// speed. The shared Walk cycle is authored for a ~0.75 m/s stride, but agents
/// move at up to 2.2 m/s — without this, feet slide across the ground.
[RequireComponent(typeof(Animator))]
public sealed class NPCLocomotionSync : MonoBehaviour
{
    public float strideSpeed = 0.75f;    // m/s the Walk cycle was authored at
    public float minRate = 0.7f;
    public float maxRate = 2.6f;

    Animator anim;
    NavMeshAgent agent;
    Vector3 lastPos;

    void Awake()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        lastPos = transform.position;
    }

    void Update()
    {
        float speed;
        if (agent && agent.enabled && agent.isOnNavMesh)
        {
            speed = agent.velocity.magnitude;
        }
        else
        {
            Vector3 d = transform.position - lastPos;
            d.y = 0f;
            speed = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
        }
        lastPos = transform.position;

        anim.speed = anim.GetCurrentAnimatorStateInfo(0).IsName("Walk")
            ? Mathf.Clamp(speed / strideSpeed, minRate, maxRate)
            : 1f;
    }
}
