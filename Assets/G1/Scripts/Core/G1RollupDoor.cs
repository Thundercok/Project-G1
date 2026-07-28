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

/// The machinery of a working installation: shutters, boom barriers, lifts that
/// serve more than two floors, and the fabricator that hands out ammunition.
///
/// These live together because they share one idea. Cradle Station is supposed
/// to read as a place that was running until very recently, and the difference
/// between a base and a stage set is whether its equipment does anything when
/// you walk up to it. Each of these is small; what matters is that there are
/// enough of them, in the places a real facility would have put them.
///
/// All of them are driven from the map manifest — the Blender generator that
/// placed the geometry also declared what it is — so a shutter can never end up
/// somewhere there is no shutter-shaped hole in the wall.

// ---------------------------------------------------------------- shutters
/// A roll-up door: the slats climb into the housing above them.
///
/// It moves by translating a parent transform rather than animating slats,
/// which is invisible in play and is the difference between one moving object
/// and thirty.
public sealed class G1RollupDoor : MonoBehaviour, IUsable
{
    public Transform shutter;
    public float lift = 4.6f;          // how far up the slats travel
    public float moveTime = 2.4f;
    public bool locked;
    public string label = "ROLLER SHUTTER";
    public string lockedMessage = "SHUTTER LOCKED";
    public bool autoProximity;
    public float promptRange = 3.6f;

    public bool IsOpen { get; private set; }

    Vector3 closedAt;
    float t;                            // 0 closed, 1 open
    bool moving;
    int dir;

    void Awake()
    {
        if (shutter == null) shutter = transform;
        closedAt = shutter.localPosition;
    }

    void Update()
    {
        if (autoProximity && !locked && !IsOpen && !moving)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null &&
                (p.transform.position - transform.position).sqrMagnitude <
                promptRange * promptRange * 2.2f)
                Open();
        }

        if (!moving) return;
        t = Mathf.Clamp01(t + dir * Time.deltaTime / Mathf.Max(0.05f, moveTime));
        // ease out at the top: a shutter that stops dead reads as a texture
        // sliding rather than a door with weight
        float e = t * t * (3f - 2f * t);
        shutter.localPosition = closedAt + Vector3.up * (lift * e);
        if (t <= 0f || t >= 1f)
        {
            moving = false;
            IsOpen = t >= 1f;
        }
    }

    public void Open() { if (!locked) { dir = 1; moving = true; } }
    public void Close() { dir = -1; moving = true; }
    public void Unlock() { locked = false; }

    public void OnUse(GameObject user)
    {
        if (locked)
        {
            G1Audio.Play("hit_thunk", transform.position, 0.5f, 0.7f);
            return;
        }
        G1Audio.Play("door_servo", transform.position, 0.85f, 0.5f);
        if (IsOpen || (moving && dir > 0)) Close();
        else Open();
    }
}
