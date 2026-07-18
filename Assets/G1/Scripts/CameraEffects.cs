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

    void Start()
    {
        cam = GetComponent<Camera>();
        movement = GetComponentInParent<PlayerMovement>();
        if (cam) baseFOV = cam.fieldOfView;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        // --- Recover punch toward zero
        if (Mathf.Abs(punchAngle) > 0.01f)
        {
            punchAngle = Mathf.Lerp(punchAngle, 0f, punchRecovery * dt);
            transform.localRotation = Quaternion.Euler(punchAngle, 0f, 0f);
        }
        else if (punchAngle != 0f)
        {
            punchAngle = 0f;
            transform.localRotation = Quaternion.identity;
        }

        // --- Speed FOV
        if (cam && movement)
        {
            Vector3 hv = movement.Velocity;
            hv.y = 0f;
            float speed = hv.magnitude;
            float targetBoost = Mathf.Clamp01(speed / speedForMaxFOV) * maxFOVBoost;
            currentFOVBoost = Mathf.Lerp(currentFOVBoost, targetBoost, fovSmoothing * dt);
            cam.fieldOfView = baseFOV + currentFOVBoost;
        }

        // --- Tick timers
        if (hitMarkerTimer > 0f) hitMarkerTimer -= dt;
        if (damageFlashTimer > 0f) damageFlashTimer -= dt;
    }

    /// Called by weapons on fire. Negative = kick upward (natural recoil feel).
    public void Punch(float degrees)
    {
        punchAngle -= degrees;
    }

    /// Called by weapons when a hitscan hit connects with an IDamageable.
    public void ShowHitMarker()
    {
        hitMarkerTimer = 0.15f;
    }

    /// Called by HealthSystem or damage receiver when the player takes damage.
    public void ShowDamageFlash()
    {
        damageFlashTimer = 0.35f;
    }
}
