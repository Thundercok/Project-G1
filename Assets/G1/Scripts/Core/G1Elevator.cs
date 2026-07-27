using UnityEngine;

/// A two-stop platform lift. Stand on it, press E, ride.
///
/// The Sprawl grew a lot of verticality — tower decks, warehouse roofs,
/// catwalks — and reaching most of it means finding the one ramp that serves
/// it. A lift is the other half of that vocabulary: it makes a roof a
/// destination rather than a puzzle, and it is the only way up the command
/// tower, where the Auditor has been standing unreachable since the map was
/// built.
///
/// The rider is moved by an explicit delta rather than by parenting. A
/// CharacterController parented to a moving transform fights its own
/// depenetration and jitters; feeding it the platform's movement through
/// Move() is what actually keeps a player planted on a lift.
public sealed class G1Elevator : MonoBehaviour, IUsable
{
    [Header("Travel")]
    public Transform platform;
    public float bottomY;
    public float topY;
    public float speed = 4f;
    public float holdAtEnds = 0.6f;

    [Header("Readout")]
    public Light statusLight;
    public string label = "LIFT";

    bool goingUp;
    bool moving;
    float restUntil;

    void Start()
    {
        if (platform == null) platform = transform;
        if (bottomY == 0f && topY == 0f)
        {
            bottomY = platform.position.y;
            topY = bottomY + 10f;
        }
        Relamp();
    }

    public void OnUse(GameObject user)
    {
        if (moving || Time.time < restUntil) return;
        goingUp = platform.position.y < (bottomY + topY) * 0.5f;
        moving = true;
        G1Audio.Play("door_servo", platform.position, 0.7f, 0.85f);
        Relamp();
    }

    void Update()
    {
        if (!moving) return;

        float target = goingUp ? topY : bottomY;
        Vector3 before = platform.position;
        var p = before;
        p.y = Mathf.MoveTowards(p.y, target, speed * Time.deltaTime);
        platform.position = p;
        float delta = p.y - before.y;

        // carry whoever is standing on the deck
        if (Mathf.Abs(delta) > 0.0001f)
        {
            var half = platform.lossyScale * 0.5f;
            var hits = Physics.OverlapBox(
                p + Vector3.up * (half.y + 1.0f),
                new Vector3(half.x, 1.0f, half.z), platform.rotation,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var cc = h.GetComponentInParent<CharacterController>();
                if (cc != null && cc.enabled) { cc.Move(Vector3.up * delta); continue; }
                var agent = h.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.transform.position += Vector3.up * delta;
            }
        }

        if (Mathf.Approximately(p.y, target))
        {
            moving = false;
            restUntil = Time.time + holdAtEnds;
            G1Audio.Play("hit_thunk", platform.position, 0.5f, 0.7f);
            Relamp();
        }
    }

    void Relamp()
    {
        if (statusLight == null) return;
        statusLight.color = moving ? new Color(1f, 0.75f, 0.1f)
            : platform.position.y > (bottomY + topY) * 0.5f
                ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.4f, 0.6f, 1f);
    }
}
