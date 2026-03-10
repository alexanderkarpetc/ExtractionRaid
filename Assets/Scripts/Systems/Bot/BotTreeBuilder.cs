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
                branches.Add(new BTSequence(
                    new BTCondition((bot, _, cfg) =>
                    {
                        if (!bot.Blackboard.HasTarget) return false;
                        return true;
                    }),
                    new BTCooldown(
                        new HealNode(),
                        config.HealCooldown,
                        bb => bb.HealCooldownTimer,
                        (bb, v) => bb.HealCooldownTimer = v
                    )
                ));
            }

            if (config.Has(BotBehaviorFlags.Dodge))
            {
                branches.Add(new BTSequence(
                    new BTCondition((bot, _, _) => bot.Blackboard.WasDamaged || bot.IsRolling),
                    new BTCooldown(
                        new DodgeNode(),
                        config.DodgeCooldown,
                        bb => bb.DodgeCooldownTimer,
                        (bb, v) => bb.DodgeCooldownTimer = v
                    )
                ));
            }

            if (config.Has(BotBehaviorFlags.Shoot) || config.Has(BotBehaviorFlags.Chase)
                || config.Has(BotBehaviorFlags.TakeCover))
            {
                var combatBranches = new List<IBTNode>();

                if (config.Has(BotBehaviorFlags.TakeCover))
                {
                    combatBranches.Add(new BTSequence(
                        new BTCondition((bot, state, _) =>
                        {
                            if (!state.HealthMap.TryGetValue(bot.Id, out var hp)) return false;
                            return hp.CurrentHp / hp.MaxHp < 0.5f;
                        }),
                        new TakeCoverNode()
                    ));
                }

                var engageBranch = new List<IBTNode>();

                if (config.Has(BotBehaviorFlags.Shoot))
                    engageBranch.Add(new ShootNode());

                if (config.Has(BotBehaviorFlags.Chase))
                    engageBranch.Add(new ChaseNode());

                if (engageBranch.Count > 0)
                    combatBranches.Add(new BTSelector(engageBranch.ToArray()));

                branches.Add(new BTSequence(
                    new BTCondition((bot, _, _) => bot.Blackboard.HasTarget),
                    new BTSelector(combatBranches.ToArray())
                ));
            }

            if (config.Has(BotBehaviorFlags.Patrol))
            {
                branches.Add(new PatrolNode());
            }

            return new BTSelector(branches.ToArray());
        }
    }
}
