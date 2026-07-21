using System.Collections.Generic;
using UnityEngine;

namespace Progression
{
    public enum NodeSize { Minor, Notable, Keystone }

    /// <summary>
    /// One allocatable node. Numeric perks fill <see cref="StatLabel"/>/<see cref="Magnitude"/>/
    /// <see cref="Unit"/> (they aggregate into the build summary); keystones/specials leave those
    /// empty and describe their effect in <see cref="Description"/>. <see cref="DevHook"/> is a
    /// designer note pointing at the config field this node is meant to drive — it is NOT wired
    /// automatically (see <see cref="ProgressionSystem.ApplyAllocatedEffects"/>).
    /// </summary>
    [System.Serializable]
    public class ProgressionNodeDef
    {
        public string Id;               // stable, unique — assigned by the seeder as "<disc>.<branch>.<index>"
        public string DisplayName;      // shown under notables/keystones; minors leave blank (tooltip shows the stat)
        public NodeSize Size = NodeSize.Minor;

        [Header("Layout")]
        public int Ring = 1;            // distance from the discipline hub (1 = closest)
        public float Offset;            // angular fork offset in degrees (±15 typical)

        [Header("Numeric effect (leave StatLabel empty for pure keystones)")]
        public string StatLabel;        // e.g. "Max HP" — groups in the build summary
        public float Magnitude;         // e.g. 10 or -15
        public string Unit;             // "", "%", "°", " HP", "s"

        [TextArea] public string Description;   // keystone / special wording
        public int PointCost = 1;
        public string DevHook;          // designer note: which config field this should modify
    }

    [System.Serializable]
    public class ProgressionBranchDef
    {
        public string Name;
        public List<ProgressionNodeDef> Nodes = new();
    }

    [System.Serializable]
    public class ProgressionDisciplineDef
    {
        public string Id;               // "warden", "phantom", ...
        public string DisplayName;
        public Color Color = Color.white;
        [TextArea] public string Tagline;
        public float AngleDeg;          // direction of this sector from the core
        public List<ProgressionBranchDef> Branches = new();
    }

    /// <summary>
    /// Data-only definition of the whole progression tree. Fully inspector-editable —
    /// reorder disciplines/branches/nodes and retune values freely. Runtime allocation
    /// state lives on <see cref="State.PlayerProgressionState"/>, never here.
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressionTree", menuName = "Progression/Progression Tree")]
    public class ProgressionTreeConfig : ScriptableObject
    {
        public const string ResourcePath = "Configs/ProgressionTree";

        [Tooltip("Layout constants shared by every sector — tweak to spread or tighten the web.")]
        public float HubRadius = 120f;
        public float RingBase = 150f;
        public float RingStep = 82f;
        [Tooltip("Angular lanes per branch index within a sector.")]
        public float[] BranchSpread = { -33f, -11f, 11f, 33f };
        [Tooltip("Multiplier applied to a node's Offset so forks stay inside their lane.")]
        public float ForkScale = 0.4f;

        public List<ProgressionDisciplineDef> Disciplines = new();

        static ProgressionTreeConfig _instance;

        /// <summary>Loaded asset, or a runtime-built default tree if the asset is missing/empty.</summary>
        public static ProgressionTreeConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var asset = Resources.Load<ProgressionTreeConfig>(ResourcePath);
                if (asset != null && asset.Disciplines != null && asset.Disciplines.Count > 0)
                    return _instance = asset;

                if (asset != null)
                    Debug.LogWarning("[Progression] ProgressionTree asset is empty — using built-in defaults. " +
                                     "Right-click the asset → 'Seed Default Tree' to make it editable.");
                return _instance = ProgressionTreeDefaults.BuildRuntime();
            }
        }

        // Domain reload is OFF in this project — reset the static cache on entering play. (CLAUDE.md §3.15)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCacheOnPlay() => _instance = null;

        [ContextMenu("Seed Default Tree")]
        public void SeedDefaultTree()
        {
            Disciplines = ProgressionTreeDefaults.BuildDisciplines();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Total number of allocatable nodes across every discipline.</summary>
        public int NodeCount
        {
            get
            {
                int n = 0;
                foreach (var d in Disciplines)
                    foreach (var b in d.Branches)
                        n += b.Nodes.Count;
                return n;
            }
        }

        public bool TryFind(string id, out ProgressionDisciplineDef disc, out ProgressionBranchDef branch, out ProgressionNodeDef node)
        {
            foreach (var d in Disciplines)
                foreach (var b in d.Branches)
                    foreach (var nd in b.Nodes)
                        if (nd.Id == id)
                        {
                            disc = d; branch = b; node = nd; return true;
                        }
            disc = null; branch = null; node = null; return false;
        }
    }
}
