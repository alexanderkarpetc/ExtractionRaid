#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor
{
    /// <summary>
    /// Auto-provisions the PanelSettings asset used by the UI Toolkit crafting mockup
    /// so we don't have to create it manually through the Create menu.
    /// Runs once on domain reload; no-op if the asset already exists.
    /// </summary>
    [InitializeOnLoad]
    static class CraftingMockupAssetsBootstrap
    {
        const string PanelSettingsPath = "Assets/Resources/UI/Crafting/CraftingMockupPanelSettings.asset";
        const string ThemePath = "Assets/Resources/UI/Crafting/CraftingMockupTheme.tss";

        static CraftingMockupAssetsBootstrap()
        {
            EditorApplication.delayCall += EnsureAssets;
        }

        static void EnsureAssets()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "UI");
            EnsureFolder("Assets/Resources/UI", "Crafting");

            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "CraftingMockupPanelSettings";

            // Runtime theme (built-in). Falls back to null if unavailable — the UI still renders
            // without the theme, it just won't have default text colors.
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme != null)
                ps.themeStyleSheet = theme;
            else
                Debug.LogWarning($"[CraftingMockup] Unity runtime theme not found at {ThemePath}. " +
                                 "PanelSettings will use defaults.");

            // Sensible mockup defaults — constant pixel size, sort above most HUD.
            ps.scaleMode = PanelScaleMode.ConstantPixelSize;
            ps.referenceDpi = 96f;
            ps.fallbackDpi = 96f;
            ps.sortingOrder = 100;
            ps.clearColor = false;

            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CraftingMockup] Created PanelSettings at {PanelSettingsPath}");
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
