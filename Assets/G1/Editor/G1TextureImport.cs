using UnityEditor;
using UnityEngine;

/// Imports the sky HDRIs as cubemaps automatically.
///
/// An equirectangular .hdr dropped into Unity imports as a flat 2D texture by
/// default, and a flat texture assigned to a skybox renders as a smear. The
/// fix is one checkbox — Texture Shape: Cube — which is exactly the kind of
/// manual step that gets forgotten and then looks like a broken asset. Doing
/// it on import means the file works the moment it lands in the folder,
/// including on a fresh clone.
public sealed class G1TextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.EndsWith(".hdr") && !assetPath.EndsWith(".exr")) return;
        if (!assetPath.Contains("G1/Textures")) return;

        var imp = (TextureImporter)assetImporter;
        if (imp.textureShape == TextureImporterShape.TextureCube) return;

        imp.textureShape = TextureImporterShape.TextureCube;
        imp.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
        imp.sRGBTexture = false;                 // HDR data is already linear
        imp.mipmapEnabled = true;
        imp.maxTextureSize = 2048;
        Debug.Log($"G1: imported {System.IO.Path.GetFileName(assetPath)} as a cubemap.");
    }
}
