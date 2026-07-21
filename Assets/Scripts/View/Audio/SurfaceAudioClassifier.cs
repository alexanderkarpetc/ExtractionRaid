using System;
using UnityEngine;

namespace View.Audio
{
    public static class SurfaceAudioClassifier
    {
        static readonly string[] MetalKeywords =
        {
            "metal", "steel", "iron", "aluminium", "aluminum", "tin", "fence", "vehicle"
        };

        public static string Resolve(Collider collider)
        {
            if (collider == null) return "surface";
            if (ContainsMetalKeyword(collider.sharedMaterial != null ? collider.sharedMaterial.name : null))
                return "metal";

            var renderer = collider.GetComponentInParent<Renderer>();
            if (renderer != null)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && ContainsMetalKeyword(materials[i].name))
                        return "metal";
            }
            return "surface";
        }

        static bool ContainsMetalKeyword(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < MetalKeywords.Length; i++)
                if (value.IndexOf(MetalKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }
    }
}
