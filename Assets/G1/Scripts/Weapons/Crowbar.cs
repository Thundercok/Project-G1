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

    protected override void HandleInput()
    {
        if (Input.GetButton("Fire1") && !swinging)
            StartCoroutine(Swing());
    }

    IEnumerator Swing()
    {
        swinging = true;
        G1Audio.Play2D("swing", 0.5f);
        bool hitDone = false;
        float t = 0f;
        while (t < swingTime)
        {
            t += Time.deltaTime;
            float windupEnd = impactMoment * 0.45f;
            if (t < windupEnd)
            {
                transform.localRotation = Quaternion.Slerp(
                    RestRot, WindupRot, t / windupEnd);
            }
            else if (t < impactMoment)
            {
                float k = (t - windupEnd) / (impactMoment - windupEnd);
                transform.localRotation = Quaternion.Slerp(
                    WindupRot, HitRot, k * k);      // accelerate into the hit
            }
            else
            {
                if (!hitDone)
                {
                    hitDone = true;
                    if (RayHit(range, out RaycastHit hit))
                    {
                        ApplyHit(hit, damage, hitForce);
                        G1Audio.Play("hit_thunk", hit.point, 0.8f);
                    }
                }
                float k = (t - impactMoment) / (swingTime - impactMoment);
                transform.localRotation = Quaternion.Slerp(HitRot, RestRot, k);
            }
            yield return null;
        }
        transform.localRotation = RestRot;
        swinging = false;
    }
}
