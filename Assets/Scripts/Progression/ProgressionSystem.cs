using System.Collections.Generic;
using State;

namespace Progression
{
    /// <summary>Aggregated numeric perk for the build summary. Keystones/specials aren't summed.</summary>
    public struct StatSum { public float Value; public string Unit; }

    /// <summary>
    /// Stateless rules for the progression tree: what's connected, what can be bought,
    /// and (stubbed) how allocated nodes translate into gameplay. The view calls these —
    /// it never decides allocation itself. There is intentionally NO refund path.
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

        public static bool CanAllocate(ProgressionTreeConfig cfg, PlayerProgressionState state, string id)
        {
            if (cfg == null || state == null) return false;
            if (IsAllocated(state, id)) return false;
            if (!cfg.TryFind(id, out _, out var branch, out var node)) return false;
            if (state.AvailablePoints < node.PointCost) return false;
            return IsConnected(state, branch, node);
        }

        /// <summary>Spend points to allocate. Permanent — returns false if not affordable/connected.</summary>
        public static bool Allocate(ProgressionTreeConfig cfg, PlayerProgressionState state, string id)
        {
            if (!CanAllocate(cfg, state, id)) return false;
            cfg.TryFind(id, out _, out _, out var node);
            state.AllocatedNodeIds.Add(id);
            state.AvailablePoints -= node.PointCost;
            return true;
        }

        public static int SpentPoints(ProgressionTreeConfig cfg, PlayerProgressionState state)
        {
            int spent = 0;
            foreach (var id in state.AllocatedNodeIds)
                if (cfg.TryFind(id, out _, out _, out var node)) spent += node.PointCost;
            return spent;
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
        /// wire each node id to the config field named in its <c>DevHook</c> as those systems are
        /// touched (e.g. push Max-HP sums into BotConstants.PlayerMaxHp, MoveSpeed into
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
