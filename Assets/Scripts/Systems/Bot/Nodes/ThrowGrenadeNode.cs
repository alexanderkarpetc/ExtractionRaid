using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    /// <summary>
    /// Throws a grenade at the player's last known position when all conditions are met:
    ///   - Bot has grenades remaining
    ///   - Bot has a target but cannot currently see them
    ///   - Distance is within throwable range and not too close
    ///   - A random 1–2 s delay has elapsed since conditions were first met
    /// Returns Running while the delay is counting down, Success when the throw intent is set,
    /// and Failure if any prerequisite is not satisfied.
    /// </summary>
    public class ThrowGrenadeNode : IBTNode
    {
        public string Name => "Throw Grenade";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;

            if (!bb.HasTarget || bb.GrenadesRemaining <= 0 || bb.CanSeeTarget)
            {
                bb.GrenadeThrowDelayTimer = -1f;
                return this.Traced(bot, BTStatus.Failure);
            }

            float dist = bb.DistanceToTarget;
            if (dist < config.GrenadeMinThrowDist || dist > GrenadeConstants.MaxThrowRange)
            {
                bb.GrenadeThrowDelayTimer = -1f;
                return this.Traced(bot, BTStatus.Failure);
            }

            if (bb.GrenadeThrowDelayTimer < 0f)
                bb.GrenadeThrowDelayTimer = Random.Range(1f, 2f);

            bb.GrenadeThrowDelayTimer -= ctx.DeltaTime;

            if (bb.GrenadeThrowDelayTimer > 0f)
            {
                bb.DebugStatus = $"Grenade {bb.GrenadeThrowDelayTimer:F1}s";
                return this.Traced(bot, BTStatus.Failure);
            }

            bb.GrenadeThrowDelayTimer = -1f;
            bot.WantsToThrowGrenade = true;
            bot.GrenadeThrowTarget = bb.LastKnownTargetPos;
            bb.DebugStatus = "ThrowGrenade";
            return this.Traced(bot, BTStatus.Success);
        }
    }
}
