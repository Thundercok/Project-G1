using UnityEngine;

/// Self-animating explosion visual: a bright orange point light that decays
/// over ~0.2 s and an expanding, fading shockwave ring. Spawned by
/// G1GrenadeProjectile.Explode(); destroys itself when finished.
public sealed class G1ExplosionFX : MonoBehaviour
{
    public float lightDuration = 0.22f;
    public float lightIntensity = 6f;
    public float lightRange = 12f;
    public float ringDuration = 0.35f;
    public float ringMaxScale = 10f;

    Light flash;
    Transform ring;
    Material ringMat;
    float t;

    public static G1ExplosionFX Spawn(Vector3 pos)
    {
        var go = new GameObject("ExplosionFX");
        go.transform.position = pos;
        return go.AddComponent<G1ExplosionFX>();
    }

    void Start()
    {
        flash = gameObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.6f, 0.2f);
        flash.range = lightRange;
        flash.intensity = lightIntensity;

        var ringGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(ringGo.GetComponent<Collider>());
        ring = ringGo.transform;
        ring.SetParent(transform, false);
        ring.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat
        ring.localPosition = new Vector3(0f, -0.4f, 0f);
        ring.localScale = Vector3.one * 0.5f;
        ringMat = new Material(Shader.Find("Sprites/Default"));
        ringMat.color = new Color(1f, 0.7f, 0.25f, 0.9f);
        ringGo.GetComponent<Renderer>().sharedMaterial = ringMat;
    }

    void Update()
    {
        t += Time.deltaTime;

        if (flash)
            flash.intensity = Mathf.Lerp(lightIntensity, 0f, t / lightDuration);

        if (ring)
        {
            float k = Mathf.Clamp01(t / ringDuration);
            float s = Mathf.Lerp(0.5f, ringMaxScale, k);
            ring.localScale = new Vector3(s, s, 1f);
            var c = ringMat.color;
            c.a = 0.9f * (1f - k);
            ringMat.color = c;
        }

        if (t >= Mathf.Max(lightDuration, ringDuration))
            Destroy(gameObject);
    }
}
