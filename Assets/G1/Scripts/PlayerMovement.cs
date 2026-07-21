using UnityEngine;

/// Half-Life 1 style movement on a CharacterController:
/// Quake-lineage ground/air acceleration, friction, and air-strafe
/// (bunnyhop-friendly: hold jump to keep hopping, air control preserved).
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speeds (m/s; HL1 units * 0.0254)")]
    public float maxSpeed = 8.1f;        // ~320 ups
    public float stopSpeed = 2.5f;       // ~100 ups
    public float jumpSpeed = 6.8f;       // ~45 HU jump apex

    [Header("Acceleration")]
    public float accelerate = 10f;       // sv_accelerate
    public float airAccelerate = 10f;    // sv_airaccelerate
    // HL1's 30-ups air wish clamp. Without it, airborne movement accelerates
    // at full ground strength with zero friction — combined with auto-bhop the
    // player is permanently on ice and WASD feels uncontrollable. Strafe-
    // steering and bhop speed gain still work exactly like GoldSrc with the
    // cap in place; that IS the skill expression.
    public float airWishCap = 0.76f;     // 30 ups
    public float friction = 4f;          // sv_friction
    public float gravity = 20.3f;        // 800 ups^2

    CharacterController cc;
    Vector3 velocity;
    bool wasGrounded;
    float coyoteTimer;              // grace period after leaving ground
    const float CoyoteWindow = 0.12f;

    [Header("Crouch Settings")]
    public float crouchHeight = 1.0f;
    public float crouchSpeedMult = 0.5f;
    public float crouchCameraY = 0.85f;

    [Header("God Mode / Fly")]
    public bool isFlying = false;
    public float flySpeedMult = 1.6f;
    public float flySprintMult = 2.0f;

    private float defaultHeight = 1.8f;
    private Vector3 defaultCameraLocalPos = new Vector3(0f, 1.62f, 0f);
    private bool isCrouching = false;
    private Transform cameraTransform;
    private HealthSystem health;

    public Vector3 Velocity => velocity;
    public bool Grounded => wasGrounded;
    public bool IsCrouching => isCrouching;
    public bool IsFlying => isFlying;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void Start()
    {
        health = GetComponent<HealthSystem>();
        cameraTransform = GetComponentInChildren<Camera>()?.transform;
        defaultHeight = cc.height;
        if (cameraTransform != null)
        {
            defaultCameraLocalPos = cameraTransform.localPosition;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Fly mode toggle (V key)
        if (Input.GetKeyDown(KeyCode.V))
        {
            if (health != null && !health.godMode)
            {
                health.godMode = true;
            }
            isFlying = !isFlying;
            G1Audio.Play2D("pickup", 0.8f, isFlying ? 1.8f : 0.8f);
            Debug.Log($"[FLY MODE] {(isFlying ? "ENABLED" : "DISABLED")}");
        }

        // Reset fly if godMode turned off
        if (health != null && !health.godMode && isFlying)
        {
            isFlying = false;
        }

        // Handle active flight
        if (isFlying)
        {
            Vector3 camFwd = cameraTransform ? cameraTransform.forward : transform.forward;
            Vector3 camRight = cameraTransform ? cameraTransform.right : transform.right;

            Vector3 flyDir = camRight * Input.GetAxisRaw("Horizontal")
                           + camFwd * Input.GetAxisRaw("Vertical");

            if (Input.GetKey(KeyCode.Space))
                flyDir += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C))
                flyDir += Vector3.down;

            float speed = maxSpeed * flySpeedMult;
            if (Input.GetKey(KeyCode.LeftShift))
                speed *= flySprintMult;

            velocity = flyDir * speed;
            cc.Move(velocity * dt);
            return;
        }

        // Crouch input and ceiling check
        bool wishCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        if (!wishCrouch && isCrouching)
        {
            Vector3 centerCrouched = transform.position + Vector3.up * (crouchHeight * 0.5f);
            float radius = cc.radius - 0.05f;
            float checkDist = defaultHeight - crouchHeight;
            if (Physics.SphereCast(centerCrouched, radius, Vector3.up, out RaycastHit hit, checkDist, ~LayerMask.GetMask("Player")))
            {
                wishCrouch = true; // Stay crouched under ceiling obstacles
            }
        }

        // Apply crouch height transitions. The capsule center sits at
        // height/2 (feet-anchored), so changing height keeps the feet at the
        // transform — NO transform reposition is needed. The old code shoved
        // the player 0.4m into the floor, causing depenetration jitter.
        if (wishCrouch != isCrouching)
        {
            isCrouching = wishCrouch;
            float targetHeight = isCrouching ? crouchHeight : defaultHeight;
            cc.height = targetHeight;
            cc.center = new Vector3(0f, targetHeight * 0.5f, 0f);
        }

        // Smooth the camera toward the current stance height
        if (cameraTransform != null)
        {
            Vector3 camPos = cameraTransform.localPosition;
            float targetY = isCrouching ? crouchCameraY : defaultCameraLocalPos.y;
            camPos.y = Mathf.MoveTowards(camPos.y, targetY, 6f * dt);
            cameraTransform.localPosition = camPos;
        }

        float currentMaxSpeed = maxSpeed;
        if (isCrouching)
        {
            currentMaxSpeed *= crouchSpeedMult;
        }

        Vector3 wish = transform.right * Input.GetAxisRaw("Horizontal")
                     + transform.forward * Input.GetAxisRaw("Vertical");
        wish.y = 0f;
        wish = Vector3.ClampMagnitude(wish, 1f);

        bool grounded = cc.isGrounded;
        wasGrounded = grounded;

        if (grounded)
            coyoteTimer = CoyoteWindow;
        else
            coyoteTimer -= dt;

        if (grounded)
        {
            ApplyFriction(dt);
            Accelerate(wish.normalized, wish.magnitude * currentMaxSpeed, accelerate, dt);
            velocity.y = -1f;                       // keep the controller planted
            if (Input.GetButton("Jump"))
                velocity.y = jumpSpeed;
        }
        else
        {
            if (coyoteTimer > 0f && Input.GetButton("Jump"))
            {
                velocity.y = jumpSpeed;
                coyoteTimer = 0f;
            }
            float wishSpeed = Mathf.Min(wish.magnitude * currentMaxSpeed, airWishCap);
            Accelerate(wish.normalized, wishSpeed, airAccelerate, dt);
            velocity.y -= gravity * dt;
        }

        cc.Move(velocity * dt);
        if ((cc.collisionFlags & CollisionFlags.Above) != 0 && velocity.y > 0f)
            velocity.y = 0f;
    }

    void ApplyFriction(float dt)
    {
        Vector3 hv = new Vector3(velocity.x, 0f, velocity.z);
        float speed = hv.magnitude;
        if (speed < 0.001f)
        {
            velocity.x = 0f;
            velocity.z = 0f;
            return;
        }
        float control = Mathf.Max(speed, stopSpeed);
        float newSpeed = Mathf.Max(speed - control * friction * dt, 0f) / speed;
        velocity.x *= newSpeed;
        velocity.z *= newSpeed;
    }

    // Quake accelerate: only the projection of velocity onto wishdir is capped,
    // which is exactly what makes air-strafing gain speed.
    void Accelerate(Vector3 wishDir, float wishSpeed, float accel, float dt)
    {
        if (wishSpeed <= 0f)
            return;
        float current = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), wishDir);
        float add = wishSpeed - current;
        if (add <= 0f)
            return;
        float accelSpeed = Mathf.Min(accel * wishSpeed * dt, add);
        velocity += wishDir * accelSpeed;
    }

    public void Launch(float upwardForce)
    {
        velocity.y = upwardForce;
        coyoteTimer = 0f;
    }
}
