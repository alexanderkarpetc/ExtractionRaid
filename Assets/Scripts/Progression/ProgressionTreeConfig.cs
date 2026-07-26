using System.Collections.Generic;
using Constants;
using State;
using UnityEngine;

namespace Progression
{
    public enum NodeSize { Minor, Notable, Keystone }

    /// <summary>What a single line of a node's material cost asks for.</summary>
    public enum ProgressionCostKind
    {
        /// <summary>A plain stackable item id (materials, ammo, mods) — <see cref="ProgressionCostEntry.ItemId"/>.</summary>
        Item = 0,
        /// <summary>An assembled weapon: this Delivery core + this Payload core, both at MinRarity or better.</summary>
        Weapon = 1,
    }

    /// <summary>
    /// One line of a node's unlock cost. Paid out of the player's stash first, then the
    /// backpack (see <see cref="ProgressionCostSystem"/>). A node lists up to three of these.
    ///
    /// Rarity only gates <see cref="ProgressionCostKind.Weapon"/> entries: plain items carry no
    /// runtime rarity in this project (only weapon cores do — see <see cref="PayloadCoreInstance"/>),
    /// so an item line is just "id × quantity".
    /// </summary>
    [System.Serializable]
    public class ProgressionCostEntry
    {
        public ProgressionCostKind Kind = ProgressionCostKind.Item;
        [Min(1)] public int Quantity = 1;

        [Header("Kind = Item")]
        [ItemIdPicker] public string ItemId;

        [Header("Kind = Weapon (Delivery + Payload + rarity)")]
        public string DeliveryId;       // DeliveryCoreDefinition.Id — "SingleAction", "Auto", "Scatter"
        public string PayloadId;        // PayloadCoreDefinition.Id  — "BallisticRound", "LaserCharge"
        public RarityTier MinRarity = RarityTier.Common;

        public bool IsWeapon => Kind == ProgressionCostKind.Weapon;

        /// <summary>Player-facing name of what this line asks for.</summary>
        public string Label => IsWeapon
            ? $"{MinRarity} Weapon"
            : ItemDefinition.Get(ItemId)?.DisplayName ?? ItemId;

        /// <summary>Sub-line for weapon entries: the core combination. Empty for items.</summary>
        public string SubLabel => IsWeapon
            ? $"{CoreName(DeliveryId)} + {CoreName(PayloadId)}"
            : string.Empty;

        // Core ids are code identifiers ("SingleAction"); the item registry carries the pretty
        // name ("Single-Action Delivery"), so prefer it and fall back to the raw id.
        static string CoreName(string coreId)
        {
            var def = ItemDefinition.Get(coreId);
            return def != null ? def.DisplayName.Replace(" Delivery", "") : coreId;
        }

        public static ProgressionCostEntry Item_(string itemId, int qty) =>
            new() { Kind = ProgressionCostKind.Item, ItemId = itemId, Quantity = qty };

        public static ProgressionCostEntry Weapon_(string deliveryId, string payloadId, RarityTier minRarity, int qty = 1) =>
            new()
            {
                Kind = ProgressionCostKind.Weapon, DeliveryId = deliveryId, PayloadId = payloadId,
                MinRarity = minRarity, Quantity = qty,
            };
    }

    /// <summary>
    /// One allocatable node. Numeric perks fill <see cref="StatLabel"/>/<see cref="Magnitude"/>/
    /// <see cref="Unit"/> (they aggregate into the build summary); keystones/specials leave those
    /// empty and describe their effect in <see cref="Description"/>.
    ///
    /// Unlocking costs exactly what's in <see cref="Cost"/> — the materials ARE the gate, there
    /// is no skill-point pool. See <see cref="ProgressionCostDefaults"/> for the ring/size curve
    /// the seeder applies.
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

        [Header("Cost")]
        [Tooltip("Items consumed on unlock — up to 3 lines. Empty = free. There is no point cost: " +
                 "materials are the only gate.")]
        public List<ProgressionCostEntry> Cost = new();
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

        /// <summary>
        /// Re-rolls only <see cref="ProgressionNodeDef.Cost"/> from the default curve, leaving
        /// stats, layout and wording as they are — the safe way to retune the material economy
        /// after hand-editing node effects.
        /// </summary>
        [ContextMenu("Reseed Node Costs")]
        public void ReseedNodeCosts()
        {
            ProgressionCostDefaults.Apply(Disciplines);
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
