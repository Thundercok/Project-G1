using UnityEngine;

/// Minimal NPC driver: no waypoints = play Idle; waypoints = patrol with Walk.
[RequireComponent(typeof(Animator))]
public class NPCController : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 0.75f;          // matched to the 1s walk cycle stride
    public float turnSpeed = 220f;

    Animator anim;
    int index;
    bool walking;

    void Start()
    {
        anim = GetComponent<Animator>();
        walking = waypoints != null && waypoints.Length > 1;
        anim.CrossFade(walking ? "Walk" : "Idle", 0f);
    }

    void Update()
    {
        if (!walking)
            return;
        Vector3 target = waypoints[index].position;
        target.y = transform.position.y;
        Vector3 to = target - transform.position;
        if (to.magnitude < 0.2f)
        {
            index = (index + 1) % waypoints.Length;
            return;
        }
        Quaternion look = Quaternion.LookRotation(to);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, look, turnSpeed * Time.deltaTime);
        transform.position += transform.forward * speed * Time.deltaTime;
    }
}
