using System.Collections.Generic;
using UnityEngine;

/// Third-person chase camera, for while you are driving.
///
/// Driving in first person is the wrong camera for this game. The truck is
/// 6.9 m long and you sit at the front of it, so from the cab you cannot see
/// the thing you are actually steering — you find out you clipped a T-wall by
/// stopping. Worse, ramming is a weapon here, and a weapon you cannot see
/// connect is not a weapon you will use on purpose.
///
/// So the camera leaves the driver's head, swings out behind the vehicle and
/// orbits on the mouse. The player object stays bolted to the seat exactly as
/// before: nothing about the driving, the collision sweep or the dismount
/// changes, only where the picture is taken from.
///
/// Three things this has to get right, in order of how badly they read when
/// wrong: the camera must never end up inside a wall, it must not fight
/// MouseLook for the same transform, and the weapon viewmodel — which lives
/// under the camera and is drawn a hand's width in front of it — has to go
/// away, or you drive around looking at a floating crowbar.
public sealed class G1DriveCamera : MonoBehaviour
{
    [Tooltip("What we are orbiting.")]
    public Transform target;

    public float distance = 8.5f;
    public float height = 3.4f;
    public float lookHeight = 1.6f;
    public float sensitivity = 2.4f;
    public float minPitch = -8f;
    public float maxPitch = 62f;
    public float follow = 9f;          // how fast the rig catches up
    public float turnFollow = 3.2f;    // how fast it swings round behind a turn
    public float recentre = 2.5f;      // how fast a mouse look eases back
    public float recentreDelay = 1.2f; // how long it waits before it does

    // Two separate angles, and conflating them was the bug.
    //
    // `followYaw` is where the vehicle is pointing; `yawOffset` is how far the
    // player has looked away from that. The first version had one free-running
    // `yaw` seeded from the vehicle's heading at the moment of boarding and
    // moved only by the mouse — so it was correct exactly as long as the truck
    // drove in a straight line, and the instant you turned, the camera stayed
    // locked to the old compass bearing. Turn ninety degrees and you were
    // filming the side of your own truck; turn round and you were in front of
    // it, looking the wrong way.
    float followYaw, yawOffset, pitch = 14f;
    float lastMouse = -99f;
    Transform hadParent;
    Vector3 hadLocalPos;
    Quaternion hadLocalRot;
    MouseLook look;
    CameraEffects fx;
    readonly List<Renderer> hidden = new List<Renderer>();
    Vector3 smoothed;
    bool ready;

    /// Take the camera off the driver and put it behind the vehicle.
    ///
    /// Returns the component so the caller can hand it back later; there is
    /// deliberately no singleton, because two vehicles being driven at once is
    /// not a thing that can happen and pretending otherwise costs a static.
    public static G1DriveCamera Engage(Transform vehicle, GameObject driver)
    {
        if (vehicle == null || driver == null) return null;
        var cam = driver.GetComponentInChildren<Camera>(true);
        if (cam == null) return null;

        var c = cam.gameObject.GetComponent<G1DriveCamera>();
        if (c == null) c = cam.gameObject.AddComponent<G1DriveCamera>();
        c.Begin(vehicle);
        return c;
    }

    void Begin(Transform vehicle)
    {
        target = vehicle;

        hadParent = transform.parent;
        hadLocalPos = transform.localPosition;
        hadLocalRot = transform.localRotation;

        // MouseLook writes localRotation every Update and would spend the whole
        // drive undoing this. Its yaw also turns the *body*, which is now bolted
        // to the seat, so leaving it on would steer the player inside the truck.
        look = GetComponent<MouseLook>();
        if (look != null)
        {
            followYaw = vehicle.eulerAngles.y;
            yawOffset = 0f;
            look.enabled = false;
        }

        // CameraEffects writes localPosition and localRotation in its own
        // LateUpdate — head bob, weapon punch, screen shake, all of it measured
        // from a first-person rest pose. Detached from the player, localPosition
        // *is* world position, so leaving it on means it and this component take
        // turns owning the camera and the picture strobes between the cab and
        // the chase position. Component execution order would decide which one
        // wins, which is not a thing to leave to chance.
        fx = GetComponent<CameraEffects>();
        if (fx != null) fx.enabled = false;

        // Out of the hierarchy entirely. Staying a child of the seat and
        // overwriting the world transform every frame works, but any jitter in
        // the parent shows up as jitter in the shot, and the vehicle is being
        // moved by a swept translation rather than by physics interpolation.
        transform.SetParent(null, true);

        // the viewmodel: everything drawn under the camera is a first-person
        // prop and has no business being in a third-person shot
        hidden.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled) continue;
            r.enabled = false;
            hidden.Add(r);
        }

        smoothed = Desired(out _);
        ready = true;
    }

    /// Put everything back exactly as it was.
    public void Release()
    {
        ready = false;
        target = null;

        transform.SetParent(hadParent, false);
        transform.localPosition = hadLocalPos;
        transform.localRotation = hadLocalRot;

        foreach (var r in hidden) if (r != null) r.enabled = true;
        hidden.Clear();

        if (fx != null) { fx.enabled = true; fx = null; }
        if (look != null) look.enabled = true;
    }

    void LateUpdate()
    {
        if (!ready || target == null) return;

        // LateUpdate, because the vehicle moves in Update — reading its
        // position any earlier frames it where it was last frame, which is the
        // classic one-frame lag that reads as the camera swimming.
        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");
        if (Mathf.Abs(mx) > 0.001f || Mathf.Abs(my) > 0.001f)
            lastMouse = Time.unscaledTime;

        yawOffset += mx * sensitivity;
        yawOffset = Mathf.Repeat(yawOffset + 180f, 360f) - 180f;
        pitch = Mathf.Clamp(pitch - my * sensitivity, minPitch, maxPitch);

        // Swing round behind the vehicle. Lerping the *angle* rather than
        // snapping is what makes a turn read as the camera trailing the truck
        // through it instead of the truck rotating under a fixed camera.
        followYaw = Mathf.LerpAngle(followYaw, target.eulerAngles.y,
                                    1f - Mathf.Exp(-turnFollow * Time.deltaTime));

        // and ease the player's own look back to centre once they stop, so
        // they are never left driving sideways because they glanced at
        // something a minute ago
        if (Time.unscaledTime - lastMouse > recentreDelay)
            yawOffset = Mathf.MoveTowards(yawOffset, 0f,
                                          recentre * 30f * Time.deltaTime);

        Vector3 want = Desired(out Vector3 look_at);
        smoothed = Vector3.Lerp(smoothed, want, 1f - Mathf.Exp(-follow * Time.deltaTime));

        // Never inside geometry. Sweeping a sphere from the pivot outward and
        // stopping at the first thing hit is what keeps the camera out of walls
        // in tight yards; a plain raycast slips through the corner of a T-wall
        // and puts the camera inside it.
        Vector3 pivot = target.position + Vector3.up * lookHeight;
        Vector3 dir = smoothed - pivot;
        float want_d = dir.magnitude;
        if (want_d > 0.01f)
        {
            dir /= want_d;
            if (Physics.SphereCast(pivot, 0.35f, dir, out RaycastHit hit, want_d,
                                   ~0, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(target))
                smoothed = pivot + dir * Mathf.Max(1.2f, hit.distance - 0.25f);
        }

        transform.position = smoothed;
        transform.rotation = Quaternion.LookRotation(look_at - smoothed, Vector3.up);
    }

    Vector3 Desired(out Vector3 look_at)
    {
        Vector3 pivot = target.position + Vector3.up * lookHeight;
        float yaw = followYaw + yawOffset;

        // Look along the camera's own bearing rather than along the vehicle's.
        // With the two split, aiming at `target.forward` would drag the framing
        // off centre the moment the player looked to one side — the truck would
        // slide to the edge of the shot every time they checked their flank.
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        look_at = pivot + (Quaternion.Euler(0f, yaw, 0f) * Vector3.forward) * 4.0f;
        return pivot + orbit * new Vector3(0f, height, -distance);
    }
}
