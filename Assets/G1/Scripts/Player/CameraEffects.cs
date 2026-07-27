using UnityEngine;

/// Lightweight camera juice: recoil punch that auto-recovers,
/// speed-based FOV scaling, and hit/damage feedback signals for HUD.
/// Attach to the ViewCamera GameObject alongside MouseLook.
public class CameraEffects : MonoBehaviour
{
    [Header("Recoil Punch")]
    public float punchRecovery = 12f;     // degrees/sec recovery lerp speed

    [Header("Speed FOV")]
    public float baseFOV = 75f;
    public float maxFOVBoost = 8f;        // extra degrees at full sprint
    public float speedForMaxFOV = 12f;    // m/s to reach max boost
    public float fovSmoothing = 6f;

    [Header("Aim down sights")]
    public float adsFOVScale = 0.62f;     // 75 -> ~46 degrees
    /// 0..1, written by whichever weapon is active. Lives here because FOV has
    /// one owner and the sprint boost has to lose to the sight picture.
    [HideInInspector] public float adsBlend;

    Camera cam;
    PlayerMovement movement;

    // punch state
    float punchAngle;   // current accumulated vertical kick (degrees)

    // FOV state
    float currentFOVBoost;

    // hit marker signal (read by PlayerHUD)
    float hitMarkerTimer;
    public bool HitMarkerActive => hitMarkerTimer > 0f;

    // damage vignette signal (read by PlayerHUD)
    float damageFlashTimer;
    public float DamageFlashAlpha => Mathf.Clamp01(damageFlashTimer / 0.35f);

    // camera shake state
    float shakeIntensity;
    public float shakeDecay = 6f;

    MouseLook mouseLook;
    Vector3 defaultCameraLocalPos = new Vector3(0f, 1.62f, 0f);

    void Start()
    {
        cam = GetComponent<Camera>();
        movement = GetComponentInParent<PlayerMovement>();
        mouseLook = GetComponent<MouseLook>();
        if (cam) baseFOV = cam.fieldOfView;
        defaultCameraLocalPos = transform.localPosition;
    }

    float currentRoll;

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        // --- Recover punch toward zero
        if (Mathf.Abs(punchAngle) > 0.01f)
        {
            punchAngle = Mathf.Lerp(punchAngle, 0f, punchRecovery * dt);
        }
        else
        {
            punchAngle = 0f;
        }

        // --- Calculate strafe roll tilt (classic GoldSrc / HL1 movement feel)
        float targetRoll = 0f;
        if (movement != null)
        {
            float sideVel = Vector3.Dot(movement.Velocity, transform.right);
            targetRoll = -Mathf.Clamp(sideVel / Mathf.Max(0.1f, movement.maxSpeed), -1f, 1f) * 1.8f;
        }
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, 10f * dt);

        // --- Apply combined pitch & roll (mouse look + recoil punch + strafe tilt)
        float pitch = mouseLook ? mouseLook.Pitch : 0f;
        transform.localRotation = Quaternion.Euler(pitch + punchAngle, 0f, currentRoll);

        // --- Speed FOV
        if (cam && movement)
        {
            Vector3 hv = movement.Velocity;
            hv.y = 0f;
            float speed = hv.magnitude;
            float targetBoost = Mathf.Clamp01(speed / speedForMaxFOV) * maxFOVBoost;
            currentFOVBoost = Mathf.Lerp(currentFOVBoost, targetBoost, fovSmoothing * dt);
            cam.fieldOfView = Mathf.Lerp(baseFOV + currentFOVBoost,
                                         baseFOV * adsFOVScale,
                                         Mathf.Clamp01(adsBlend));
        }

        // --- Apply camera shake (relative to base camera local position)
        Vector3 basePos = defaultCameraLocalPos;
        if (movement != null && movement.IsCrouching)
        {
            basePos.y = movement.crouchCameraY;
        }

        Vector3 shakeOffset = Vector3.zero;
        if (shakeIntensity > 0.001f)
        {
            shakeIntensity = Mathf.Lerp(shakeIntensity, 0f, shakeDecay * dt);
            shakeOffset = Random.insideUnitSphere * shakeIntensity;
        }
        else
        {
            shakeIntensity = 0f;
        }
        transform.localPosition = basePos + shakeOffset;

        // --- Tick timers
        if (hitMarkerTimer > 0f) hitMarkerTimer -= dt;
        if (damageFlashTimer > 0f) damageFlashTimer -= dt;
    }

    /// Called by weapons on fire. Negative = kick upward (natural recoil feel).
    public void Punch(float degrees)
    {
        punchAngle -= degrees;
    }

    public void Shake(float intensity)
    {
        shakeIntensity = Mathf.Max(shakeIntensity, intensity);
    }

    /// Called by weapons when a hitscan hit connects with an IDamageable.
    public void ShowHitMarker()
    {
        hitMarkerTimer = 0.15f;
        G1Audio.Play2D("pickup", 0.35f, 2.2f);
    }

    /// Called by HealthSystem or damage receiver when the player takes damage.
    public void ShowDamageFlash()
    {
        damageFlashTimer = 0.35f;
        Shake(0.24f); // Shake hard on damage!
    }
}
