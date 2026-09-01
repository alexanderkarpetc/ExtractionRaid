using System.Collections.Generic;
using State;
using UnityEngine;

namespace Progression
{
    /// <summary>Aggregated numeric perk for the build summary. Keystones/specials aren't summed.</summary>
    public struct StatSum { public float Value; public string Unit; }

    /// <summary>
    /// Aggregated character modifiers produced by allocated Predator nodes. Percent fields are
    /// additive inside the tree, then exposed as safe multipliers for runtime configs.
    /// </summary>
    public struct ProgressionModifiers
    {
        public float WeaponDamagePercent;
        public float PenetrationPercent;
        public float ArmorDamagePercent;
        public float HeadshotDamagePercent;
        public float RecoilPercent;
        public float RecoilRecoveryPercent;
        public float ReloadTimePercent;
        public float AimSwayPercent;
        public float EquipTimePercent;
        public float HeatBuildupPercent;
        public float MaxHpBonus;
        public float HealPerKill;
        public float BleedAppliedPercent;
        public float StaminaPerKillPercent;
        public float BossSpawnChancePercent;
        public float BossKillDrops;
        public float CreditsFromLootPercent;

        public float WeaponDamageMultiplier => Multiplier(WeaponDamagePercent);
        public float PenetrationMultiplier => Multiplier(PenetrationPercent);
        public float ArmorDamageMultiplier => Multiplier(ArmorDamagePercent);
        public float HeadshotDamageMultiplier => Multiplier(HeadshotDamagePercent);
        public float RecoilMultiplier => Multiplier(RecoilPercent);
        public float RecoilRecoveryMultiplier => Multiplier(RecoilRecoveryPercent);
        public float ReloadTimeMultiplier => Multiplier(ReloadTimePercent);
        public float EquipTimeMultiplier => Multiplier(EquipTimePercent);
        public float HeatBuildupMultiplier => Multiplier(HeatBuildupPercent);
        public float BleedAppliedMultiplier => Multiplier(BleedAppliedPercent);

        static float Multiplier(float percent) => Mathf.Max(0f, 1f + percent * 0.01f);
    }

    /// <summary>
    /// Stateless rules for the progression tree: what's connected, what's already taken,
    /// and how allocated nodes translate into gameplay modifiers. The view calls these —
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
        /// Translates allocated Predator nodes into gameplay-ready modifiers. Existing seeded
        /// assets predate <see cref="ProgressionEffectType"/>, so centralized stable-id/label
        /// fallbacks keep them functional until they are re-seeded; new/default content uses the enum.
        /// </summary>
        public static ProgressionModifiers ApplyAllocatedEffects(
            ProgressionTreeConfig cfg, PlayerProgressionState state)
        {
            var result = new ProgressionModifiers();
            if (cfg == null || state?.AllocatedNodeIds == null) return result;

            foreach (var id in state.AllocatedNodeIds)
            {
                if (!cfg.TryFind(id, out var discipline, out _, out var node)) continue;
                if (discipline.Id != "predator") continue;

                var effect = node.Effect != ProgressionEffectType.None
                    ? node.Effect
                    : LegacyPredatorEffect(id, node.StatLabel);

                switch (effect)
                {
                    case ProgressionEffectType.WeaponDamage:   result.WeaponDamagePercent += node.Magnitude; break;
                    case ProgressionEffectType.Penetration:    result.PenetrationPercent += node.Magnitude; break;
                    case ProgressionEffectType.ArmorDamage:    result.ArmorDamagePercent += node.Magnitude; break;
                    case ProgressionEffectType.HeadshotDamage: result.HeadshotDamagePercent += node.Magnitude; break;
                    case ProgressionEffectType.Recoil:         result.RecoilPercent += node.Magnitude; break;
                    case ProgressionEffectType.RecoilRecovery: result.RecoilRecoveryPercent += node.Magnitude; break;
                    case ProgressionEffectType.ReloadTime:     result.ReloadTimePercent += node.Magnitude; break;
                    case ProgressionEffectType.AimSway:        result.AimSwayPercent += node.Magnitude; break;
                    case ProgressionEffectType.EquipTime:      result.EquipTimePercent += node.Magnitude; break;
                    case ProgressionEffectType.HeatBuildup:    result.HeatBuildupPercent += node.Magnitude; break;
                    case ProgressionEffectType.MaxHp:          result.MaxHpBonus += node.Magnitude; break;
                    case ProgressionEffectType.HealPerKill:    result.HealPerKill += node.Magnitude; break;
                    case ProgressionEffectType.BleedApplied:   result.BleedAppliedPercent += node.Magnitude; break;
                    case ProgressionEffectType.StaminaPerKill: result.StaminaPerKillPercent += node.Magnitude; break;
                    case ProgressionEffectType.BossSpawnChance: result.BossSpawnChancePercent += node.Magnitude; break;
                    case ProgressionEffectType.BossKillDrops:  result.BossKillDrops += node.Magnitude; break;
                    case ProgressionEffectType.CreditsFromLoot: result.CreditsFromLootPercent += node.Magnitude; break;
                }
            }

            return result;
        }

        static ProgressionEffectType LegacyPredatorEffect(string id, string statLabel)
        {
            var byId = id switch
            {
                "predator.0.0" or "predator.0.4" => ProgressionEffectType.WeaponDamage,
                "predator.0.1" => ProgressionEffectType.Penetration,
                "predator.0.2" => ProgressionEffectType.ArmorDamage,
                "predator.0.3" => ProgressionEffectType.HeadshotDamage,
                "predator.1.0" => ProgressionEffectType.Recoil,
                "predator.1.1" => ProgressionEffectType.RecoilRecovery,
                "predator.1.2" => ProgressionEffectType.ReloadTime,
                "predator.1.3" => ProgressionEffectType.AimSway,
                "predator.1.4" => ProgressionEffectType.EquipTime,
                "predator.1.5" => ProgressionEffectType.HeatBuildup,
                "predator.2.0" or "predator.2.4" => ProgressionEffectType.MaxHp,
                "predator.2.1" => ProgressionEffectType.HealPerKill,
                "predator.2.2" => ProgressionEffectType.BleedApplied,
                "predator.2.3" => ProgressionEffectType.StaminaPerKill,
                "predator.3.0" or "predator.3.4" => ProgressionEffectType.BossSpawnChance,
                "predator.3.1" => ProgressionEffectType.BossKillDrops,
                "predator.3.2" => ProgressionEffectType.CreditsFromLoot,
                _ => ProgressionEffectType.None,
            };
            if (byId != ProgressionEffectType.None) return byId;

            // Last-resort compatibility for hand-authored legacy Predator assets with custom ids.
            return statLabel switch
            {
                "Weapon Damage"      => ProgressionEffectType.WeaponDamage,
                "Penetration"        => ProgressionEffectType.Penetration,
                "Armor Damage"       => ProgressionEffectType.ArmorDamage,
                "Headshot Damage"    => ProgressionEffectType.HeadshotDamage,
                "Recoil"             => ProgressionEffectType.Recoil,
                "Recoil Recovery"    => ProgressionEffectType.RecoilRecovery,
                "Reload Time"        => ProgressionEffectType.ReloadTime,
                "Aim Sway"           => ProgressionEffectType.AimSway,
                "Equip Time"         => ProgressionEffectType.EquipTime,
                "Heat Buildup"       => ProgressionEffectType.HeatBuildup,
                "Max HP"             => ProgressionEffectType.MaxHp,
                "Heal per Kill"      => ProgressionEffectType.HealPerKill,
                "Bleed Applied"      => ProgressionEffectType.BleedApplied,
                "Stamina per Kill"   => ProgressionEffectType.StaminaPerKill,
                "Boss Spawn Chance"  => ProgressionEffectType.BossSpawnChance,
                "Boss Kill Drops"    => ProgressionEffectType.BossKillDrops,
                "Credits from Loot"  => ProgressionEffectType.CreditsFromLoot,
                _                     => ProgressionEffectType.None,
            };
        }
    }
}
