using System.Collections;
using UnityEngine;

/// 9mm automatic submachine gun: automatic fire (hitscan), clip + reserve ammo, reload.
/// Animations (Idle/Fire/Reload) come from the SMG FBX takes and are
/// triggered on the model's Animator by state name.
public class G1Smg : WeaponBase
{
    [Header("Ballistics")]
    public float damage = 5f;            // HL1 9mm SMG damage per round
    public float range = 100f;
    public float hitForce = 2.5f;
    public float fireInterval = 0.1f;    // 10 shots per second (automatic)

    [Header("Ammo")]
    public int clipSize = 50;            // 50-round magazine
    public int clip = 50;
    public int reserve = 150;            // Reserve rounds
    public float reloadTime = 2.0f;      // Matches the 60-frame Reload clip

    [Header("Wiring")]
    public Animator modelAnimator;
    public G1WeaponFX weaponFX;

    bool reloading;
    float nextFire;

    public override bool HasAmmo => true;
    public override int Clip => clip;
    public override int Reserve => reserve;
    public override bool IsReloading => reloading;

    [Header("Secondary (40mm launcher)")]
    public float launcherInterval = 0.9f;
    public float launcherSpeed = 22f;
    float nextLaunch;

    protected override void HandleInput()
    {
        if (reloading)
            return;
        // RMB: 40mm explosive grenade, drawn from the grenade reserve
        if (Input.GetButtonDown("Fire2") && Time.time >= nextLaunch)
        {
            var g = Grenades;
            if (g != null && g.count > 0)
            {
                g.count--;
                nextLaunch = Time.time + launcherInterval;
                LaunchGrenade(launcherSpeed, 1.4f);
                G1Audio.Play2D("fire_shotgun", 0.7f, 0.8f);
                if (camFX) { camFX.Punch(2.2f); camFX.Shake(0.1f); }
            }
            return;
        }
        if (Input.GetKeyDown(KeyCode.R) && clip < clipSize && reserve > 0)
        {
            StartCoroutine(Reload());
            return;
        }
        if (Input.GetButton("Fire1") && Time.time >= nextFire)
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
        {
            camFX.Punch(0.8f);
            camFX.Shake(0.05f);
        }
        AddRecoilImpulse(new Vector3(Random.Range(-0.012f, 0.012f), 0.02f, -0.045f));
        G1Audio.Play2D("fire_smg", 0.55f);
        Vector3 origin = muzzlePoint ? muzzlePoint.position : viewCamera.transform.position;
        if (RayHit(range, out RaycastHit hit))
        {
            bool hitEnemy = ApplyHit(hit, damage, hitForce);
            if (weaponFX)
            {
                weaponFX.SpawnBulletDecal(hit);
                weaponFX.PlayTracerBeam(origin, hit.point, new Color(1f, 0.8f, 0.3f));
            }
            if (hitEnemy && camFX)
                camFX.ShowHitMarker();
        }
        else if (weaponFX)
        {
            weaponFX.PlayTracerBeam(origin, origin + viewCamera.transform.forward * range, new Color(1f, 0.8f, 0.3f));
        }
    }

    IEnumerator Reload()
    {
        reloading = true;
        if (modelAnimator)
            modelAnimator.CrossFade("Reload", 0.05f, 0, 0f);
        yield return new WaitForSeconds(reloadTime);
        int take = Mathf.Min(clipSize - clip, reserve);
        clip += take;
        reserve -= take;
        reloading = false;
    }

}
