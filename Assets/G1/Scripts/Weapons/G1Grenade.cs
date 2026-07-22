using UnityEngine;

/// Frag grenade, weapon slot 6. Hold LMB to cook (3 s fuse), release to throw;
/// auto-throws at full cook. Cooking gives rising audio ticks and a growing
/// camera micro-shake so the player feels the fuse burning down.
public class G1Grenade : WeaponBase
{
    [Header("Grenade")]
    public int count = 3;
    public int maxCount = 9;
    public float fuseTime = 3f;
    public float throwSpeed = 12f;

    float cookStart = -1f;
    float nextTick;

    public override bool HasAmmo => true;
    public override int Clip => count;
    public override int Reserve => 0;
    public override bool IsReloading => false;

    CameraEffects Cam =>
        camFX ? camFX : (Camera.main ? Camera.main.GetComponent<CameraEffects>() : null);

    protected override void HandleInput()
    {
        if (cookStart < 0f && Input.GetButtonDown("Fire1") && count > 0)
        {
            cookStart = Time.time;
            nextTick = Time.time;
        }

        if (cookStart >= 0f)
        {
            float cooked = Time.time - cookStart;
            float remaining = Mathf.Max(0f, fuseTime - cooked);

            // rising ticks: interval shrinks and pitch climbs as the fuse burns
            if (Time.time >= nextTick)
            {
                float frac = Mathf.Clamp01(cooked / fuseTime);
                nextTick = Time.time + Mathf.Lerp(0.5f, 0.12f, frac);
                G1Audio.Play2D("hit_thunk", 0.3f, Mathf.Lerp(1.4f, 2.6f, frac), 0f);
            }
            // micro-shake that grows as detonation nears
            var cam = Cam;
            if (cam)
                cam.Shake(Mathf.Lerp(0.008f, 0.03f, Mathf.Clamp01(cooked / fuseTime)));

            if (Input.GetButtonUp("Fire1") || cooked >= fuseTime - 0.1f)
            {
                Throw(remaining);
                cookStart = -1f;
            }
        }
    }

    void Throw(float fuseRemaining)
    {
        count--;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Grenade";
        go.transform.position = viewCamera.transform.position
            + viewCamera.transform.forward * 0.35f;
        go.transform.localScale = Vector3.one * 0.16f;
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.5f, 0.5f, 0f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.velocity = viewCamera.transform.forward * throwSpeed + Vector3.up * 3f;
        rb.angularVelocity = Random.insideUnitSphere * 12f;
        go.AddComponent<G1GrenadeProjectile>().fuse = Mathf.Max(0.15f, fuseRemaining);
    }
}
