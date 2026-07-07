using System.Collections.Generic;
using Constants;
using Systems.Bot.BT;
using Systems.Bot.Nodes;
using UnityEngine;

namespace Systems.Bot
{
    public static class BotTreeBuilder
    {
        static readonly Dictionary<string, IBTNode> Cache = new();

        // Reset cache on every Play-mode enter — Domain Reload is disabled in this project
        // (ProjectSettings/EditorSettings.asset → m_EnterPlayModeOptions: 1), so static
        // dictionaries persist across sessions and would hand back BT trees built from stale
        // BotTypeConfig values after a behavior-flag edit. Hook here is cheap (Clear on
        // already-empty dict), keeps tests + Editor iteration deterministic.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCacheOnPlay() => Cache.Clear();

        public static IBTNode GetOrBuild(in BotTypeConfig config)
        {
            if (Cache.TryGetValue(config.TypeId, out var cached))
                return cached;

            var tree = Build(in config);
            Cache[config.TypeId] = tree;
            return tree;
        }

        static IBTNode Build(in BotTypeConfig config)
        {
            var branches = new List<IBTNode>();

            // FireForward — top-level, NOT gated by HasTarget. Stationary turrets fire continuously
            // in their current facing direction regardless of any perception state. Used by
            // FeedbackRange test bots. Placed first so other branches (if mixed) don't preempt.
            if (config.Has(BotBehaviorFlags.FireForward))
            {
                branches.Add(new FireForwardNode());
            }

            if (config.Has(BotBehaviorFlags.Heal))
            {
                branches.Add(new HealNode());
            }

            if (config.Has(BotBehaviorFlags.Dodge))
            {
                branches.Add(new BTSequence("Dodge",
                    new BTCondition("Damaged?", (bot, _, _) => bot.Blackboard.WasDamaged || bot.IsRolling),
                    new BTCooldown("Dodge CD",
                        new DodgeNode(),
                        config.DodgeCooldown,
                        bb => bb.DodgeCooldownTimer,
                        (bb, v) => bb.DodgeCooldownTimer = v
                    )
                ));
            }

            if (config.Has(BotBehaviorFlags.Shoot) || config.Has(BotBehaviorFlags.Chase)
                || config.Has(BotBehaviorFlags.MeleeAttack))
            {
                var combatBranches = new List<IBTNode>();

                if (config.Has(BotBehaviorFlags.ThrowGrenade))
                {
                    combatBranches.Add(new BTCooldown("Grenade CD",
                        new ThrowGrenadeNode(),
                        config.GrenadeCooldown,
                        bb => bb.GrenadeCooldownTimer,
                        (bb, v) => bb.GrenadeCooldownTimer = v
                    ));
                }

                var engageBranch = new List<IBTNode>();

                // Melee comes first — when target is in contact range we want to
                // attack instead of chase past or shoot at point-blank.
                if (config.Has(BotBehaviorFlags.MeleeAttack))
                    engageBranch.Add(new BTCooldown("Melee CD",
                        new MeleeAttackNode(),
                        config.MeleeAttackCooldown,
                        bb => bb.MeleeAttackCooldownTimer,
                        (bb, v) => bb.MeleeAttackCooldownTimer = v
                    ));

                if (config.Has(BotBehaviorFlags.Shoot))
                    engageBranch.Add(new ShootNode());

                if (config.Has(BotBehaviorFlags.Chase))
                {
                    engageBranch.Add(new ChaseNode());
                    // Chase fails at the last-known-position when the target isn't
                    // visible — SearchNode takes over (scan around, then give up)
                    // instead of the old freeze-until-memory-expires statue.
                    engageBranch.Add(new SearchNode());
                }

                if (engageBranch.Count > 0)
                    combatBranches.Add(new BTSelector("Engage", engageBranch.ToArray()));

                // Alert? — reaction gate. Until the reaction window elapses the bot
                // hasn't "noticed" yet: no chasing, no shooting, no snap-to-target.
                branches.Add(new BTSequence("Combat",
                    new BTCondition("Alert?", (bot, _, _) => bot.Blackboard.HasTarget && bot.Blackboard.IsAlert),
                    new BTSelector("Tactics", combatBranches.ToArray())
                ));
            }

            if (config.Has(BotBehaviorFlags.Patrol))
            {
                branches.Add(new PatrolNode());
            }

            return new BTSelector("Root", branches.ToArray());
        }
    }
}
