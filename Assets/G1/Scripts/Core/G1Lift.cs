using UnityEngine;

// Split out of G1BaseEquipment.cs so the class name matches the file name.
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

// ------------------------------------------------------------------- lifts
/// A lift that serves an arbitrary list of floors.
///
/// G1Elevator has a bottom and a top, which is the whole vocabulary a two-storey
/// bunker needs. A five-storey headquarters needs to be able to stop at the
/// third floor, and "call it, ride it, get off" is most of what makes a tall
/// building feel tall rather than feel like a roof with stairs.
public sealed class G1Lift : MonoBehaviour, IUsable
{
    public Transform car;
    public float[] stops = new float[0];   // world Y of each floor, ascending
    public float speed = 3.2f;
    public float doorDwell = 1.1f;
    public string label = "LIFT";

    int at;                 // index of the floor we are parked at
    int target;
    float dwell;

    void Awake()
    {
        if (car == null) car = transform;
        if (stops.Length == 0) stops = new[] { car.position.y };
        at = target = 0;
    }

    void Update()
    {
        if (stops.Length == 0) return;
        float want = stops[Mathf.Clamp(target, 0, stops.Length - 1)];
        var p = car.position;
        if (Mathf.Abs(p.y - want) > 0.02f)
        {
            // Ride the passenger up with the floor. Parenting them would fight
            // the CharacterController; moving them by the same delta does not.
            float step = Mathf.MoveTowards(p.y, want, speed * Time.deltaTime) - p.y;
            car.position = new Vector3(p.x, p.y + step, p.z);
            CarryRiders(step);
            dwell = doorDwell;
            return;
        }
        at = target;
        if (dwell > 0f) dwell -= Time.deltaTime;
    }

    void CarryRiders(float dy)
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        Vector3 d = player.transform.position - car.position;
        if (Mathf.Abs(d.x) > 1.6f || Mathf.Abs(d.z) > 1.6f) return;
        if (d.y < -0.2f || d.y > 2.6f) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.Move(new Vector3(0f, dy, 0f));
        else player.transform.position += new Vector3(0f, dy, 0f);
    }

    public void OnUse(GameObject user)
    {
        if (stops.Length < 2) return;
        if (dwell > 0f) return;
        // one button, cycling upward and turning round at the top — the same
        // affordance the bunker lift already taught the player
        target = at + 1 >= stops.Length ? 0 : at + 1;
        G1Audio.Play("door_servo", transform.position, 0.8f, 0.5f);
        Debug.Log($"{label}: floor {target + 1}/{stops.Length}");
    }
}
