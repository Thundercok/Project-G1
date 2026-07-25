using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Procedural PBR Normal Map Generator for Project G1.
/// Generates seamless normal maps for concrete, metal grid, steel panels, hazard stripes, and alien bio textures.
/// </summary>
public static class G1TextureGenerator
{
    private const string TextureDir = "Assets/G1/Textures";
    private const int Size = 512;

    [MenuItem("G1/Generate PBR Normal Maps & Materials", false, 30)]
    public static void GenerateAllNormalMaps()
    {
        if (!Directory.Exists(TextureDir))
        {
            Directory.CreateDirectory(TextureDir);
        }

        GenerateTexture("tex_concrete_wall_normal", GenerateConcreteHeight);
        GenerateTexture("tex_floor_metal_grid_normal", GenerateMetalGridHeight);
        GenerateTexture("tex_steel_panel_normal", GenerateSteelPanelHeight);
        GenerateTexture("tex_hazard_stripe_normal", GenerateHazardStripeHeight);
        GenerateTexture("tex_alien_bio_normal", GenerateAlienBioHeight);

        AssetDatabase.Refresh();
        Debug.Log("G1: Successfully generated all 5 seamless PBR Normal Maps!");
    }

    private delegate float HeightFunction(float u, float v, int x, int y);

    private static void GenerateTexture(string name, HeightFunction heightFunc)
    {
        int width = Size;
        int height = Size;

        // 1. Generate Heightmap
        float[,] hMap = new float[width, height];
        for (int y = 0; y < height; y++)
        {
            float v = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width;
                hMap[x, y] = heightFunc(u, v, x, y);
            }
        }

        // 2. Convert Heightmap to Normal Map via Sobel Filter
        Texture2D normTex = new Texture2D(width, height, TextureFormat.RGBA32, true);
        float strength = 3.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int xL = (x - 1 + width) % width;
                int xR = (x + 1) % width;
                int yD = (y - 1 + height) % height;
                int yU = (y + 1) % height;

                float dX = (hMap[xR, y] - hMap[xL, y]) * strength;
                float dY = (hMap[x, yU] - hMap[x, yD]) * strength;

                Vector3 normal = new Vector3(-dX, -dY, 1.0f).normalized;

                Color col = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1.0f
                );

                normTex.SetPixel(x, y, col);
            }
        }

        normTex.Apply();

        // 3. Save PNG File
        string path = $"{TextureDir}/{name}.png";
        byte[] bytes = normTex.EncodeToPNG();
        Object.DestroyImmediate(normTex);
        File.WriteAllBytes(path, bytes);

        // 4. Configure TextureImporter to NormalMap
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
    }

    // --- Height Functions ---

    private static float GenerateConcreteHeight(float u, float v, int x, int y)
    {
        // Concrete panel seams
        float seamX = Mathf.Abs(u * 2f - Mathf.Round(u * 2f));
        float seamY = Mathf.Abs(v * 2f - Mathf.Round(v * 2f));
        float seam = Mathf.SmoothStep(0.015f, 0.0f, seamX) + Mathf.SmoothStep(0.015f, 0.0f, seamY);

        // Micro grit noise
        float noise1 = Mathf.PerlinNoise(u * 32f, v * 32f) * 0.15f;
        float noise2 = Mathf.PerlinNoise(u * 128f, v * 128f) * 0.08f;

        return (noise1 + noise2) - seam * 0.4f;
    }

    private static float GenerateMetalGridHeight(float u, float v, int x, int y)
    {
        // Grid lines every 64 pixels
        float gridX = Mathf.Abs(u * 8f - Mathf.Round(u * 8f));
        float gridY = Mathf.Abs(v * 8f - Mathf.Round(v * 8f));
        float groove = Mathf.SmoothStep(0.06f, 0.01f, gridX) + Mathf.SmoothStep(0.06f, 0.01f, gridY);

        // Diamond tread pattern inside grid cells
        float du = (u * 16f) % 1.0f - 0.5f;
        float dv = (v * 16f) % 1.0f - 0.5f;
        float diamond = Mathf.Max(0f, 0.35f - (Mathf.Abs(du) + Mathf.Abs(dv)));

        // Rivets at grid intersections
        float rivDist = Mathf.Sqrt(du * du + dv * dv);
        float rivet = Mathf.SmoothStep(0.18f, 0.05f, rivDist) * 0.3f;

        return diamond * 0.6f + rivet - groove * 0.5f;
    }

    private static float GenerateSteelPanelHeight(float u, float v, int x, int y)
    {
        // Beveled panel borders (2x2 panels per tile)
        float pu = (u * 2f) % 1.0f;
        float pv = (v * 2f) % 1.0f;

        float edgeDistU = Mathf.Min(pu, 1.0f - pu);
        float edgeDistV = Mathf.Min(pv, 1.0f - pv);
        float edgeDist = Mathf.Min(edgeDistU, edgeDistV);

        float bevel = Mathf.SmoothStep(0.0f, 0.08f, edgeDist);
        float seam = edgeDist < 0.012f ? -0.4f : 0.0f;

        // Screws near corners
        float cornerU = Mathf.Abs(pu - 0.5f);
        float cornerV = Mathf.Abs(pv - 0.5f);
        float screwDist = Vector2.Distance(new Vector2(cornerU, cornerV), new Vector2(0.42f, 0.42f));
        float screw = Mathf.SmoothStep(0.04f, 0.01f, screwDist) * 0.25f;

        // Fine brushed metal lines
        float brush = Mathf.Sin(v * 256f * Mathf.PI * 2f) * 0.02f;

        return (bevel * 0.3f) + seam + screw + brush;
    }

    private static float GenerateHazardStripeHeight(float u, float v, int x, int y)
    {
        // Diagonal tape ridges
        float diag = Mathf.Repeat((u + v) * 8f, 1.0f);
        float ridge = Mathf.SmoothStep(0.48f, 0.50f, diag) - Mathf.SmoothStep(0.98f, 1.00f, diag);

        // Tape texture grain
        float grain = Mathf.PerlinNoise(u * 64f, v * 64f) * 0.05f;

        return ridge * 0.18f + grain;
    }

    private static float GenerateAlienBioHeight(float u, float v, int x, int y)
    {
        // Organic cell / blobby vein ridges
        float n1 = Mathf.PerlinNoise(u * 12f, v * 12f);
        float n2 = Mathf.PerlinNoise(u * 24f + 5.2f, v * 24f + 1.8f);
        float veins = Mathf.Sin((n1 + n2) * Mathf.PI * 6f) * 0.5f + 0.5f;

        float bubble = Mathf.Pow(veins, 2.5f) * 0.4f;

        return bubble;
    }
}
