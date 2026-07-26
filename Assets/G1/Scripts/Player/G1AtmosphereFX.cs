using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small local particle volume around the player: dust in the facility and
/// battlefield, bioluminescent spores in the Threshold. Kept local so it is
/// deterministic, cheap, and always visible in first-person framing.
/// </summary>
[DisallowMultipleComponent]
public sealed class G1AtmosphereFX : MonoBehaviour
{
    ParticleSystem particles;
    Material particleMaterial;
    Texture2D softParticleTexture;

    void Awake()
    {
        CreateParticleVolume();
    }

    void CreateParticleVolume()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        bool alien = sceneName == "Level3";
        bool battlefield = sceneName == "HugeMap";

        Color particleColor = alien
            ? new Color(0.18f, 1f, 0.82f, 0.30f)
            : battlefield
                ? new Color(0.72f, 0.78f, 0.84f, 0.16f)
                : new Color(0.72f, 0.84f, 0.96f, 0.14f);

        var fxObject = new GameObject(alien ? "AmbientSpores" : "AmbientDust");
        fxObject.transform.SetParent(transform, false);
        fxObject.transform.localPosition = new Vector3(0f, 1.4f, 2.2f);
        particles = fxObject.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = alien ? 96 : 70;
        main.startLifetime = new ParticleSystem.MinMaxCurve(alien ? 5f : 7f, alien ? 10f : 13f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, alien ? 0.18f : 0.10f);
        main.startSize = new ParticleSystem.MinMaxCurve(alien ? 0.045f : 0.028f, alien ? 0.12f : 0.075f);
        main.startColor = particleColor;
        main.gravityModifier = 0f;

        var emission = particles.emission;
        emission.rateOverTime = alien ? 11f : 7f;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(battlefield ? 16f : 10f, 4f, battlefield ? 14f : 9f);

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = alien ? 0.42f : 0.22f;
        noise.frequency = alien ? 0.28f : 0.18f;
        noise.scrollSpeed = alien ? 0.24f : 0.10f;
        noise.damping = true;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(particleColor, 0f),
                new GradientColorKey(particleColor, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(particleColor.a, 0.18f),
                new GradientAlphaKey(particleColor.a * 0.8f, 0.76f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = gradient;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.minParticleSize = 0.001f;
        renderer.maxParticleSize = 0.04f;
        renderer.sortingFudge = -0.6f;
        renderer.sharedMaterial = CreateParticleMaterial(particleColor);
    }

    Material CreateParticleMaterial(Color color)
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        particleMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        softParticleTexture = CreateSoftParticleTexture();
        particleMaterial.mainTexture = softParticleTexture;
        if (particleMaterial.HasProperty("_Color"))
            particleMaterial.SetColor("_Color", color);
        return particleMaterial;
    }

    static Texture2D CreateSoftParticleTexture()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, falloff * falloff));
            }
        }
        texture.Apply();
        return texture;
    }

    void OnDestroy()
    {
        if (particleMaterial != null)
            Destroy(particleMaterial);
        if (softParticleTexture != null)
            Destroy(softParticleTexture);
    }
}
