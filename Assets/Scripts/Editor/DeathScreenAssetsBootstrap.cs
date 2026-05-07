#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor
{
    /// <summary>
    /// Auto-provisions the <see cref="PanelSettings"/> asset used by the death
    /// screen overlay. Mirrors <c>HotbarAssetsBootstrap</c> /
    /// <c>WeaponBuilderAssetsBootstrap</c>.
    ///
    /// Sorting order 500 sits above HUD (Hotbar=50) and modals
    /// (Crafting=100, WeaponBuilder=110) so the death screen always reads on
    /// top of the live game UI, but below the Tooltip overlay (1000) — the
    /// death screen itself shouldn't host tooltips.
    /// </summary>
    [InitializeOnLoad]
    static class DeathScreenAssetsBootstrap
    {
        const string PanelSettingsPath = "Assets/Resources/UI/Death/DeathScreenPanelSettings.asset";

        static DeathScreenAssetsBootstrap()
        {
            EditorApplication.delayCall += EnsureAssets;
        }

        static void EnsureAssets()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "UI");
            EnsureFolder("Assets/Resources/UI", "Death");

            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "DeathScreenPanelSettings";

            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.referenceDpi = 96f;
            ps.fallbackDpi = 96f;
            ps.sortingOrder = 500;
            ps.clearColor = false;

            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DeathScreen] Created PanelSettings at {PanelSettingsPath}");
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
