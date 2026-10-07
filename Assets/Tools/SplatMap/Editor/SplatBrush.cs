using UnityEngine;

namespace ExtractionRaid.Editor.SplatMap
{
    public static class SplatBrush
    {
        public static Color Blend(Color source, int channel, float amount)
        {
            Vector4 weights = new Vector4(source.r, source.g, source.b, source.a);
            for (int i = 0; i < 4; i++) weights[i] = Mathf.Clamp01(weights[i]);
            float total = weights.x + weights.y + weights.z + weights.w;
            weights = total > 0.0001f ? weights / total : new Vector4(1, 0, 0, 0);
            Vector4 target = Vector4.zero;
            target[Mathf.Clamp(channel, 0, 3)] = 1;
            weights = Vector4.Lerp(weights, target, Mathf.Clamp01(amount));
            return new Color(weights.x, weights.y, weights.z, weights.w);
        }

        public static Color BlendMask(Color source, float visibility, float amount)
        {
            float value = Mathf.Lerp(Mathf.Clamp01(source.r), Mathf.Clamp01(visibility), Mathf.Clamp01(amount));
            return new Color(value, value, value, 1);
        }

        public static void Stamp(Color[] pixels, int size, Vector2 uv, float radius, float strength, bool soft, int channel,
            bool mask = false, float visibility = 1)
        {
            float centerX = uv.x * size - 0.5f;
            float centerY = uv.y * size - 0.5f;
            float pixelRadius = Mathf.Max(1, radius * size);
            int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - pixelRadius));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(centerX + pixelRadius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - pixelRadius));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(centerY + pixelRadius));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float distance = new Vector2(x - centerX, y - centerY).magnitude / pixelRadius;
                if (distance > 1) continue;
                float falloff = soft ? 1 - Mathf.SmoothStep(0, 1, distance) : 1;
                int index = y * size + x;
                pixels[index] = mask
                    ? BlendMask(pixels[index], visibility, strength * falloff)
                    : Blend(pixels[index], channel, strength * falloff);
            }
        }
    }
}
