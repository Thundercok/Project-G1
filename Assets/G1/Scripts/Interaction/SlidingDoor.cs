using System.Collections;
using UnityEngine;

/// Press E on it: slides up, press again: slides back down.
public class SlidingDoor : MonoBehaviour, IUsable
{
    public float travel = 2.1f;
    public float moveTime = 1.1f;

    public static float GlobalDoorOpeningGraceTime { get; private set; }

    Vector3 closedPos;
    bool open;
    bool moving;

    void Start()
    {
        closedPos = transform.position;
    }

    public void OnUse(GameObject user)
    {
        if (!moving)
        {
            GlobalDoorOpeningGraceTime = Time.time + 0.65f;
            G1Audio.Play("door_servo", transform.position, 0.7f);
            StartCoroutine(Slide(!open));
        }
    }

    /// Open-only (idempotent) — used by proximity auto-open so the door never
    /// toggles shut on the player mid-fight.
    public void Open()
    {
        if (!moving && !open)
        {
            GlobalDoorOpeningGraceTime = Time.time + 0.65f;
            G1Audio.Play("door_servo", transform.position, 0.7f);
            StartCoroutine(Slide(true));
        }
    }

    IEnumerator Slide(bool opening)
    {
        moving = true;
        Vector3 from = transform.position;
        Vector3 to = closedPos + (opening ? Vector3.up * travel : Vector3.zero);
        float t = 0f;
        while (t < moveTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / moveTime);
            transform.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        transform.position = to;
        open = opening;
        moving = false;
    }
}
