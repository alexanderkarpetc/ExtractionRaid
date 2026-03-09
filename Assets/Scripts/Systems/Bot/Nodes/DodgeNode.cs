using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    public class DodgeNode : IBTNode
    {
        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            if (!bb.CanSeeTarget || bb.DistanceToTarget > config.EngageRange)
                return BTStatus.Failure;

            if (bb.IsDodging)
            {
                bb.DodgeTimer -= ctx.DeltaTime;
                if (bb.DodgeTimer <= 0f)
                {
                    bb.IsDodging = false;
                    return BTStatus.Success;
                }

                bot.DesiredVelocity = bb.DodgeDirection * config.DodgeSpeed;
                return BTStatus.Running;
            }

            bot.WantsToDodge = true;

            var right = Vector3.Cross(Vector3.up, bot.FacingDirection).normalized;
            bb.DodgeDirection = Random.value > 0.5f ? right : -right;
            bb.DodgeTimer = BotConstants.DodgeDuration;
            bb.IsDodging = true;

            bot.DesiredVelocity = bb.DodgeDirection * config.DodgeSpeed;
            return BTStatus.Running;
        }
    }
}
