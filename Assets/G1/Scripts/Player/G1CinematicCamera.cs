using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lightweight built-in-render-pipeline presentation pass. It gives every
/// gameplay scene a distinct filmic grade, restrained bloom, vignette and
/// chromatic edge separation without requiring a URP/HDRP migration.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class G1CinematicCamera : MonoBehaviour
{
    Camera targetCamera;
    Material compositeMaterial;

    Color sceneTint = Color.white;
    float exposure;
    float contrast = 1.08f;
    float saturation = 0.94f;
    float vignette = 0.28f;
    float grain = 0.016f;
    float bloomIntensity = 0.46f;
    float bloomThreshold = 0.78f;
    float bloomRadius = 1.35f;
    float chromaticAberration = 0.0012f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallPresentationLayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        var playerCamera = player.GetComponentInChildren<Camera>(true);
        if (playerCamera != null && playerCamera.GetComponent<G1CinematicCamera>() == null)
            playerCamera.gameObject.AddComponent<G1CinematicCamera>();

        if (player.GetComponent<G1AtmosphereFX>() == null)
            player.AddComponent<G1AtmosphereFX>();
    }

    void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.allowHDR = true;
        targetCamera.allowMSAA = true;
        targetCamera.depthTextureMode |= DepthTextureMode.Depth;

        Shader shader = Resources.Load<Shader>("Shaders/G1CinematicPresentation");
        if (shader == null)
            shader = Shader.Find("Hidden/G1/CinematicPresentation");
        if (shader != null && shader.isSupported)
            compositeMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };

        ConfigureSceneGrade(SceneManager.GetActiveScene().name);
    }

    void ConfigureSceneGrade(string sceneName)
    {
        switch (sceneName)
        {
            case "Level2":
                // Cold, desaturated dawn with practical orange light.
                sceneTint = new Color(0.87f, 0.94f, 1f);
                exposure = 0.10f;
                contrast = 1.12f;
                saturation = 0.82f;
                vignette = 0.24f;
                bloomIntensity = 0.38f;
                bloomThreshold = 0.82f;
                grain = 0.012f;
                break;

            case "Level3":
                // Alien undercroft: rich cyan emissives against near-black rock.
                sceneTint = new Color(0.82f, 1f, 0.97f);
                exposure = -0.04f;
                contrast = 1.18f;
                saturation = 1.10f;
                vignette = 0.35f;
                bloomIntensity = 0.62f;
                bloomThreshold = 0.62f;
                bloomRadius = 1.65f;
                chromaticAberration = 0.0018f;
                grain = 0.020f;
                break;

            case "HugeMap":
                // Battlefield readability: clean contrast, almost no chromatic split.
                sceneTint = new Color(0.97f, 0.98f, 1f);
                exposure = 0.05f;
                contrast = 1.08f;
                saturation = 0.90f;
                vignette = 0.18f;
                bloomIntensity = 0.26f;
                bloomThreshold = 0.92f;
                chromaticAberration = 0.0006f;
                grain = 0.008f;
                break;

            default:
                // Corvus facility: cool steel shadows and hot amber fixtures.
                sceneTint = new Color(0.90f, 0.96f, 1f);
                exposure = 0.02f;
                contrast = 1.14f;
                saturation = 0.92f;
                vignette = 0.30f;
                bloomIntensity = 0.50f;
                bloomThreshold = 0.72f;
                grain = 0.016f;
                break;
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (compositeMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        compositeMaterial.SetColor("_Tint", sceneTint);
        compositeMaterial.SetFloat("_Exposure", exposure);
        compositeMaterial.SetFloat("_Contrast", contrast);
        compositeMaterial.SetFloat("_Saturation", saturation);
        compositeMaterial.SetFloat("_Vignette", vignette);
        compositeMaterial.SetFloat("_Grain", grain);
        compositeMaterial.SetFloat("_BloomIntensity", bloomIntensity);
        compositeMaterial.SetFloat("_BloomThreshold", bloomThreshold);
        compositeMaterial.SetFloat("_BloomRadius", bloomRadius);
        compositeMaterial.SetFloat("_ChromaticAberration", chromaticAberration);
        Graphics.Blit(source, destination, compositeMaterial);
    }

    void OnDestroy()
    {
        if (compositeMaterial != null)
            Destroy(compositeMaterial);
    }
}
