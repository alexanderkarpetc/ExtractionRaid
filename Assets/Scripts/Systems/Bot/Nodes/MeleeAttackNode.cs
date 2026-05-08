using Constants;
using Session;
using State;
using Systems.Bot.BT;

namespace Systems.Bot.Nodes
{
    /// <summary>
    /// Horde-mode contact attack. Returns Success when the bot is within
    /// <see cref="BotTypeConfig.MeleeAttackRadius"/> of its current target —
    /// wrap у <see cref="BTCooldown"/> for rate-limiting. The actual damage hit
    /// is applied later у tick by <c>BotCombatSystem</c>, which reads
    /// <see cref="BotEntityState.WantsToMeleeAttack"/>.
    ///
    /// Failure cases (out of range, no target) let the parent Selector fall
    /// through to <c>ChaseNode</c>, so zombies keep closing the gap.
    /// </summary>
    public class MeleeAttackNode : IBTNode
    {
        public string Name => "MeleeAttack";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            if (!bb.HasTarget)
                return this.Traced(bot, BTStatus.Failure);

            if (bb.DistanceToTarget > config.MeleeAttackRadius)
                return this.Traced(bot, BTStatus.Failure);

            bb.DebugStatus = "MeleeAttack";
            bot.DesiredVelocity = UnityEngine.Vector3.zero; // plant the feet to swing
            bot.DesiredAimPoint  = bb.LastKnownTargetPos;
            bot.WantsToMeleeAttack = true;
            return this.Traced(bot, BTStatus.Success);
        }
    }
}
