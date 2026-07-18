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

    protected bool InputLocked => Cursor.lockState != CursorLockMode.Locked;

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

    /// Damage + physics kick shared by all hitscan weapons.
    /// Returns true if an IDamageable was hit (for hit marker feedback).
    protected bool ApplyHit(RaycastHit hit, float damage, float force)
    {
        var target = hit.collider.GetComponentInParent<IDamageable>();
        target?.TakeDamage(damage, hit.point, hit.normal);
        if (hit.rigidbody)
            hit.rigidbody.AddForceAtPosition(
                viewCamera.transform.forward * force, hit.point, ForceMode.Impulse);
        return target != null;
    }
}
