using System.Collections;
using UnityEngine;

/// 9mm semi-automatic pistol: hitscan, clip + reserve ammo, reload.
/// Animations (Idle/Fire/Reload) come from the pistol FBX takes and are
/// triggered on the model's Animator by state name.
public class G1Pistol : WeaponBase
{
    [Header("Ballistics")]
    public float damage = 8f;            // HL1 9mm energy
    public float range = 120f;
    public float hitForce = 3f;
    public float fireInterval = 0.2f;

    [Header("Ammo")]
    public int clipSize = 17;
    public int clip = 17;
    public int reserve = 68;
    public float reloadTime = 1.6f;      // matches the 48-frame Reload clip

    [Header("Wiring")]
    public Animator modelAnimator;
    public G1WeaponFX weaponFX;

    bool reloading;

    public override bool HasAmmo => true;
    public override int Clip => clip;
    public override int Reserve => reserve;
    public override bool IsReloading => reloading;

    protected override void HandleInput()
    {
        if (reloading)
            return;
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

    float nextFire;

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
            camFX.Punch(1.5f);
            camFX.Shake(0.04f);
        }
        G1Audio.Play2D("fire_pistol", 0.7f);
        if (RayHit(range, out RaycastHit hit))
        {
            bool hitEnemy = ApplyHit(hit, damage, hitForce);
            if (weaponFX)
                weaponFX.SpawnBulletDecal(hit);
            if (hitEnemy && camFX)
                camFX.ShowHitMarker();
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
