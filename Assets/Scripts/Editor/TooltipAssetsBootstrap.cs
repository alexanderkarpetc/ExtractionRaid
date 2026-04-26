#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor
{
    /// <summary>
    /// Auto-provisions the <see cref="PanelSettings"/> asset used by the Tooltip
    /// overlay so the runtime never has to error-out on a missing asset. Mirrors
    /// <see cref="WeaponBuilderAssetsBootstrap"/>.
    ///
    /// Sorting order 1000 keeps the tooltip on top of every other UI Toolkit panel
    /// in the project (Builder=110, CraftingMockup=100). The panel does not clear
    /// the colour buffer so it composes correctly over the existing UI.
    /// </summary>
    [InitializeOnLoad]
    static class TooltipAssetsBootstrap
    {
        const string PanelSettingsPath = "Assets/Resources/UI/Tooltip/TooltipPanelSettings.asset";

        static TooltipAssetsBootstrap()
        {
            EditorApplication.delayCall += EnsureAssets;
        }

        static void EnsureAssets()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "UI");
            EnsureFolder("Assets/Resources/UI", "Tooltip");

            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "TooltipPanelSettings";
            ps.scaleMode = PanelScaleMode.ConstantPixelSize;
            ps.referenceDpi = 96f;
            ps.fallbackDpi = 96f;
            ps.sortingOrder = 1000;
            ps.clearColor = false;

            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Tooltip] Created PanelSettings at {PanelSettingsPath}");
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
