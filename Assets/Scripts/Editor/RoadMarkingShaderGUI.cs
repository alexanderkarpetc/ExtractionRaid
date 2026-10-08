using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtractionRaid.Editor
{
    /// <summary>Synchronizes the blend selector with ShaderLab render state.</summary>
    public sealed class RoadMarkingShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);
            foreach (Object target in materialEditor.targets)
                ValidateMaterial((Material)target);
        }

        public override void ValidateMaterial(Material material)
        {
            int mode = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_BlendMode")), 0, 2);
            BlendMode source = mode == 1 ? BlendMode.One : BlendMode.SrcAlpha;
            BlendMode destination = mode == 2 ? BlendMode.One : BlendMode.OneMinusSrcAlpha;
            SetIfChanged(material, "_SrcBlend", (float)source);
            SetIfChanged(material, "_DstBlend", (float)destination);
        }

        private static void SetIfChanged(Material material, string property, float value)
        {
            if (material.GetFloat(property) != value)
                material.SetFloat(property, value);
        }
    }
}
