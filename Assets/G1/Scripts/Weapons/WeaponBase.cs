using UnityEngine;

/// Shared viewmodel behavior for everything held under the camera:
/// wiring to the player, cursor-lock gating, movement bob, and hitscan helpers.
public abstract class WeaponBase : MonoBehaviour
{
    [Header("Wiring")]
    public Camera viewCamera;
    public PlayerMovement movement;
    public LayerMask hitMask = ~0;

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
        return Physics.Raycast(ray, out hit, range, hitMask);
    }

    /// Damage + physics kick shared by all hitscan weapons.
    protected void ApplyHit(RaycastHit hit, float damage, float force)
    {
        hit.collider.GetComponentInParent<IDamageable>()
            ?.TakeDamage(damage, hit.point, hit.normal);
        if (hit.rigidbody)
            hit.rigidbody.AddForceAtPosition(
                viewCamera.transform.forward * force, hit.point, ForceMode.Impulse);
    }
}
