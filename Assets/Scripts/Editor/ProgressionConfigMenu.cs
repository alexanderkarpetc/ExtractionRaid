using System.IO;
using Progression;
using UnityEditor;
using UnityEngine;

namespace GameEditor
{
    /// <summary>
    /// Creates (if needed) and seeds the <see cref="ProgressionTreeConfig"/> asset with the
    /// built-in default tree so it loads at runtime and is editable in the inspector.
    /// The asset can't be hand-authored as YAML safely (its script guid is assigned on import),
    /// so this menu item builds it in-editor where guids resolve correctly.
    /// </summary>
    public static class ProgressionConfigMenu
    {
        const string Dir = "Assets/Resources/Configs";
        const string Path = Dir + "/ProgressionTree.asset";

        [MenuItem("Raid/Progression/Create & Seed Config Asset")]
        public static void CreateAndSeed()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Resources", "Configs");

            var cfg = AssetDatabase.LoadAssetAtPath<ProgressionTreeConfig>(Path);
            bool created = false;
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<ProgressionTreeConfig>();
                AssetDatabase.CreateAsset(cfg, Path);
                created = true;
            }

            cfg.SeedDefaultTree();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = cfg;
            EditorGUIUtility.PingObject(cfg);
            Debug.Log($"[Progression] {(created ? "Created and seeded" : "Re-seeded")} {Path} — {cfg.NodeCount} nodes.");
        }

        /// <summary>
        /// Re-rolls every node's material cost from <see cref="ProgressionCostDefaults"/> without
        /// touching stats/layout/wording — use this after tuning the curve, so hand-edited node
        /// effects survive.
        /// </summary>
        [MenuItem("Raid/Progression/Reseed Node Costs")]
        public static void ReseedCosts()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ProgressionTreeConfig>(Path);
            if (cfg == null)
            {
                Debug.LogWarning($"[Progression] No asset at {Path} — run 'Create & Seed Config Asset' first.");
                return;
            }

            cfg.ReseedNodeCosts();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Progression] Reseeded material costs on {cfg.NodeCount} nodes in {Path}.");
        }
    }
}
