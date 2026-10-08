using UnityEngine;

namespace ExtractionRaid.Editor.SplatMap
{
    public static class PaintMaterial
    {
        public static bool IsRoad(Material material) =>
            material && material.shader && material.shader.name == "ExtractionRaid/Road Marking Unlit";

        public static Vector2[] UV(Mesh mesh, Material material) =>
            IsRoad(material) && material.GetFloat("_UseRoadMaskUV") > 0.5f ? mesh.uv2 : mesh.uv;

        // Match the shader thresholds, including fractional material values.
        public static Color Grayscale(Color value, float channel)
        {
            float v = channel < 0.5f ? value.r : channel < 1.5f ? value.g : channel < 2.5f ? value.b : value.a;
            return new Color(v, v, v, 1);
        }

        public static void Configure(Material material, bool mask)
        {
            if (IsRoad(material))
            {
                if (mask) material.SetFloat("_MaskChannel", 0);
                return; // Preserve Road Marking blending, alpha clip, shadows and depth offset.
            }
            if (mask) material.SetFloat("_UseOpacityMask", 1);
            SplatShaderGUI.Configure(material);
        }
    }
}