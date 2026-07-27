using System.Collections.Generic;
using UnityEngine;

/// A drivable truck. Press E to get in, WASD to drive, E to get out.
///
/// An 800m map is a lot of walking, and sprint only buys seven seconds of it.
/// This is the other answer: about three times running speed, at the cost of
/// being a large loud target that cannot use cover.
///
/// The drive is deliberately arcade rather than WheelColliders. Wheel physics
/// needs per-wheel friction curves, suspension tuning and a centre of mass
/// that all have to be right together or the vehicle flips on the first kerb —
/// and none of that is visible to a player crossing a battlefield. Forces
/// along the body plus a ground raycast gives something predictable to drive
/// on terrain this rough.
[RequireComponent(typeof(Rigidbody))]
public sealed class G1Vehicle : MonoBehaviour, IUsable
{
    [Header("Handling")]
    public float maxSpeed = 26f;          // ~94 km/h, about 3x running
    public float reverseSpeed = 9f;
    public float accel = 16f;
    public float turnRate = 78f;          // deg/s at speed
    public float grip = 6f;               // how hard sideways slide is killed
    public float gravity = 22f;

    [Header("Seat")]
    public Transform seat;
    public float exitOffset = 2.4f;
    [Tooltip("Walk this close and E gets you in, without having to aim at it.")]
    public float mountRange = 5f;

    [Header("Feel")]
    public Light[] headlights;
    public float engineVolume = 0.35f;

    /// Every truck in the scene, so the HUD can point at the nearest one.
    public static readonly List<G1Vehicle> All = new List<G1Vehicle>();

    Rigidbody rb;
    AudioSource engine;
    GameObject driver;
    PlayerMovement move;
    PlayerUse use;
    CharacterController cc;
    bool grounded;

    public bool Occupied => driver != null;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // ------------------------------------------------------------- getting in
    // Aiming at a five-metre truck to press E is needless precision, so
    // proximity is enough — but only when the player is not already aiming at
    // something else, or standing by a truck would steal every E press meant
    // for the person next to it.
    static Transform playerT;
    static PlayerUse playerUse;

    void FindPlayer()
    {
        if (playerT != null) return;
        var p = GameObject.FindWithTag("Player");
        if (p == null) return;
        playerT = p.transform;
        playerUse = p.GetComponentInChildren<PlayerUse>(true);
    }

    void LateUpdate()
    {
        if (driver != null) return;
        FindPlayer();
        if (playerT == null || !InRange) return;
        if (!Input.GetKeyDown(KeyCode.E)) return;

        var aimed = playerUse != null && playerUse.enabled ? playerUse.FindUsable() : null;
        if (aimed != null && !ReferenceEquals(aimed, this)) return;   // they meant that
        Mount(playerT.gameObject);
    }

    public bool InRange => playerT != null && !Occupied &&
        (playerT.position - transform.position).sqrMagnitude < mountRange * mountRange;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;            // driven by hand, not by the solver
        rb.useGravity = false;

        engine = gameObject.AddComponent<AudioSource>();
        engine.clip = Resources.Load<AudioClip>("Audio/ambient_industrial");
        engine.loop = true;
        engine.spatialBlend = 1f;
        engine.minDistance = 4f;
        engine.maxDistance = 60f;
        engine.rolloffMode = AudioRolloffMode.Linear;
        engine.volume = 0f;
        if (engine.clip) engine.Play();
    }

    public void OnUse(GameObject user)
    {
        if (driver == null) Mount(user);
        else if (driver == user) Dismount();
    }

    void Mount(GameObject user)
    {
        driver = user;
        move = user.GetComponent<PlayerMovement>();
        cc = user.GetComponent<CharacterController>();
        use = user.GetComponentInChildren<PlayerUse>(true);

        // the controller has to go before reparenting or it fights the seat
        if (cc) cc.enabled = false;
        if (move) move.enabled = false;
        if (use) use.enabled = false;     // E belongs to this script while seated

        user.transform.SetParent(seat != null ? seat : transform, false);
        user.transform.localPosition = Vector3.zero;
        user.transform.localRotation = Quaternion.identity;

        foreach (var l in headlights) if (l) l.enabled = true;
        G1Audio.Play2D("door_servo", 0.6f, 0.9f);
    }

    void Dismount()
    {
        var user = driver;
        driver = null;

        // step out to the side, and only somewhere there is room to stand
        Vector3 spot = transform.position + transform.right * -exitOffset + Vector3.up * 0.4f;
        if (Physics.CheckCapsule(spot + Vector3.up * 0.4f, spot + Vector3.up * 1.6f, 0.4f,
                                 ~0, QueryTriggerInteraction.Ignore))
            spot = transform.position + transform.right * exitOffset + Vector3.up * 0.4f;

        user.transform.SetParent(null, true);
        user.transform.position = spot;
        user.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        if (cc) cc.enabled = true;
        if (move) move.enabled = true;
        if (use) use.enabled = true;

        foreach (var l in headlights) if (l) l.enabled = false;
        engine.volume = 0f;
        G1Audio.Play2D("door_servo", 0.6f, 0.7f);
    }

    Vector3 velocity;

    void Update()
    {
        if (driver == null) return;
        if (Input.GetKeyDown(KeyCode.E)) { Dismount(); return; }

        float dt = Time.deltaTime;
        float throttle = Input.GetAxisRaw("Vertical");
        float steer = Input.GetAxisRaw("Horizontal");

        Vector3 flat = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float fwd = Vector3.Dot(flat, transform.forward);

        // steering only bites when the wheels are turning, and reverses when
        // reversing — a car that pivots on the spot reads as a turret
        if (Mathf.Abs(fwd) > 0.4f)
        {
            float authority = Mathf.Clamp01(Mathf.Abs(fwd) / 6f) * Mathf.Sign(fwd);
            transform.Rotate(0f, steer * turnRate * authority * dt, 0f, Space.World);
        }

        float cap = throttle >= 0f ? maxSpeed : reverseSpeed;
        if (Mathf.Abs(throttle) > 0.01f && grounded)
            velocity += transform.forward * (throttle * accel * dt);
        else
            velocity = Vector3.MoveTowards(velocity, new Vector3(0f, velocity.y, 0f), 8f * dt);

        // kill sideways slide so it corners instead of drifting like soap
        Vector3 lateral = Vector3.Project(velocity, transform.right);
        velocity -= lateral * Mathf.Clamp01(grip * dt);

        flat = Vector3.ProjectOnPlane(velocity, Vector3.up);
        if (flat.magnitude > cap) { flat = flat.normalized * cap; velocity = new Vector3(flat.x, velocity.y, flat.z); }

        // ground follow: a ray from above the axle keeps it on slopes and
        // stops it burrowing into the ramps and berms all over this map
        grounded = false;
        if (Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.down,
                            out RaycastHit hit, 3.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            grounded = true;
            float target = hit.point.y + 0.55f;
            var p = transform.position;
            p.y = Mathf.Lerp(p.y, target, 10f * dt);
            transform.position = p;
            velocity.y = 0f;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(transform.forward, hit.normal), hit.normal),
                6f * dt);
        }
        else velocity.y -= gravity * dt;

        transform.position += velocity * dt;

        float speed01 = Mathf.Clamp01(flat.magnitude / maxSpeed);
        engine.volume = engineVolume * (0.35f + 0.65f * speed01);
        engine.pitch = 0.7f + 1.1f * speed01;
    }
}
