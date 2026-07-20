using UnityEngine;

/// The Auditor: stands somewhere unreachable, faces the player, and is gone
/// the moment anyone gets close. No health, no combat — only observation.
public sealed class G1GManCameo : MonoBehaviour
{
    public float vanishDistance = 8f;

    Transform player;

    void Update()
    {
        if (!player)
        {
            var p = GameObject.FindWithTag("Player");
            if (!p)
                return;
            player = p.transform;
        }

        Vector3 to = player.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(to),
                90f * Time.deltaTime);

        if (to.magnitude < vanishDistance)
        {
            G1Audio.Play("door_servo", transform.position, 0.25f, 0.5f, 0f);
            gameObject.SetActive(false);           // gone when you look twice
        }
    }
}
