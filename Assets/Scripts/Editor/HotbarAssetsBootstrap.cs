#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor
{
    /// <summary>
    /// Auto-provisions the <see cref="PanelSettings"/> asset used by the Hotbar
    /// HUD overlay. Mirrors <c>WeaponBuilderAssetsBootstrap</c> /
    /// <c>TooltipAssetsBootstrap</c>.
    ///
    /// Sorting order 50 sits below the Weapon Builder modal (110), the Crafting
    /// mockup (100), and the Tooltip overlay (1000) — the hotbar is a HUD strip,
    /// modals/tooltips that open over it must read on top.
    /// </summary>
    [InitializeOnLoad]
    static class HotbarAssetsBootstrap
    {
        const string PanelSettingsPath = "Assets/Resources/UI/Hotbar/HotbarPanelSettings.asset";

        static HotbarAssetsBootstrap()
        {
            EditorApplication.delayCall += EnsureAssets;
        }

        static void EnsureAssets()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "UI");
            EnsureFolder("Assets/Resources/UI", "Hotbar");

            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "HotbarPanelSettings";

            // Match the project-wide resolution scaling baseline (see
            // docs/ai/ui-styling.md "Resolution scaling"). Sort 50 keeps the
            // hotbar below modals and tooltips that may open over it.
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.referenceDpi = 96f;
            ps.fallbackDpi = 96f;
            ps.sortingOrder = 50;
            ps.clearColor = false;

            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Hotbar] Created PanelSettings at {PanelSettingsPath}");
        }

        static void EnsureFolder(string parent, string name)
        {
            string full = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
