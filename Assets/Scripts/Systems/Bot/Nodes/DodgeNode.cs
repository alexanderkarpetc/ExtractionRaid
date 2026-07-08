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

            // Per-type cooldown gate (config.DodgeCooldown). Owned here, timestamp-based:
            // the old BTCooldown wrapper armed only on Success, but this node returns
            // Running when a roll starts — so the config cooldown never applied and bots
            // dodged as often as the global 0.8 s roll cap allowed.
            if (state.ElapsedTime < bb.NextDodgeTime)
                return this.Traced(bot, BTStatus.Failure);

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

            bb.NextDodgeTime = state.ElapsedTime + config.DodgeCooldown;
            bb.DebugStatus = "Dodge";
            return this.Traced(bot, BTStatus.Running);
        }
    }
}
