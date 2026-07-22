using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Lightweight weapon FX manager: handles unlit retro muzzle flash 
/// and a pre-allocated pool of bullet decals (bullet holes) on surfaces.
public class G1WeaponFX : MonoBehaviour
{
    [Header("Muzzle Flash Settings")]
    public float flashDuration = 0.05f;
    public float flashSize = 0.08f;

    [Header("Bullet Decal Settings")]
    public float decalSize = 0.035f;
    public int maxDecals = 30;

    GameObject muzzleFlashInstance;
    Light muzzleLight;
    List<GameObject> decalPool = new List<GameObject>();
    int nextDecalIndex = 0;
    float flashTimer;
    float lightTimer;

    void Awake()
    {
        InitializeMuzzleFlash();
        InitializeDecalPool();
    }

    void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && muzzleFlashInstance != null)
            {
                muzzleFlashInstance.SetActive(false);
                muzzleFlashInstance.transform.SetParent(null, false);
            }
        }
        if (lightTimer > 0f && muzzleLight != null)
        {
            lightTimer -= Time.deltaTime;
            muzzleLight.intensity = Mathf.Max(0f, muzzleLight.intensity - Time.deltaTime * 250f);
            if (lightTimer <= 0f)
                muzzleLight.enabled = false;
        }
    }

    void InitializeMuzzleFlash()
    {
        muzzleFlashInstance = new GameObject("MuzzleFlashFX");
        var filter = muzzleFlashInstance.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateCrossMesh(flashSize);
        var renderer = muzzleFlashInstance.AddComponent<MeshRenderer>();

        // Bright yellow unlit material
        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = new Color(1f, 0.85f, 0.1f);
        renderer.sharedMaterial = mat;

        // momentary point light that flashes with the muzzle
        var lightGo = new GameObject("MuzzleLight");
        lightGo.transform.SetParent(muzzleFlashInstance.transform, false);
        muzzleLight = lightGo.AddComponent<Light>();
        muzzleLight.type = LightType.Point;
        muzzleLight.color = new Color(1f, 0.63f, 0.31f);
        muzzleLight.range = 6f;
        muzzleLight.enabled = false;

        muzzleFlashInstance.SetActive(false);
    }

    void InitializeDecalPool()
    {
        Mesh mesh = CreateQuadMesh(decalSize);
        // Dark grey unlit material representing bullet hole
        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = new Color(0.12f, 0.12f, 0.12f);

        for (int i = 0; i < maxDecals; i++)
        {
            var go = new GameObject("BulletDecal_" + i);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.SetActive(false);
            decalPool.Add(go);
        }
    }

    public void PlayMuzzleFlash(Transform muzzlePoint)
    {
        if (muzzlePoint == null || muzzleFlashInstance == null)
            return;

        // Parent to muzzle and align
        muzzleFlashInstance.transform.SetParent(muzzlePoint, false);
        muzzleFlashInstance.transform.localPosition = Vector3.zero;

        // Random roll rotation around forward axis
        muzzleFlashInstance.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        muzzleFlashInstance.SetActive(true);

        if (muzzleLight != null)
        {
            muzzleLight.enabled = true;
            muzzleLight.intensity = 8f;
            lightTimer = 0.04f;
        }

        flashTimer = flashDuration;
    }

    /// Spark/debris burst flying outward from a bullet impact point.
    public void PlayImpactFX(Vector3 point, Vector3 normal)
    {
        int n = Random.Range(4, 7);
        for (int i = 0; i < n; i++)
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(spark.GetComponent<Collider>());
            spark.transform.position = point + normal * 0.02f;
            spark.transform.localScale = new Vector3(0.015f, 0.015f, Random.Range(0.04f, 0.1f));
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = new Color(1f, 0.8f, 0.35f);
            spark.GetComponent<Renderer>().sharedMaterial = mat;
            Vector3 dir = (normal + Random.insideUnitSphere * 0.7f).normalized;
            spark.transform.rotation = Quaternion.LookRotation(dir);
            var rb = spark.AddComponent<Rigidbody>();
            rb.mass = 0.02f;
            rb.velocity = dir * Random.Range(3f, 6f);
            Destroy(spark, Random.Range(0.3f, 0.6f));
        }
    }

    public void SpawnBulletDecal(RaycastHit hit)
    {
        if (decalPool.Count == 0)
            return;

        GameObject decal = decalPool[nextDecalIndex];
        nextDecalIndex = (nextDecalIndex + 1) % maxDecals;

        // Position slightly offset along surface normal to prevent Z-fighting
        decal.transform.position = hit.point + hit.normal * 0.001f;
        // Align local Z with the normal
        decal.transform.rotation = Quaternion.LookRotation(hit.normal);

        decal.SetActive(true);

        // spark burst rides along with every decal spawn
        PlayImpactFX(hit.point, hit.normal);
    }

    /// Fast-fading unlit hitscan tracer line beam
    public void PlayTracerBeam(Vector3 start, Vector3 end, Color tracerColor)
    {
        var lineGo = new GameObject("TracerBeam");
        var line = lineGo.AddComponent<LineRenderer>();
        line.startWidth = 0.04f;
        line.endWidth = 0.01f;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = tracerColor;
        line.material = mat;

        Destroy(lineGo, 0.04f);
    }

    // Programmatic double-sided cross star mesh
    Mesh CreateCrossMesh(float s)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[]
        {
            // Quad 1 (Horizontal)
            new Vector3(-s, 0, -s), new Vector3(s, 0, -s), new Vector3(s, 0, s), new Vector3(-s, 0, s),
            // Quad 2 (Vertical)
            new Vector3(0, -s, -s), new Vector3(0, s, -s), new Vector3(0, s, s), new Vector3(0, -s, s)
        };
        int[] triangles = new int[]
        {
            0, 2, 1,  0, 3, 2,  // Quad 1 front
            1, 2, 0,  2, 3, 0,  // Quad 1 back
            4, 6, 5,  4, 7, 6,  // Quad 2 front
            5, 6, 4,  6, 7, 4   // Quad 2 back
        };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    // Programmatic quad mesh
    Mesh CreateQuadMesh(float s)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-s, -s, 0), new Vector3(s, -s, 0), new Vector3(s, s, 0), new Vector3(-s, s, 0)
        };
        int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
        };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        return mesh;
    }
}
