using UnityEngine;

/// Frag grenade, weapon slot 6. Hold LMB to cook (3 s fuse), release to throw;
/// auto-throws at full cook. Count-based ammo (start 3, max 9).
public class G1Grenade : WeaponBase
{
    [Header("Grenade")]
    public int count = 3;
    public int maxCount = 9;
    public float fuseTime = 3f;
    public float throwSpeed = 12f;

    float cookStart = -1f;

    public override bool HasAmmo => true;
    public override int Clip => count;
    public override int Reserve => 0;
    public override bool IsReloading => false;

    protected override void HandleInput()
    {
        if (cookStart < 0f && Input.GetButtonDown("Fire1") && count > 0)
            cookStart = Time.time;

        if (cookStart >= 0f &&
            (Input.GetButtonUp("Fire1") || Time.time - cookStart >= fuseTime - 0.1f))
        {
            Throw(fuseTime - (Time.time - cookStart));
            cookStart = -1f;
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
