using UnityEngine;

/// Shared viewmodel behavior for everything held under the camera:
/// wiring to the player, cursor-lock gating, movement bob, and hitscan helpers.
public abstract class WeaponBase : MonoBehaviour
{
    [Header("Wiring")]
    public Camera viewCamera;
    public PlayerMovement movement;
    public LayerMask hitMask = ~0;
    public Transform muzzlePoint;
    public CameraEffects camFX;

    [Header("View bob")]
    public float bobAmount = 0.012f;

    protected Vector3 restPos;
    float bobT;

    protected bool InputLocked => Cursor.lockState != CursorLockMode.Locked
                                || G1MobSpawnerToolbox.IsOpen;

    // HUD interfaces
    public virtual bool HasAmmo => false;
    public virtual int Clip => 0;
    public virtual int Reserve => 0;
    public virtual bool IsReloading => false;

    protected virtual void Start()
    {
        restPos = transform.localPosition;
    }

    protected virtual void Update()
    {
        if (!InputLocked)
            HandleInput();
        ApplyBob();
    }

    /// Per-weapon input handling; only called while the cursor is locked.
    protected abstract void HandleInput();

    void ApplyBob()
    {
        Vector3 hv = movement ? movement.Velocity : Vector3.zero;
        hv.y = 0f;
        float speed = movement && movement.Grounded ? hv.magnitude : 0f;
        bobT += Time.deltaTime * Mathf.Lerp(1f, 10f, Mathf.Clamp01(speed / 8f));
        float amp = bobAmount * Mathf.Clamp01(speed / 4f);
        transform.localPosition = restPos + new Vector3(
            Mathf.Cos(bobT) * amp, Mathf.Abs(Mathf.Sin(bobT)) * amp, 0f);
    }

    protected bool RayHit(float range, out RaycastHit hit)
    {
        var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        // Direct hit takes priority
        if (Physics.Raycast(ray, out hit, range, hitMask))
            return true;
        // Bullet magnetism: broad-phase cylinder check via SphereCast
        float magnetRadius = range * 0.018f; // Max cone radius at max range
        if (Physics.SphereCast(ray, magnetRadius, out hit, range, hitMask))
        {
            if (hit.collider.GetComponentInParent<IDamageable>() != null)
            {
                // Verify against true narrow-phase cone using closest point on collider surface
                Vector3 toCollider = hit.collider.bounds.center - ray.origin;
                float projection = Vector3.Dot(toCollider, ray.direction);
                if (projection > 0f)
                {
                    Vector3 pointOnRay = ray.origin + ray.direction * projection;
                    Vector3 closestSurfacePoint = hit.collider.ClosestPoint(pointOnRay);
                    float lateralOffset = Vector3.Distance(pointOnRay, closestSurfacePoint);
                    float trueConeRadius = projection * 0.018f; // ~2° full-angle cone (1° half-angle)
                    if (lateralOffset <= trueConeRadius)
                        return true;
                }
            }
        }
        return false;
    }

    /// Damage + physics kick shared by all weapons with Headshot & Crit Multipliers.
    /// Returns true if an IDamageable was hit (for hit marker feedback).
    protected bool ApplyHit(RaycastHit hit, float damage, float force)
    {
        damage *= G1Difficulty.OutgoingDamageMult;
        var target = hit.collider.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            bool isHeadshot = false;
            bool isCrit = false;
            float multiplier = 1.0f;

            // Detect Headshot (collider name or hit height relative to enemy root)
            Transform enemyTransform = hit.collider.transform;
            while (enemyTransform.parent != null && enemyTransform.GetComponent<IDamageable>() == null)
            {
                enemyTransform = enemyTransform.parent;
            }

            string colName = hit.collider.name.ToLower();
            float relativeHeight = hit.point.y - enemyTransform.position.y;

            if (colName.Contains("head") || colName.Contains("skull") || relativeHeight >= 1.35f)
            {
                isHeadshot = true;
                multiplier = 2.5f; // 2.5x Headshot multiplier
            }
            else if (Random.value <= 0.15f) // 15% Random Crit chance
            {
                isCrit = true;
                multiplier = 1.75f; // 1.75x Critical multiplier
            }

            float finalDamage = damage * multiplier;
            target.TakeDamage(finalDamage, hit.point, hit.normal);

            // Display HUD Headshot/Crit popup banner & play audio hit ping
            var hud = FindFirstObjectByType<PlayerHUD>();
            if (hud != null && (isHeadshot || isCrit))
            {
                hud.ShowCritFeedback(isHeadshot, isCrit, finalDamage);
            }

            if (hit.rigidbody)
                hit.rigidbody.AddForceAtPosition(
                    viewCamera.transform.forward * force * multiplier, hit.point, ForceMode.Impulse);

            return true;
        }
        return false;
    }

    G1Grenade cachedGrenade;

    /// The player's grenade weapon (sibling holder under the camera), for
    /// weapons whose secondary fire consumes grenades (e.g. the SMG launcher).
    protected G1Grenade Grenades
    {
        get
        {
            if (cachedGrenade == null && transform.parent != null)
                cachedGrenade = transform.parent.GetComponentInChildren<G1Grenade>(true);
            return cachedGrenade;
        }
    }

    /// Launch a live frag along the aim ray (used by the SMG 40mm launcher).
    protected void LaunchGrenade(float speed, float fuse)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "LaunchedGrenade";
        go.transform.position = (muzzlePoint ? muzzlePoint.position
            : viewCamera.transform.position) + viewCamera.transform.forward * 0.4f;
        go.transform.localScale = Vector3.one * 0.16f;
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.5f, 0.5f, 0f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.velocity = viewCamera.transform.forward * speed + Vector3.up * 1.2f;
        rb.angularVelocity = Random.insideUnitSphere * 10f;
        go.AddComponent<G1GrenadeProjectile>().fuse = fuse;
    }
}
