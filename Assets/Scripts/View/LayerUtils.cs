using UnityEngine;

namespace View
{
    /// <summary>
    /// Layer convention helpers. Project named layers (per ProjectSettings/TagManager.asset):
    /// <list type="bullet">
    /// <item>0 — Default (worldspace geometry)</item>
    /// <item>4 — Water</item>
    /// <item>6 — Player (player character body + colliders)</item>
    /// <item>7 — Bot (bot character body + colliders)</item>
    /// <item>8 — FOV (fog-of-war geometry)</item>
    /// </list>
    ///
    /// Player + Bot share the same CharacterBody prefab; layer is authored as "Player"
    /// у prefab. Presenters override at instantiation time через
    /// <see cref="SetLayerRecursively"/> so per-character convention holds. Decal raycasts
    /// (and анало systems) rely on this convention via layer mask filters.
    /// </summary>
    public static class LayerUtils
    {
        public const int Default     = 0;
        public const int IgnoreRaycast = 2;
        public const int Water       = 4;
        public const int UI          = 5;
        public const int Player      = 6;
        public const int Bot         = 7;
        public const int FOV         = 8;

        /// <summary>
        /// Sets the layer на GameObject + усіх його дітей recursively. Use after
        /// Instantiate() to override prefab-baked layers per-instance.
        /// </summary>
        public static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            var t = root.transform;
            for (int i = 0, n = t.childCount; i < n; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }
}
