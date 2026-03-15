using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    public class DodgeNode : IBTNode
    {
        public string Name => "Dodge";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;

            if (bot.IsRolling)
            {
                bb.DebugStatus = "Dodge";
                return this.Traced(bot, BTStatus.Running);
            }

            var player = state.PlayerEntity;
            if (player == null) return this.Traced(bot, BTStatus.Failure);

            var toPlayer = (player.Position - bot.Position).normalized;
            var perp = Vector3.Cross(Vector3.up, toPlayer).normalized;
            if (perp.sqrMagnitude < 0.001f)
                perp = Vector3.right;

            var dir = Random.value > 0.5f ? perp : -perp;

            RollSystem.StartBotRoll(bot, dir, state.ElapsedTime);

            if (!bot.IsRolling)
                return this.Traced(bot, BTStatus.Failure);

            bb.DebugStatus = "Dodge";
            return this.Traced(bot, BTStatus.Running);
        }
    }
}
