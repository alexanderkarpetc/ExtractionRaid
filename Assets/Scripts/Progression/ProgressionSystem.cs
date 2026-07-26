using System.Collections.Generic;
using State;

namespace Progression
{
    /// <summary>Aggregated numeric perk for the build summary. Keystones/specials aren't summed.</summary>
    public struct StatSum { public float Value; public string Unit; }

    /// <summary>
    /// Stateless rules for the progression tree: what's connected, what's already taken,
    /// and (stubbed) how allocated nodes translate into gameplay. The view calls these —
    /// it never decides allocation itself. There is intentionally NO refund path.
    ///
    /// This half only knows about <b>connectivity</b>. There is no skill-point pool: a node's
    /// price is the items in its <see cref="ProgressionNodeDef.Cost"/>, charged by
    /// <c>Systems.ProgressionCostSystem</c> (it needs the player's stash). Call its
    /// <c>TryUnlock</c> rather than <see cref="Allocate"/> from gameplay/UI, or nothing is paid.
    /// </summary>
    public static class ProgressionSystem
    {
        public static bool IsAllocated(PlayerProgressionState state, string id) =>
            state != null && state.AllocatedNodeIds.Contains(id);

        /// <summary>Nodes a node hangs off — its nearest lower-ring neighbour(s) in the same branch.</summary>
        public static List<ProgressionNodeDef> GetParents(ProgressionBranchDef branch, ProgressionNodeDef node)
        {
            var parents = new List<ProgressionNodeDef>();
            int maxLower = -1;
            foreach (var n in branch.Nodes)
                if (n.Ring < node.Ring && n.Ring > maxLower) maxLower = n.Ring;
            if (maxLower < 0) return parents;   // ring-1 node → gated only by the (free) discipline hub

            // Keystones join every deepest node; others attach to the nearest by angle.
            ProgressionNodeDef nearest = null;
            float best = float.MaxValue;
            foreach (var n in branch.Nodes)
            {
                if (n.Ring != maxLower) continue;
                if (node.Size == NodeSize.Keystone) { parents.Add(n); continue; }
                float d = UnityEngine.Mathf.Abs(n.Offset - node.Offset);
                if (d < best) { best = d; nearest = n; }
            }
            if (node.Size != NodeSize.Keystone && nearest != null) parents.Add(nearest);
            return parents;
        }

        /// <summary>A node is reachable when it's a ring-1 node, or any parent is already allocated.</summary>
        public static bool IsConnected(PlayerProgressionState state, ProgressionBranchDef branch, ProgressionNodeDef node)
        {
            var parents = GetParents(branch, node);
            if (parents.Count == 0) return true;
            foreach (var p in parents)
                if (IsAllocated(state, p.Id)) return true;
            return false;
        }

        /// <summary>
        /// Structural gate only: the node exists, isn't taken yet, and hangs off something
        /// allocated. Materials are the actual price — see <c>Systems.ProgressionCostSystem</c>.
        /// </summary>
        public static bool CanAllocate(ProgressionTreeConfig cfg, PlayerProgressionState state, string id)
        {
            if (cfg == null || state == null) return false;
            if (IsAllocated(state, id)) return false;
            if (!cfg.TryFind(id, out _, out var branch, out var node)) return false;
            return IsConnected(state, branch, node);
        }

        /// <summary>Mark a node allocated. Permanent — returns false if it isn't connected.</summary>
        public static bool Allocate(ProgressionTreeConfig cfg, PlayerProgressionState state, string id)
        {
            if (!CanAllocate(cfg, state, id)) return false;
            state.AllocatedNodeIds.Add(id);
            return true;
        }

        /// <summary>How many nodes are unlocked — drives the "n / total" header.</summary>
        public static int AllocatedCount(ProgressionTreeConfig cfg, PlayerProgressionState state)
        {
            int n = 0;
            foreach (var id in state.AllocatedNodeIds)
                if (cfg.TryFind(id, out _, out _, out _)) n++;
            return n;
        }

        /// <summary>Numeric perks summed by StatLabel — drives the build-summary panel.</summary>
        public static Dictionary<string, StatSum> Summarize(ProgressionTreeConfig cfg, PlayerProgressionState state)
        {
            var agg = new Dictionary<string, StatSum>();
            foreach (var id in state.AllocatedNodeIds)
            {
                if (!cfg.TryFind(id, out _, out _, out var node)) continue;
                if (string.IsNullOrEmpty(node.StatLabel)) continue;
                agg.TryGetValue(node.StatLabel, out var s);
                s.Value += node.Magnitude;
                s.Unit = node.Unit;
                agg[node.StatLabel] = s;
            }
            return agg;
        }

        /// <summary>
        /// TODO — translate allocated nodes into gameplay modifiers. Deliberately unimplemented:
        /// wire each node id to a gameplay config field as those systems are touched (e.g. push
        /// Max-HP sums into BotConstants.PlayerMaxHp, MoveSpeed into
        /// MovementConfig.MoveSpeedMultiplier). Called once when a raid context is built.
        /// </summary>
        public static void ApplyAllocatedEffects(ProgressionTreeConfig cfg, PlayerProgressionState state)
        {
            // Intentionally empty for now. Suggested shape once effects are hooked up:
            //
            //   foreach (var id in state.AllocatedNodeIds)
            //       switch (id) { case "warden.0.0": /* +MaxHp */ break; ... }
            //
            // or drive it from Summarize(cfg, state) by StatLabel. Left as a single seam so the
            // UI/allocation can ship before every stat is balanced.
        }
    }
}
