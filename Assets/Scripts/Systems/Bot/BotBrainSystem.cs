using Constants;
using Session;
using State;

namespace Systems.Bot
{
    public static class BotBrainSystem
    {
        public static void Tick(RaidState state, in RaidContext ctx)
        {
            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];

                if (!state.HealthMap.TryGetValue(bot.Id, out var hp) || !hp.IsAlive)
                    continue;

                if (!BotConstants.TryGetConfig(bot.TypeId, out var config))
                    continue;

                bot.ClearIntents();
                bot.Blackboard.DebugStatus = "Idle";

                var tree = BotTreeBuilder.GetOrBuild(in config);
                tree.Tick(bot, state, in ctx, in config);

                bot.Blackboard.WasDamaged = false;
            }
        }
    }
}
