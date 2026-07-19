using System.Collections;
using UnityEngine;

/// 357 Magnum Revolver: High damage (50 DMG), heavy physics force (15),
/// large camera kick (6.0°), and single-shell nạp đạn từng viên (interrupted by Fire1).
public class G1Magnum : WeaponBase
{
    [Header("Ballistics")]
    public float damage = 50f;
    public float range = 150f;
    public float hitForce = 15f;
    public float fireInterval = 0.65f;

    [Header("Ammo")]
    public int clipSize = 6;
    public int clip = 6;
    public int reserve = 18;

    [Header("Reload Durations")]
    public float cylinderOpenDur = 0.35f;
    public float bulletInsertDur = 0.55f;
    public float cylinderCloseDur = 0.25f;

    [Header("Wiring")]
    public Animator modelAnimator;
    public G1WeaponFX weaponFX;

    private float nextFire;
    private float recoilAccum;

    // Reload State Machine
    private enum ReloadPhase : byte { None, OpenCylinder, InsertBullet, CloseCylinder }
    private ReloadPhase reloadPhase = ReloadPhase.None;
    private float reloadTimer;
    private int bulletsToInsert;
    private bool wantsToInterrupt;

    public override bool HasAmmo => true;
    public override int Clip => clip;
    public override int Reserve => reserve;
    public override bool IsReloading => reloadPhase != ReloadPhase.None;

    protected override void Start()
    {
        base.Start();
        clip = clipSize;
    }

    protected override void Update()
    {
        base.Update();

        if (recoilAccum > 0.01f)
            recoilAccum = Mathf.Lerp(recoilAccum, 0f, 7.0f * Time.deltaTime);
        else
            recoilAccum = 0f;

        if (InputLocked) return;

        if (reloadPhase != ReloadPhase.None)
        {
            TickReload();
        }
    }

    protected override void HandleInput()
    {
        if (reloadPhase != ReloadPhase.None)
        {
            if (Input.GetButtonDown("Fire1"))
            {
                wantsToInterrupt = true;
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.R) && clip < clipSize && reserve > 0)
        {
            BeginReload();
            return;
        }

        if (Input.GetButtonDown("Fire1") && Time.time >= nextFire)
        {
            if (clip <= 0)
            {
                if (reserve > 0)
                    BeginReload();
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
        G1Audio.Play2D("fire_magnum", 0.9f);

        recoilAccum = Mathf.Min(recoilAccum + 6.0f, 9.0f);
        if (camFX)
        {
            camFX.Punch(recoilAccum);
            camFX.Shake(0.24f);
        }

        if (RayHit(range, out RaycastHit hit))
        {
            bool hitEnemy = ApplyHit(hit, damage, hitForce);
            if (weaponFX)
                weaponFX.SpawnBulletDecal(hit);
            if (hitEnemy && camFX)
                camFX.ShowHitMarker();
        }
    }

    void BeginReload()
    {
        bulletsToInsert = clipSize - clip;
        if (bulletsToInsert > reserve)
            bulletsToInsert = reserve;

        reloadPhase = ReloadPhase.OpenCylinder;
        reloadTimer = cylinderOpenDur;
        wantsToInterrupt = false;

        if (modelAnimator)
            modelAnimator.CrossFade("ReloadOpen", 0.05f, 0, 0f);
    }

    void TickReload()
    {
        reloadTimer -= Time.deltaTime;
        if (reloadTimer > 0f) return;

        switch (reloadPhase)
        {
            case ReloadPhase.OpenCylinder:
                reloadPhase = ReloadPhase.InsertBullet;
                reloadTimer = bulletInsertDur;
                if (modelAnimator)
                    modelAnimator.CrossFade("ReloadInsert", 0.05f, 0, 0f);
                break;

            case ReloadPhase.InsertBullet:
                clip++;
                reserve--;
                bulletsToInsert--;

                bool stop = wantsToInterrupt || bulletsToInsert <= 0 || reserve <= 0;
                if (stop)
                {
                    reloadPhase = ReloadPhase.CloseCylinder;
                    reloadTimer = cylinderCloseDur;
                    if (modelAnimator)
                        modelAnimator.CrossFade("ReloadClose", 0.05f, 0, 0f);
                }
                else
                {
                    reloadTimer = bulletInsertDur;
                    if (modelAnimator)
                        modelAnimator.CrossFade("ReloadInsert", 0.05f, 0, 0f);
                }
                break;

            case ReloadPhase.CloseCylinder:
                reloadPhase = ReloadPhase.None;
                if (wantsToInterrupt && clip > 0 && Time.time >= nextFire)
                {
                    wantsToInterrupt = false;
                    Fire();
                }
                wantsToInterrupt = false;
                break;
        }
    }
}
