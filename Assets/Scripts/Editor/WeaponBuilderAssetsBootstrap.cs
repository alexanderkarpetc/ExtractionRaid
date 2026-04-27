#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor
{
    /// <summary>
    /// Auto-provisions the <see cref="PanelSettings"/> asset used by the Weapon Builder
    /// modal so we don't have to create it manually through the Create menu. Runs once
    /// on domain reload; no-op if the asset already exists.
    /// Mirrors the CraftingMockup bootstrap pattern.
    /// </summary>
    [InitializeOnLoad]
    static class WeaponBuilderAssetsBootstrap
    {
        const string PanelSettingsPath = "Assets/Resources/UI/WeaponBuilder/WeaponBuilderPanelSettings.asset";

        static WeaponBuilderAssetsBootstrap()
        {
            EditorApplication.delayCall += EnsureAssets;
        }

        static void EnsureAssets()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "UI");
            EnsureFolder("Assets/Resources/UI", "WeaponBuilder");

            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "WeaponBuilderPanelSettings";

            // Modal panel: scale-with-screen so the modal is proportional on 4K and
            // still fits on 1366×768. Reference 1920×1080, balanced match — see
            // docs/ai/ui-styling.md "Resolution scaling". Sort 110 keeps it above
            // CraftingMockup (100). Tooltip stays on top via its own 1000.
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.referenceDpi = 96f;
            ps.fallbackDpi = 96f;
            ps.sortingOrder = 110;
            ps.clearColor = false;

            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WeaponBuilder] Created PanelSettings at {PanelSettingsPath}");
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
