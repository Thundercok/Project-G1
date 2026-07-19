using UnityEngine;

public class G1CCTVScreen : MonoBehaviour
{
    public Vector3 cameraPosition = new Vector3(12f, 8f, 54f);
    public Vector3 cameraTarget = new Vector3(12f, 1f, 42f);
    public float fieldOfView = 60f;

    private Camera cctvCamera;
    private RenderTexture renderTexture;

    void Start()
    {
        // 1. Create CCTV Camera
        GameObject camGo = new GameObject("CCTV_Camera_Runtime");
        cctvCamera = camGo.AddComponent<Camera>();
        cctvCamera.transform.position = cameraPosition;
        cctvCamera.transform.LookAt(cameraTarget);
        cctvCamera.fieldOfView = fieldOfView;
        
        // Ensure no AudioListener conflicts
        var listener = camGo.GetComponent<AudioListener>();
        if (listener != null) Destroy(listener);

        // 2. Create Render Texture
        renderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        renderTexture.Create();
        cctvCamera.targetTexture = renderTexture;

        // 3. Apply to Renderer material
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Unlit/Texture"));
            mat.mainTexture = renderTexture;
            renderer.sharedMaterial = mat;
        }
    }

    void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
