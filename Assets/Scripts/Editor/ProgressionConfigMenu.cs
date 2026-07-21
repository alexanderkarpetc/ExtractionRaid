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
    }
}
