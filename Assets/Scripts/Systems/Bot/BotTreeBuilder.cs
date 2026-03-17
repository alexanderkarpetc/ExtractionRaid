using System.Collections.Generic;
using Constants;
using Systems.Bot.BT;
using Systems.Bot.Nodes;

namespace Systems.Bot
{
    public static class BotTreeBuilder
    {
        static readonly Dictionary<string, IBTNode> Cache = new();

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

            if (config.Has(BotBehaviorFlags.Shoot) || config.Has(BotBehaviorFlags.Chase))
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

                if (config.Has(BotBehaviorFlags.Shoot))
                    engageBranch.Add(new ShootNode());

                if (config.Has(BotBehaviorFlags.Chase))
                    engageBranch.Add(new ChaseNode());

                if (engageBranch.Count > 0)
                    combatBranches.Add(new BTSelector("Engage", engageBranch.ToArray()));

                branches.Add(new BTSequence("Combat",
                    new BTCondition("HasTarget?", (bot, _, _) => bot.Blackboard.HasTarget),
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
