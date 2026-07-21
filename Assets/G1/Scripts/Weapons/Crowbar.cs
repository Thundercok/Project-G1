using System.Collections;
using UnityEngine;

/// Code-driven crowbar viewmodel: swing arc with a raycast at the apex.
/// Viewmodel bob and hit application come from WeaponBase.
public class Crowbar : WeaponBase
{
    [Header("Swing")]
    public float range = 1.9f;
    public float damage = 25f;
    public float swingTime = 0.42f;       // full cycle, HL1-ish cadence
    public float impactMoment = 0.16f;
    public float hitForce = 4f;

    bool swinging;

    static readonly Quaternion RestRot = Quaternion.identity;
    static readonly Quaternion WindupRot = Quaternion.Euler(-18f, -28f, -6f);
    static readonly Quaternion HitRot = Quaternion.Euler(38f, 26f, 10f);

    [Header("Secondary (charged heavy swing)")]
    public float heavyMultiplier = 2.5f;
    public float heavyKnockback = 9f;
    static readonly Quaternion HeavyWindup = Quaternion.Euler(-42f, -10f, -2f);

    protected override void HandleInput()
    {
        if (swinging)
            return;
        // RMB: slower overhead heavy swing, 2.5x damage + knockback
        if (Input.GetButtonDown("Fire2"))
        {
            StartCoroutine(Swing(true));
            return;
        }
        if (Input.GetButton("Fire1"))
            StartCoroutine(Swing(false));
    }

    IEnumerator Swing(bool heavy)
    {
        swinging = true;
        float time = heavy ? swingTime * 1.7f : swingTime;
        float impact = heavy ? impactMoment * 2.2f : impactMoment;
        float dmg = heavy ? damage * heavyMultiplier : damage;
        float force = heavy ? heavyKnockback : hitForce;
        Quaternion windup = heavy ? HeavyWindup : WindupRot;

        G1Audio.Play2D("swing", heavy ? 0.7f : 0.5f, heavy ? 0.7f : 1f);
        if (camFX) camFX.Shake(heavy ? 0.03f : 0.015f);
        bool hitDone = false;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float windupEnd = impact * 0.45f;
            if (t < windupEnd)
            {
                transform.localRotation = Quaternion.Slerp(
                    RestRot, windup, t / windupEnd);
            }
            else if (t < impact)
            {
                float k = (t - windupEnd) / (impact - windupEnd);
                transform.localRotation = Quaternion.Slerp(
                    windup, HitRot, k * k);
            }
            else
            {
                if (!hitDone)
                {
                    hitDone = true;
                    if (RayHit(range, out RaycastHit hit))
                    {
                        ApplyHit(hit, dmg, force);
                        G1Audio.Play("hit_thunk", hit.point, heavy ? 1f : 0.8f,
                                     heavy ? 0.75f : 1f);
                        if (camFX) camFX.Shake(heavy ? 0.14f : 0.05f);
                    }
                }
                float k = (t - impact) / (time - impact);
                transform.localRotation = Quaternion.Slerp(HitRot, RestRot, k);
            }
            yield return null;
        }
        transform.localRotation = RestRot;
        swinging = false;
    }
}
