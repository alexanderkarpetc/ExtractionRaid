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

            // Modal panel: constant pixel size, above HUD. Sorting order 110 keeps it
            // on top of the Crafting mockup (100) too, in case both are open via DevCheats.
            ps.scaleMode = PanelScaleMode.ConstantPixelSize;
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
