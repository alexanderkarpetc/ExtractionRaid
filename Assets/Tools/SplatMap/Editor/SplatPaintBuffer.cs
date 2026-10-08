using UnityEngine;

namespace ExtractionRaid.Editor.SplatMap
{
    public sealed class SplatPaintBuffer : ScriptableObject
    {
        // Retain size as width for existing serialized painting sessions.
        public int size;
        public int height;
        public int Width => size;
        public int Height => height > 0 ? height : size;
        public Color[] pixels;
    }
}
