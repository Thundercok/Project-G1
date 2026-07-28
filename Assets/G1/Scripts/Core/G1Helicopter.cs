using UnityEngine;

/// A flyable helicopter. E to board, then:
///   WASD     where you want to go — the cyclic, tilting the aircraft
///   Space    climb        Ctrl/C   descend
///   Mouse    look, and the aircraft turns to follow your gaze
///   Shift    throttle boost
///   E        land and get out (only when low and slow)
///
/// The first control scheme here was the one a real pilot uses: collective on
/// W/S, pedals on A/D, cyclic on the arrow keys. It is correct and it is
/// horrible, because a player pressing W expects to *go forward*, not to rise.
/// WASD is now the cyclic — the thing that moves you — and yaw comes off the
/// mouse, so there is no third hand needed and no key to learn.
///
/// Built on the same arcade footing as G1Vehicle and for the same reason: real
/// rotor physics needs a flight model nobody crossing a battlefield will ever
/// see. What matters is that it *feels* like a helicopter, and that comes from
/// two things a car does not do — you tilt in the direction you travel, and you
/// keep travelling after you stop asking to, because nothing but drag stops
/// three tonnes moving through air.
///
/// The other half is that it must not become a way to leave the level. Altitude
/// is capped, and the map's own perimeter is enforced, so the Sprawl stays the
/// place the game happens in.
[RequireComponent(typeof(Rigidbody))]
public sealed class G1Helicopter : MonoBehaviour, IUsable
{
    [Header("Flight")]
    public float maxSpeed = 34f;
    public float climbRate = 11f;
    public float yawRate = 62f;
    public float tiltMax = 22f;          // degrees of cyclic
    public float tiltResponse = 2.6f;
    public float drag = 0.7f;            // air is the only brake
    public float ceiling = 120f;
    public float boost = 1.6f;

    [Header("Bounds")]
    public float mapHalf = 395f;         // do not let it leave the Sprawl

    [Header("Parts")]
    public Transform mainRotor;
    public Transform tailRotor;
    public Transform seat;
    public Light[] navLights;

    public static readonly System.Collections.Generic.List<G1Helicopter> All =
        new System.Collections.Generic.List<G1Helicopter>();

    GameObject driver;
    PlayerMovement move;
    PlayerUse use;
    CharacterController cc;
    BoxCollider box;
    AudioSource rotorSfx;
    Vector3 velocity;
    float spin, pitch, roll, toggleLockUntil, groundY;

    static Transform playerT;
    static PlayerUse playerUse;

    public bool Occupied => driver != null;
    public bool InRange => playerT != null && !Occupied &&
        (playerT.position - transform.position).sqrMagnitude < 42f;
    public bool Airborne => transform.position.y > 2.2f;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        box = GetComponent<BoxCollider>();

        rotorSfx = gameObject.AddComponent<AudioSource>();
        rotorSfx.clip = Resources.Load<AudioClip>("Audio/ambient_industrial");
        rotorSfx.loop = true;
        rotorSfx.spatialBlend = 1f;
        rotorSfx.minDistance = 8f;
        rotorSfx.maxDistance = 140f;
        rotorSfx.rolloffMode = AudioRolloffMode.Linear;
        rotorSfx.volume = 0f;
        if (rotorSfx.clip) rotorSfx.Play();
    }

    public void OnUse(GameObject user)
    {
        if (driver == null) Board(user);
        else if (driver == user) TryLeave();
    }

    void LateUpdate()
    {
        if (driver != null) return;
        if (playerT == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            playerT = p.transform;
            playerUse = p.GetComponentInChildren<PlayerUse>(true);
        }
        if (!InRange || !Input.GetKeyDown(KeyCode.E) || Time.time < toggleLockUntil) return;
        var aimed = playerUse != null && playerUse.enabled ? playerUse.FindUsable() : null;
        if (aimed != null && !ReferenceEquals(aimed, this)) return;
        Board(playerT.gameObject);
    }

    void Board(GameObject user)
    {
        toggleLockUntil = Time.time + 0.35f;
        driver = user;
        move = user.GetComponent<PlayerMovement>();
        cc = user.GetComponent<CharacterController>();
        use = user.GetComponentInChildren<PlayerUse>(true);
        if (cc) cc.enabled = false;
        if (move) move.enabled = false;
        if (use) use.enabled = false;
        user.transform.SetParent(seat != null ? seat : transform, false);
        user.transform.localPosition = Vector3.zero;
        user.transform.localRotation = Quaternion.identity;
        foreach (var l in navLights) if (l) l.enabled = true;
        boardedAt = Time.time;
        G1Audio.Play2D("door_servo", 0.6f, 1.1f);
    }

    /// Getting out mid-air would be a fall, not a dismount, so it is refused —
    /// and refused audibly, because a control that silently does nothing reads
    /// as broken rather than as a rule.
    void TryLeave()
    {
        if (Airborne || velocity.magnitude > 3f)
        {
            G1Audio.Play2D("hit_thunk", 0.5f, 0.6f);
            return;
        }
        toggleLockUntil = Time.time + 0.35f;
        var user = driver;
        driver = null;
        user.transform.SetParent(null, true);
        user.transform.position = transform.position + transform.right * -3.2f + Vector3.up * 0.4f;
        user.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (cc) cc.enabled = true;
        if (move) move.enabled = true;
        if (use) use.enabled = true;
        foreach (var l in navLights) if (l) l.enabled = false;
        G1Audio.Play2D("door_servo", 0.6f, 0.8f);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // rotors turn whenever anyone is aboard, and spin down when nobody is
        float rpm = driver != null ? 1f : Mathf.Max(0f, 1f - Time.time * 0f);
        spin += (driver != null ? 1800f : 0f) * dt;
        if (mainRotor) mainRotor.localRotation = Quaternion.Euler(0f, spin, 0f);
        if (tailRotor) tailRotor.localRotation = Quaternion.Euler(spin * 1.4f, 0f, 0f);

        if (driver == null)
        {
            rotorSfx.volume = Mathf.MoveTowards(rotorSfx.volume, 0f, dt);
            return;
        }
        if (Input.GetKeyDown(KeyCode.E) && Time.time >= toggleLockUntil) { TryLeave(); return; }

        // ---- controls: WASD is where you want to go
        float cycPitch = Input.GetAxisRaw("Vertical");
        float cycRoll = Input.GetAxisRaw("Horizontal");
        float collective = (Input.GetKey(KeyCode.Space) ? 1f : 0f)
                         - (Input.GetKey(KeyCode.LeftControl) ||
                            Input.GetKey(KeyCode.C) ? 1f : 0f);
        float thr = Input.GetKey(KeyCode.LeftShift) ? boost : 1f;

        // Yaw follows the mouse. The player sits parented to the seat, so the
        // angle between where they are looking and where the nose points is
        // just their local Y rotation. Turn the aircraft toward it and take
        // the same amount back off the player, so the aircraft comes round
        // under a view that stays where the player put it — turn without
        // countering it and the nose chases the gaze forever.
        if (driver != null)
        {
            float offset = Mathf.DeltaAngle(0f, driver.transform.localEulerAngles.y);
            if (Mathf.Abs(offset) > 1.5f)
            {
                float step = Mathf.Clamp(offset, -yawRate * dt, yawRate * dt);
                transform.Rotate(0f, step, 0f, Space.World);
                var le = driver.transform.localEulerAngles;
                le.y -= step;
                driver.transform.localEulerAngles = le;
            }
        }

        // A helicopter goes where it leans. Tilting the body and then pushing
        // along that tilt is the whole illusion — it is why this feels like
        // flying and a car with a Y axis does not.
        pitch = Mathf.Lerp(pitch, cycPitch * tiltMax, tiltResponse * dt);
        roll = Mathf.Lerp(roll, -cycRoll * tiltMax, tiltResponse * dt);
        transform.localRotation = Quaternion.Euler(
            -pitch, transform.eulerAngles.y, roll);

        Vector3 lean = transform.forward * (pitch / tiltMax)
                     + transform.right * (-roll / tiltMax);
        velocity += lean * (18f * thr * dt);
        velocity.y += collective * climbRate * thr * dt;

        // drag, not brakes: nothing stops three tonnes in air but the air
        velocity -= velocity * Mathf.Clamp01(drag * dt);
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        if (flat.magnitude > maxSpeed * thr)
        {
            flat = flat.normalized * maxSpeed * thr;
            velocity = new Vector3(flat.x, velocity.y, flat.z);
        }

        Vector3 next = transform.position + velocity * dt;

        // ---- the ground, the ceiling and the fence
        groundY = 0f;
        if (Physics.Raycast(next + Vector3.up * 200f, Vector3.down,
                            out RaycastHit g, 400f, ~0, QueryTriggerInteraction.Ignore))
            groundY = g.point.y;
        float floor = groundY + 1.6f;
        if (next.y <= floor)
        {
            next.y = floor;
            if (velocity.y < 0f) velocity.y = 0f;
            velocity.x *= 0.86f; velocity.z *= 0.86f;      // skids bite
        }
        next.y = Mathf.Min(next.y, groundY + ceiling);
        next.x = Mathf.Clamp(next.x, -mapHalf, mapHalf);
        next.z = Mathf.Clamp(next.z, -mapHalf, mapHalf);
        transform.position = next;

        float load = Mathf.Clamp01(flat.magnitude / maxSpeed);
        rotorSfx.volume = Mathf.MoveTowards(rotorSfx.volume, 0.42f, 2f * dt);
        rotorSfx.pitch = 1.1f + 0.5f * load;

        altitude = transform.position.y - groundY;
        speedKph = flat.magnitude * 3.6f;
    }

    float altitude, speedKph;

    /// A flight model the player has to guess at is not a flight model. The
    /// keys stay on screen for the first stretch of every flight, and the
    /// instruments stay up the whole time — altitude especially, because
    /// "can I get out here" is the one question the controls refuse to answer.
    void OnGUI()
    {
        if (driver == null) return;

        var font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
        var s = new GUIStyle(GUI.skin.label) { fontSize = 15, font = font };

        // instruments, bottom centre
        s.alignment = TextAnchor.MiddleCenter;
        string readout = $"ALT {altitude:0} m      SPD {speedKph:0} km/h" +
                         (Airborne ? "" : "      [E] DISEMBARK");
        s.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(2, Screen.height - 118, Screen.width, 24), readout, s);
        s.normal.textColor = Airborne
            ? new Color(0.55f, 0.9f, 1f, 0.95f)
            : new Color(0.55f, 0.95f, 0.55f, 0.95f);
        GUI.Label(new Rect(0, Screen.height - 120, Screen.width, 24), readout, s);

        // the keys, only while they are still new
        if (Time.time - boardedAt > 12f) return;
        s.fontSize = 14;
        s.alignment = TextAnchor.UpperLeft;
        s.normal.textColor = new Color(0.85f, 0.85f, 0.8f,
                                       Mathf.Clamp01(12f - (Time.time - boardedAt)));
        GUI.Label(new Rect(34, Screen.height / 2f - 40, 320, 120),
                  "WASD   fly\nSPACE  climb\nCTRL   descend\nMOUSE  turn\n" +
                  "SHIFT  boost\nE      land, then leave", s);
    }

    float boardedAt;
}
