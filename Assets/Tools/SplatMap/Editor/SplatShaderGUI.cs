using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtractionRaid.Editor.SplatMap
{
    public sealed class SplatShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);
            foreach (Object target in materialEditor.targets) Configure((Material)target);
        }

        public override void ValidateMaterial(Material material) => Configure(material);

        public static void Configure(Material material)
        {
            bool transparent = material.GetFloat("_UseOpacityMask") > 0.5f;
            material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", transparent ? 0 : 1);
            if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            material.renderQueue = (int)(transparent ? RenderQueue.Transparent : RenderQueue.Geometry);
            // Smooth transparency must not leave a solid depth/normal/shadow silhouette.
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            material.SetShaderPassEnabled("DepthOnly", !transparent);
            material.SetShaderPassEnabled("DepthNormals", !transparent);
        }
    }
}
