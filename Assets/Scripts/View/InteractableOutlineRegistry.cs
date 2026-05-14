using System.Collections.Generic;
using UnityEngine;

namespace View
{
    public static class InteractableOutlineRegistry
    {
        public readonly struct Entry
        {
            public readonly Renderer Renderer;
            public readonly float Opacity;

            public Entry(Renderer renderer, float opacity)
            {
                Renderer = renderer;
                Opacity = opacity;
            }
        }

        static readonly Dictionary<Renderer, float> Renderers = new();
        static readonly List<Entry> SnapshotBuffer = new();

        public static int Count => Renderers.Count;

        public static void Register(Renderer renderer, float opacity)
        {
            if (renderer == null) return;
            Renderers[renderer] = Mathf.Clamp01(opacity);
        }

        public static void Unregister(Renderer renderer)
        {
            if (renderer != null)
                Renderers.Remove(renderer);
        }

        public static Entry[] GetSnapshot()
        {
            SnapshotBuffer.Clear();

            foreach (var kvp in Renderers)
            {
                var renderer = kvp.Key;
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    SnapshotBuffer.Add(new Entry(renderer, kvp.Value));
            }

            if (SnapshotBuffer.Count != Renderers.Count)
            {
                Renderers.Clear();
                foreach (var entry in SnapshotBuffer)
                    Renderers[entry.Renderer] = entry.Opacity;
            }

            return SnapshotBuffer.ToArray();
        }
    }
}
