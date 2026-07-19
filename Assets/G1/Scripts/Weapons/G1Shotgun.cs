using System.Collections;
using UnityEngine;

/// 12-Gauge pump-action shotgun: multi-pellet spread hitscan,
/// shell-by-shell reload with interruption support.
public class G1Shotgun : WeaponBase
{
    [Header("Ballistics")]
    public float damage = 4f;            // 4 damage per pellet
    public int pellets = 8;              // 8 pellets per shot
    public float range = 45f;            // shorter effective range
    public float hitForce = 6f;          // high impact kick per pellet
    public float spreadAngle = 4.5f;     // spread cone in degrees
    public float fireInterval = 0.8f;    // pump-action cycle delay

    [Header("Ammo")]
    public int clipSize = 8;
    public int clip = 8;
    public int reserve = 24;
    public float reloadShellTime = 0.5f; // time to insert one shell
    public float reloadEndTime = 0.25f;   // time to return to idle after reload

    [Header("Wiring")]
    public Animator modelAnimator;
    public G1WeaponFX weaponFX;

    bool reloading;
    bool wantsToFireDuringReload;
    float nextFire;

    public override bool HasAmmo => true;
    public override int Clip => clip;
    public override int Reserve => reserve;
    public override bool IsReloading => reloading;

    protected override void HandleInput()
    {
        if (reloading)
        {
            if (Input.GetButtonDown("Fire1"))
            {
                wantsToFireDuringReload = true;
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.R) && clip < clipSize && reserve > 0)
        {
            StartCoroutine(Reload());
            return;
        }

        if (Input.GetButtonDown("Fire1") && Time.time >= nextFire)
        {
            if (clip <= 0)
            {
                if (reserve > 0)
                    StartCoroutine(Reload());
                return;
            }
            Fire();
        }
    }

    void Fire()
    {
        nextFire = Time.time + fireInterval;
        clip--;

        if (modelAnimator)
            modelAnimator.CrossFade("Fire", 0.02f, 0, 0f);
        if (weaponFX && muzzlePoint)
            weaponFX.PlayMuzzleFlash(muzzlePoint);
        if (camFX)
            camFX.Punch(3.5f); // substantial recoil kick for shotgun

        bool hitAnyEnemy = false;
        for (int i = 0; i < pellets; i++)
        {
            if (RayHitSpread(range, spreadAngle, out RaycastHit hit))
            {
                bool hitEnemy = ApplyHit(hit, damage, hitForce);
                if (hitEnemy) hitAnyEnemy = true;
                if (weaponFX)
                    weaponFX.SpawnBulletDecal(hit);
            }
        }

        if (hitAnyEnemy && camFX)
            camFX.ShowHitMarker();
    }

    IEnumerator Reload()
    {
        reloading = true;
        wantsToFireDuringReload = false;

        while (clip < clipSize && reserve > 0)
        {
            // Insert single shell animation
            if (modelAnimator)
                modelAnimator.CrossFade("Reload", 0.05f, 0, 0f);

            float elapsed = 0f;
            while (elapsed < reloadShellTime)
            {
                elapsed += Time.deltaTime;
                // Capture fire input during shell insert wait
                if (Input.GetButtonDown("Fire1") && clip > 0)
                {
                    wantsToFireDuringReload = true;
                }
                yield return null;
            }

            // FIX Bug 2: increment BEFORE checking for interrupt so the shell is not lost
            clip++;
            reserve--;

            if (wantsToFireDuringReload)
                break;
        }

        // FIX Bug 1: CrossFade to Idle and wait for reloadEndTime to prevent instant snap
        if (modelAnimator)
            modelAnimator.CrossFade("Idle", 0.05f, 0, 0f);
        yield return new WaitForSeconds(reloadEndTime);

        reloading = false;

        // Trigger immediate shot if interrupted
        if (wantsToFireDuringReload && clip > 0 && Time.time >= nextFire)
        {
            wantsToFireDuringReload = false;
            Fire();
        }
        wantsToFireDuringReload = false;
    }

    bool RayHitSpread(float range, float angle, out RaycastHit hit)
    {
        Vector3 forward = viewCamera.transform.forward;
        Vector3 up = viewCamera.transform.up;
        Vector3 right = viewCamera.transform.right;

        float angleRad = angle * Mathf.Deg2Rad;
        float r = Random.Range(0f, angleRad);
        float phi = Random.Range(0f, 2f * Mathf.PI);

        Vector3 spreadDir = forward + (right * Mathf.Cos(phi) + up * Mathf.Sin(phi)) * r;
        var ray = new Ray(viewCamera.transform.position, spreadDir.normalized);

        return Physics.Raycast(ray, out hit, range, hitMask);
    }
}
