using System.Collections;
using UnityEngine;

/// Code-driven crowbar viewmodel: swing arc, hit raycast at the apex,
/// and a classic movement bob. Sits on the WeaponHolder under the camera.
public class Crowbar : MonoBehaviour
{
    public Camera viewCamera;
    public PlayerMovement movement;
    public float range = 1.9f;
    public float damage = 25f;
    public float swingTime = 0.42f;       // full cycle, HL1-ish cadence
    public float impactMoment = 0.16f;
    public float hitForce = 4f;

    Vector3 restPos;
    bool swinging;
    float bobT;

    static readonly Quaternion RestRot = Quaternion.identity;
    static readonly Quaternion WindupRot = Quaternion.Euler(-18f, -28f, -6f);
    static readonly Quaternion HitRot = Quaternion.Euler(38f, 26f, 10f);

    void Start()
    {
        restPos = transform.localPosition;
    }

    void Update()
    {
        if (Input.GetButton("Fire1") && !swinging && Cursor.lockState == CursorLockMode.Locked)
            StartCoroutine(Swing());

        // viewmodel bob from horizontal speed
        Vector3 hv = movement ? movement.Velocity : Vector3.zero;
        hv.y = 0f;
        float speed = movement && movement.Grounded ? hv.magnitude : 0f;
        bobT += Time.deltaTime * Mathf.Lerp(1f, 10f, Mathf.Clamp01(speed / 8f));
        float amp = 0.012f * Mathf.Clamp01(speed / 4f);
        Vector3 bob = new Vector3(Mathf.Cos(bobT) * amp, Mathf.Abs(Mathf.Sin(bobT)) * amp, 0f);
        transform.localPosition = restPos + bob;
    }

    IEnumerator Swing()
    {
        swinging = true;
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
                    DoImpact();
                }
                float k = (t - impactMoment) / (swingTime - impactMoment);
                transform.localRotation = Quaternion.Slerp(HitRot, RestRot, k);
            }
            yield return null;
        }
        transform.localRotation = RestRot;
        swinging = false;
    }

    void DoImpact()
    {
        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, range))
            return;
        Breakable target = hit.collider.GetComponentInParent<Breakable>();
        if (target)
            target.TakeHit(damage, hit.point, ray.direction);
        if (hit.rigidbody)
            hit.rigidbody.AddForceAtPosition(ray.direction * hitForce, hit.point,
                                             ForceMode.Impulse);
    }
}
