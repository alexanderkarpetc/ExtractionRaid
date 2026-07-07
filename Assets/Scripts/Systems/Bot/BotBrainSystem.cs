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

                // Reaction gate: the timer runs from target acquisition and unlocks the
                // whole response chain (facing, chase, fire) at once. Gating only the
                // first shot was a tell — the bot would whip around instantly, then
                // politely wait out its "reaction time" already aimed at the player.
                var bb = bot.Blackboard;
                if (bb.HasTarget)
                {
                    bb.ReactionTimer += ctx.DeltaTime;
                    if (!bb.IsAlert)
                    {
                        float threshold = config.ReactionTime * bb.ReactionTimeMult + bb.ReactionJitter;
                        if (bb.ReactionTimer >= threshold)
                            bb.IsAlert = true;
                    }
                }

                bot.Blackboard.Trace ??= new BTTrace();
                bot.Blackboard.Trace.Clear();

                var tree = BotTreeBuilder.GetOrBuild(in config);
                tree.Tick(bot, state, in ctx, in config);

                if (bb.HasTarget && !bb.IsAlert)
                    bb.DebugStatus = "Reacting...";
            }
        }
    }
}
