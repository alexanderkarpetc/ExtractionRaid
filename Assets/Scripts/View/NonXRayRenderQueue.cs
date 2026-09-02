using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace View
{
    /// <summary>
    /// Pushes renderers on the NonXRay layer into a dedicated render queue so they
    /// draw after the x-ray pass but before regular transparents.
    ///
    /// This used to live inline in <see cref="NonXRayFeature.AddRenderPasses"/>, which
    /// meant a full <c>FindObjectsByType&lt;Renderer&gt;</c> scan of the scene once per
    /// camera per frame — ~8ms per camera on the main scene, ~17ms total with the FOV
    /// camera. Queue assignment is a static property of the object, so it now runs once
    /// per scene load plus explicitly whenever something spawns a NonXRay object at
    /// runtime (see <see cref="Apply"/>).
    ///
    /// Renderers get a *shared* per-source-material variant instead of the per-renderer
    /// copies <c>Renderer.materials</c> used to create, so NonXRay objects drawing the
    /// same source material still batch together. The source asset is never mutated —
    /// it stays shared with the layer-0 prefabs that also use it (SM_GrassLOD,
    /// ER_MCrate02/04/05), which must keep their original queue.
    /// </summary>
    public static class NonXRayRenderQueue
    {
        public const int RenderQueue = 2999; // Transparent-1: after x-ray, before normal transparents.

        // Source asset material → queue-adjusted variant shared by every NonXRay renderer.
        static readonly Dictionary<Material, Material> Variants = new();
        // Variants we own, so re-applying to an already-processed renderer is a no-op.
        static readonly HashSet<Material> OwnedVariants = new();
        // Scratch list for rebuilding a renderer's material array without a per-call alloc.
        static readonly List<Material> Scratch = new();

        static bool _sceneApplied;

        // Reload Domain is disabled in this project (EditorSettings m_EnterPlayModeOptions=1),
        // so these statics survive Play→Stop→Play and would otherwise hand out materials
        // destroyed with the previous session's scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            foreach (var variant in OwnedVariants)
            {
                if (variant != null) Object.Destroy(variant);
            }
            Variants.Clear();
            OwnedVariants.Clear();
            _sceneApplied = false;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _sceneApplied = false;

        /// <summary>Forces the next <see cref="EnsureSceneApplied"/> call to re-scan.</summary>
        public static void Invalidate() => _sceneApplied = false;

        /// <summary>
        /// One full scene scan, guarded so it only actually runs after a scene load or an
        /// explicit <see cref="Invalidate"/>. Steady-state cost is a bool check, which is
        /// why it is safe to call from a per-camera render callback.
        /// </summary>
        public static void EnsureSceneApplied(LayerMask layerMask)
        {
            if (_sceneApplied) return;
            _sceneApplied = true;

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (((1 << r.gameObject.layer) & layerMask.value) == 0) continue;
                ApplyToRenderer(r);
            }
        }

        /// <summary>
        /// Applies the queue to a freshly spawned hierarchy. Call this after
        /// Instantiate()-ing anything that may contain NonXRay renderers — the scene scan
        /// only covers objects that existed at load time.
        /// </summary>
        public static void Apply(GameObject root)
        {
            if (root == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r.gameObject.layer != LayerUtils.NonXRay) continue;
                ApplyToRenderer(r);
            }
        }

        static void ApplyToRenderer(Renderer r)
        {
            var shared = r.sharedMaterials;
            bool changed = false;

            Scratch.Clear();
            for (int i = 0; i < shared.Length; i++)
            {
                var src = shared[i];
                // Already ours, already in the right queue, or nothing to do.
                if (src == null || src.renderQueue == RenderQueue || OwnedVariants.Contains(src))
                {
                    Scratch.Add(src);
                    continue;
                }
                Scratch.Add(GetVariant(src));
                changed = true;
            }

            if (changed) r.sharedMaterials = Scratch.ToArray();
        }

        static Material GetVariant(Material src)
        {
            if (Variants.TryGetValue(src, out var variant) && variant != null)
                return variant;

            variant = new Material(src)
            {
                name = src.name + " (NonXRay)",
                renderQueue = RenderQueue,
                // Survives scene unload so the cache stays valid across raids; released in
                // ResetOnPlay because domain reload won't do it for us.
                hideFlags = HideFlags.HideAndDontSave,
            };
            Variants[src] = variant;
            OwnedVariants.Add(variant);
            return variant;
        }
    }
}
